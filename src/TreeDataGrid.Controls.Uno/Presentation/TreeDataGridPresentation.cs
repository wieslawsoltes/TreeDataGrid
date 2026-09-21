using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

/// <summary>Owns view columns and bounded cell-model pools, never the Core source.</summary>
public abstract partial class TreeDataGridPresentation : INotifyPropertyChanged, IDisposable
{
    private TreeDataGridRows? _viewRows;
    private bool _viewRowsActive = true;
    private bool _viewRowsDisposed;
    public abstract ITreeDataGridSource Model { get; }
    public Uno.Controls.Models.TreeDataGrid.ITreeDataGridRows Rows
    {
        get
        {
            ObjectDisposedException.ThrowIf(_viewRowsDisposed, this);
            return _viewRows ??= TreeDataGridRows.Create(this, _viewRowsActive);
        }
    }
    public abstract Uno.Controls.Models.TreeDataGrid.IColumns Columns { get; }
    internal IReadOnlyList<CellColumn> NativeColumns => Columns as IReadOnlyList<CellColumn> ??
        throw new InvalidOperationException("The native presentation requires CellColumn view columns.");
    public abstract TreeDataGridSelection Selection { get; }
    public abstract CellValue RealizeCell(int columnIndex, int rowIndex);
    internal virtual bool TryReuseCell(int columnIndex, int rowIndex, CellValue value) => false;
    public abstract void RecycleCell(CellColumn column, CellValue cell);
    public virtual void Suspend() { _viewRowsActive = false; _viewRows?.Suspend(); }
    public virtual void Resume() { _viewRowsActive = true; _viewRows?.Resume(); }
    public virtual void Dispose() { _viewRowsDisposed = true; _viewRows?.Dispose(); }
    public abstract event EventHandler? ColumnsChanged;
    public abstract event NotifyCollectionChangedEventHandler? RowsChanged;
    internal virtual void ResetColumnMeasurements()
    {
        foreach (var column in NativeColumns) column.ResetWidthMeasurement();
    }
    public static TreeDataGridPresentation Create(ITreeDataGridSource model, ITreeDataGridPresentationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (options is null) return CreateUntyped(model, new());
        var presentation = options.Create(model) ?? throw new InvalidOperationException("The presentation options returned no presentation.");
        if (!ReferenceEquals(presentation.Model, model))
        {
            presentation.Dispose();
            throw new InvalidOperationException("The presentation must use the supplied Core source instance.");
        }
        return presentation;
    }
    internal static TreeDataGridPresentation CreateUntyped(ITreeDataGridSource model, TreeDataGridPresentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.Accept(new Factory(options));
    }
    private sealed class Factory(TreeDataGridPresentationOptions options) : ITreeDataGridSourceVisitor<TreeDataGridPresentation>
    {
        public TreeDataGridPresentation Visit<TModel>(ITreeDataGridSource<TModel> source) where TModel : class =>
            new TreeDataGridPresentation<TModel>(source, options, null);
    }
}

public sealed class TreeDataGridPresentation<TModel> : TreeDataGridPresentation, IColumnVisitor<TModel, CellColumn> where TModel : class
{
    private const int PoolCapacity = 256;
    private const int ColumnPoolCapacity = 32;
    private readonly ITreeDataGridSource<TModel> _model;
    private readonly TreeDataGridPresentationOptions? _options;
    private readonly TreeDataGridPresentationOptions<TModel>? _typedOptions;
    private readonly Dictionary<IColumn, (CellColumn View, string? Key)> _views = new(ReferenceEqualityComparer.Instance);
    // Observe definitions independently of successfully created views. A failed
    // factory must still be retried when its definition is repaired.
    private readonly HashSet<IColumn> _observed = new(ReferenceEqualityComparer.Instance);
    private readonly VisibleColumnList _visible = new();
    private readonly Dictionary<CellColumn, Stack<CellValue>> _pool = new(ReferenceEqualityComparer.Instance);
    private readonly TreeDataGridSelection<TModel> _selection;
    private IRows? _rows;
    private bool _active;
    private bool _disposed;
    private int _pooled;
    private bool _notifyingModel;

    public TreeDataGridPresentation(ITreeDataGridSource<TModel> model, TreeDataGridPresentationOptions<TModel>? options = null)
        : this(model, null, options ?? new()) { }
    internal TreeDataGridPresentation(ITreeDataGridSource<TModel> model, TreeDataGridPresentationOptions? options,
        TreeDataGridPresentationOptions<TModel>? typedOptions)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _options = options;
        _typedOptions = typedOptions;
        _selection = new(model, _visible);
        try { Resume(); }
        catch { Dispose(); throw; }
    }
    public override ITreeDataGridSource<TModel> Model => _model;
    public override Uno.Controls.Models.TreeDataGrid.IColumns Columns => _visible;
    public override TreeDataGridSelection Selection => _selection;
    public override event EventHandler? ColumnsChanged;
    public override event NotifyCollectionChangedEventHandler? RowsChanged;
    internal override void ResetColumnMeasurements()
    {
        // Hidden definitions retain their views. They must not bring back width
        // measurements from an old font/theme when made visible later.
        foreach (var view in _views.Values) view.View.ResetWidthMeasurement();
    }

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
                try
                {
                    if (column.TryReuseCell(value, row)) { column.ConfigureCell(value); return value; }
                }
                catch { value.Dispose(); throw; }
                value.Dispose();
            }
        }
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_active || !_visible.Contains(column)) throw new InvalidOperationException("The presentation changed while realizing a cell.");
        var created = column.CreateCell(row);
        column.ConfigureCell(created);
        return created;
    }

    internal override bool TryReuseCell(int columnIndex, int rowIndex, CellValue value)
    {
        if (_disposed || !_active || (uint)columnIndex >= (uint)_visible.Count || (uint)rowIndex >= (uint)Rows.Count) return false;
        var column = _visible[columnIndex];
        if (!column.TryReuseCell(value, Rows[rowIndex]) || _disposed || !_active ||
            (uint)columnIndex >= (uint)_visible.Count || !ReferenceEquals(column, _visible[columnIndex])) return false;
        column.ConfigureCell(value);
        return true;
    }

    public override void RecycleCell(CellColumn column, CellValue cell)
    {
        var pooled = false;
        try
        {
            if (_active && _pooled < PoolCapacity && _visible.Contains(column) &&
                (_pool.ContainsKey(column) || _pool.Count < ColumnPoolCapacity) && cell.TrySuspend() &&
                _active && !_disposed && _visible.Contains(column) && _pooled < PoolCapacity &&
                (_pool.ContainsKey(column) || _pool.Count < ColumnPoolCapacity))
            {
                if (!_pool.TryGetValue(column, out var values)) _pool[column] = values = new();
                values.Push(cell);
                ++_pooled;
                pooled = true;
            }
        }
        finally { if (!pooled) cell.Dispose(); }
    }

    public override void Suspend()
    {
        if (!_active) return;
        _active = false;
        base.Suspend();
        _selection.Suspend();
        _model.PropertyChanged -= OnModelChanged;
        _model.Sorted -= OnSorted;
        if (_model.Columns is INotifyCollectionChanged columns) columns.CollectionChanged -= OnColumnsChanged;
        if (_rows is not null) _rows.CollectionChanged -= OnRowsChanged;
        _rows = null;
        foreach (var column in _observed) column.PropertyChanged -= OnColumnChanged;
        foreach (var view in _views.Values) view.View.PropertyChanged -= OnViewChanged;
        ClearPool();
        RaisePropertyChanged(new(nameof(SelectionInteraction)));
    }

    public override void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_active) return;
        SynchronizeColumns();
        _active = true;
        base.Resume();
        foreach (var column in _observed) column.PropertyChanged += OnColumnChanged;
        foreach (var view in _views.Values) view.View.PropertyChanged += OnViewChanged;
        _model.PropertyChanged += OnModelChanged;
        _model.Sorted += OnSorted;
        if (_model.Columns is INotifyCollectionChanged columns) columns.CollectionChanged += OnColumnsChanged;
        SetRows();
        _selection.Resume();
        RaisePropertyChanged(new(nameof(SelectionInteraction)));
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        List<Exception>? errors = null;
        try { Suspend(); }
        catch (Exception error) { (errors ??= new()).Add(error); }
        try { base.Dispose(); }
        catch (Exception error) { (errors ??= new()).Add(error); }
        var views = _views.Values.ToArray();
        _views.Clear();
        _observed.Clear();
        try { _visible.Clear(); }
        catch (Exception error) { (errors ??= new()).Add(error); }
        foreach (var view in views)
            try { view.View.Dispose(); }
            catch (Exception error) { (errors ??= new()).Add(error); }
        ThrowCleanupErrors(errors);
    }

    public CellColumn Visit<TValue>(ValueColumn<TModel, TValue> column)
    {
        if (column.PresentationKey is { } key)
        {
            if (_typedOptions is not null && _typedOptions.Columns.TryGetValue(key, out var typedCreate))
            {
                var result = typedCreate(column) ?? throw new InvalidOperationException("The column factory returned no column.");
                if (result is CellColumn native) { native.AttachModel(column); return native; }
                try { return new CellColumnAdapter<TModel>(column, result); }
                catch { (result as IDisposable)?.Dispose(); throw; }
            }
            if (_options is not null && _options.Columns.TryGetValue(key, out var create))
            {
                var result = create(column) ?? throw new InvalidOperationException("The column factory returned no column.");
                result.AttachModel(column);
                return result;
            }
        }
        if (ColumnPresentationRegistry.TryCreate(column, out var registered)) return registered;
        if (column is CheckBoxColumn<TModel>) return new ValueCellColumn<TModel, TValue>(column, CellKind.CheckBox);
        if (column is TemplateColumn<TModel>) return new ValueCellColumn<TModel, TValue>(column, CellKind.Template);
        if (column.PresentationKey is { } missing)
            throw new InvalidOperationException($"No Uno column presentation is registered for '{missing}'.");
        return new ValueCellColumn<TModel, TValue>(column, CellKind.Text);
    }

    public CellColumn Visit(HierarchicalExpanderColumn<TModel> column) => new ExpanderCellColumn<TModel>(column, column.Inner.Accept(this));

    private void ClearPool()
    {
        var pools = _pool.Values.ToArray();
        _pool.Clear();
        _pooled = 0;
        List<Exception>? errors = null;
        foreach (var pool in pools)
            while (pool.TryPop(out var value))
                try { value.Dispose(); }
                catch (Exception error) { (errors ??= new()).Add(error); }
        ThrowCleanupErrors(errors);
    }

    private static void ThrowCleanupErrors(List<Exception>? errors)
    {
        if (errors is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException(errors);
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
            // Retire the pool before committing definitions. If a custom cell
            // throws during cleanup, every newly staged view still needs disposal.
            ClearPool();
        }
        catch (Exception error)
        {
            var errors = new List<Exception> { error };
            foreach (var replacement in replacements.Values)
                try { replacement.View.Dispose(); }
                catch (Exception cleanupError) { errors.Add(cleanupError); }
            ThrowCleanupErrors(errors);
            throw;
        }

        // Commit only after all factories have succeeded. Existing realized cells
        // and the previous projection remain valid if a replacement throws.
        var retired = new List<CellColumn>();
        foreach (var column in _views.Keys.ToArray())
            if (!desired.Contains(column) || replacements.ContainsKey(column))
            {
                retired.Add(_views[column].View);
                if (_active) _views[column].View.PropertyChanged -= OnViewChanged;
                _views.Remove(column);
            }
        foreach (var replacement in replacements)
        {
            _views.Add(replacement.Key, replacement.Value);
            if (_active) replacement.Value.View.PropertyChanged += OnViewChanged;
        }
        List<Exception>? notificationErrors = null;
        try
        {
            var next = new List<CellColumn>(_model.Columns.Count);
            foreach (var column in _model.Columns)
                if (column.IsVisible) next.Add(_views[column].View);
            // Publish index maps before observers receive the atomic list change.
            _selection.ColumnsChanged(next);
            _visible.Synchronize(next);
        }
        catch (Exception error) { (notificationErrors ??= new()).Add(error); }
        try { ColumnsChanged?.Invoke(this, EventArgs.Empty); }
        catch (Exception error) { (notificationErrors ??= new()).Add(error); }
        foreach (var column in retired)
            try { column.Dispose(); }
            catch (Exception error) { (notificationErrors ??= new()).Add(error); }
        ThrowCleanupErrors(notificationErrors);
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
    private void OnSorted()
    {
        RowsChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
        if (_active && !_disposed) RaiseSorted();
    }
    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SynchronizeColumns();
    private void OnColumnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(IColumn.IsVisible) or nameof(IColumn.PresentationKey)) SynchronizeColumns();
        else
        {
            _notifyingModel = true;
            try { foreach (var view in _views.Values) view.View.ModelChanged(e); }
            finally { _notifyingModel = false; }
            ColumnsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    private void OnViewChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_notifyingModel && e.PropertyName != nameof(CellColumn.ActualWidth))
            ColumnsChanged?.Invoke(this, EventArgs.Empty);
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_active || _disposed) return;
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(Model.Rows)) SetRows();
        if (!_active || _disposed) return;
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(Model.Selection))
        {
            _selection.Refresh();
            if (_active && !_disposed) RaisePropertyChanged(new(nameof(SelectionInteraction)));
        }
        else RaisePropertyChanged(e);
    }
}
