using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

public sealed class TreeDataGridPresentationOptions
{
    public Dictionary<string, Func<IColumn, CellColumn>> Columns { get; } = new();
}

/// <summary>Owns view columns and bounded cell-model pools, never the Core source.</summary>
public abstract class TreeDataGridPresentation : IDisposable
{
    public abstract ITreeDataGridSource Model { get; }
    public IRows Rows => Model.Rows;
    public abstract IReadOnlyList<CellColumn> Columns { get; }
    public abstract TreeDataGridSelection Selection { get; }
    public abstract CellValue RealizeCell(int columnIndex, int rowIndex);
    public abstract void RecycleCell(CellColumn column, CellValue cell);
    public abstract void Suspend();
    public abstract void Resume();
    public abstract void Dispose();
    public abstract event EventHandler? ColumnsChanged;
    public abstract event NotifyCollectionChangedEventHandler? RowsChanged;
    public static TreeDataGridPresentation Create(ITreeDataGridSource model, TreeDataGridPresentationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.Accept(new Factory(options ?? new()));
    }
    private sealed class Factory(TreeDataGridPresentationOptions options) : ITreeDataGridSourceVisitor<TreeDataGridPresentation>
    {
        public TreeDataGridPresentation Visit<TModel>(ITreeDataGridSource<TModel> source) where TModel : class =>
            new TreeDataGridPresentation<TModel>(source, options);
    }
}

internal sealed class TreeDataGridPresentation<TModel> : TreeDataGridPresentation, IColumnVisitor<TModel, CellColumn> where TModel : class
{
    private const int PoolCapacity = 256;
    private const int ColumnPoolCapacity = 32;
    private readonly ITreeDataGridSource<TModel> _model;
    private readonly TreeDataGridPresentationOptions _options;
    private readonly Dictionary<IColumn, (CellColumn View, string? Key)> _views = new(ReferenceEqualityComparer.Instance);
    // Observe definitions independently of successfully created views. A failed
    // factory must still be retried when its definition is repaired.
    private readonly HashSet<IColumn> _observed = new(ReferenceEqualityComparer.Instance);
    private readonly List<CellColumn> _visible = new();
    private readonly Dictionary<CellColumn, Stack<CellValue>> _pool = new(ReferenceEqualityComparer.Instance);
    private readonly TreeDataGridSelection<TModel> _selection;
    private IRows? _rows;
    private bool _active;
    private bool _disposed;
    private int _pooled;

    public TreeDataGridPresentation(ITreeDataGridSource<TModel> model, TreeDataGridPresentationOptions options)
    {
        _model = model;
        _options = options;
        _selection = new(model, _visible);
        try { Resume(); }
        catch { Dispose(); throw; }
    }
    public override ITreeDataGridSource Model => _model;
    public override IReadOnlyList<CellColumn> Columns => _visible;
    public override TreeDataGridSelection Selection => _selection;
    public override event EventHandler? ColumnsChanged;
    public override event NotifyCollectionChangedEventHandler? RowsChanged;

    public override CellValue RealizeCell(int columnIndex, int rowIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_active) throw new InvalidOperationException("The presentation is suspended.");
        var column = _visible[columnIndex];
        var row = Rows[rowIndex];
        if (_pool.TryGetValue(column, out var values))
        {
            while (values.TryPop(out var value))
            {
                --_pooled;
                try { if (value.TryRetarget(row)) return value; }
                catch { value.Dispose(); throw; }
                value.Dispose();
            }
        }
        return column.CreateCell(row);
    }

    public override void RecycleCell(CellColumn column, CellValue cell)
    {
        if (_active && _pooled < PoolCapacity && _visible.Contains(column) &&
            (_pool.ContainsKey(column) || _pool.Count < ColumnPoolCapacity) && cell.TrySuspend())
        {
            if (!_pool.TryGetValue(column, out var values)) _pool[column] = values = new();
            values.Push(cell);
            ++_pooled;
        }
        else cell.Dispose();
    }

    public override void Suspend()
    {
        if (!_active) return;
        _active = false;
        _selection.Suspend();
        _model.PropertyChanged -= OnModelChanged;
        _model.Sorted -= OnSorted;
        if (_model.Columns is INotifyCollectionChanged columns) columns.CollectionChanged -= OnColumnsChanged;
        if (_rows is not null) _rows.CollectionChanged -= OnRowsChanged;
        _rows = null;
        foreach (var column in _observed) column.PropertyChanged -= OnColumnChanged;
        ClearPool();
    }

    public override void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_active) return;
        SynchronizeColumns();
        _active = true;
        foreach (var column in _observed) column.PropertyChanged += OnColumnChanged;
        _model.PropertyChanged += OnModelChanged;
        _model.Sorted += OnSorted;
        if (_model.Columns is INotifyCollectionChanged columns) columns.CollectionChanged += OnColumnsChanged;
        SetRows();
        _selection.Resume();
    }

    public override void Dispose()
    {
        if (_disposed) return;
        Suspend();
        _disposed = true;
        foreach (var view in _views.Values) view.View.Dispose();
        _views.Clear();
        _observed.Clear();
        _visible.Clear();
    }

    public CellColumn Visit<TValue>(ValueColumn<TModel, TValue> column)
    {
        if (column.PresentationKey is { } key && _options.Columns.TryGetValue(key, out var create)) return create(column);
        if (column is CheckBoxColumn<TModel>) return new ValueCellColumn<TModel, TValue>(column, CellKind.CheckBox);
        if (column is TemplateColumn<TModel>) return new ValueCellColumn<TModel, TValue>(column, CellKind.Template);
        if (column.PresentationKey is { } missing)
            throw new InvalidOperationException($"No Uno column presentation is registered for '{missing}'.");
        return new ValueCellColumn<TModel, TValue>(column, CellKind.Text);
    }

    public CellColumn Visit(HierarchicalExpanderColumn<TModel> column) => new ExpanderCellColumn<TModel>(column, column.Inner.Accept(this));

    private void ClearPool()
    {
        foreach (var pool in _pool.Values)
            foreach (var value in pool) value.Dispose();
        _pool.Clear();
        _pooled = 0;
    }

    private void SynchronizeColumns()
    {
        var desired = new HashSet<IColumn>(_model.Columns, ReferenceEqualityComparer.Instance);
        foreach (var removed in _observed.Where(x => !desired.Contains(x)).ToArray())
        {
            if (_active) removed.PropertyChanged -= OnColumnChanged;
            _observed.Remove(removed);
        }
        foreach (var column in desired)
            if (_observed.Add(column) && _active) column.PropertyChanged += OnColumnChanged;

        var replacements = new Dictionary<IColumn, (CellColumn View, string? Key)>(ReferenceEqualityComparer.Instance);
        try
        {
            foreach (IColumn<TModel> column in desired)
            {
                if (_views.TryGetValue(column, out var current) && current.Key == column.PresentationKey) continue;
                var view = column.Accept(this) ?? throw new InvalidOperationException("A column factory returned null.");
                replacements.Add(column, (view, column.PresentationKey));
            }
        }
        catch
        {
            foreach (var replacement in replacements.Values) replacement.View.Dispose();
            throw;
        }

        // Commit only after all factories have succeeded. Existing realized cells
        // and the previous projection remain valid if a replacement throws.
        ClearPool();
        var retired = new List<CellColumn>();
        foreach (var column in _views.Keys.ToArray())
            if (!desired.Contains(column) || replacements.ContainsKey(column))
            {
                retired.Add(_views[column].View);
                _views.Remove(column);
            }
        foreach (var replacement in replacements) _views.Add(replacement.Key, replacement.Value);
        _visible.Clear();
        foreach (var column in _model.Columns)
            if (column.IsVisible) _visible.Add(_views[column].View);
        _selection.ColumnsChanged();
        try { ColumnsChanged?.Invoke(this, EventArgs.Empty); }
        finally { foreach (var column in retired) column.Dispose(); }
    }

    private void SetRows()
    {
        var rows = _model.Rows;
        if (ReferenceEquals(rows, _rows)) return;
        ClearPool();
        if (_rows is not null) _rows.CollectionChanged -= OnRowsChanged;
        _rows = rows;
        _rows.CollectionChanged += OnRowsChanged;
        RowsChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
    }
    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RowsChanged?.Invoke(this, e);
    // Flat Core rows intentionally reuse one anonymous row object and publish
    // sort completion on the source, without a collection Reset notification.
    private void OnSorted() => RowsChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SynchronizeColumns();
    private void OnColumnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(IColumn.IsVisible) or nameof(IColumn.PresentationKey)) SynchronizeColumns();
        else ColumnsChanged?.Invoke(this, EventArgs.Empty);
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(Model.Rows)) SetRows();
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(Model.Selection)) _selection.Refresh();
    }
}
