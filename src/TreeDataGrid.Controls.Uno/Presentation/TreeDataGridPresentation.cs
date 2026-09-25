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

public sealed partial class TreeDataGridPresentation<TModel> : TreeDataGridPresentation, IColumnVisitor<TModel, CellColumn> where TModel : class
{
    private const int PoolCapacity = 256;
    private const int ColumnPoolCapacity = 32;
    private readonly ITreeDataGridSource<TModel> _model;
    private readonly TreeDataGridPresentationOptions? _options;
    private readonly TreeDataGridPresentationOptions<TModel>? _typedOptions;
    private readonly Dictionary<IColumn, (CellColumn View, string? Key)> _views = new(ReferenceEqualityComparer.Instance);
    // Bind one handler to each definition, not the event's sender. Expanders
    // forward an inner definition's event without changing its sender. Different
    // definitions may share that inner column, but each view is notified once.
    // Observations also survive failed factories so a repaired definition retries.
    private readonly Dictionary<IColumn, ColumnObservation> _observed = new(ReferenceEqualityComparer.Instance);
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
        catch (Exception error)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
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

    private bool _resuming;
    private bool _suspending;
    private bool _resumeAgain;
    private int _lifecycleVersion;

    public override void Suspend()
    {
        _resumeAgain = false;
        if (_suspending || (!_active && !_resuming)) return;
        _suspending = true;
        unchecked { ++_cellOperationVersion; ++_lifecycleVersion; }
        _active = false;
        // Snapshot ownership before the first application callback. Every old
        // subscription/pool is attempted even when an earlier cleanup fails.
        var observations = _observed.Values.ToArray();
        var views = _views.Values.ToArray();
        var rows = _rows;
        _rows = null;
        List<Exception>? errors = null;
        try
        {
            Run(base.Suspend);
            Run(_selection.Suspend);
            Run(() => _model.PropertyChanged -= OnModelChanged);
            Run(() => _model.Sorted -= OnSorted);
            Run(() => { if (_model.Columns is INotifyCollectionChanged columns) columns.CollectionChanged -= OnColumnsChanged; });
            Run(() => { if (rows is not null) rows.CollectionChanged -= OnRowsChanged; });
            foreach (var observation in observations) Run(observation.UpdateSubscription);
            foreach (var view in views) view.View.PropertyChanged -= OnViewChanged;
            Run(ClearPool);
            Run(() => RaisePropertyChanged(new(nameof(SelectionInteraction))));
        }
        finally { _suspending = false; }
        if (_resumeAgain && !_resuming && !_disposed) Run(Resume);
        ThrowCleanupErrors(errors);
        void Run(Action action) { try { action(); } catch (Exception error) { (errors ??= new()).Add(error); } }
    }

    public override void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_suspending) { _resumeAgain = true; return; }
        if (_active) return;
        if (_resuming) { _resumeAgain = true; return; }
        _resuming = true;
        try
        {
            do
            {
                _resumeAgain = false;
                ResumeCore();
            }
            while (_resumeAgain && !_disposed && !_active);
        }
        finally { _resuming = false; _resumeAgain = false; }
    }

    private void ResumeCore()
    {
        var version = unchecked(++_lifecycleVersion);
        try
        {
            SynchronizeColumns();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (version != _lifecycleVersion) return;
            _active = true;
            base.Resume();
            if (!Current()) return;
            foreach (var observation in _observed.Values.ToArray())
            {
                observation.UpdateSubscription();
                if (!Current()) return;
            }
            foreach (var view in _views.Values) view.View.PropertyChanged += OnViewChanged;
            _model.PropertyChanged += OnModelChanged;
            _model.Sorted += OnSorted;
            if (_model.Columns is INotifyCollectionChanged columns) columns.CollectionChanged += OnColumnsChanged;
            SetRows();
            if (!Current()) return;
            _selection.Resume();
            if (Current()) RaisePropertyChanged(new(nameof(SelectionInteraction)));
        }
        catch (Exception error)
        {
            if (Current())
            {
                try { Suspend(); }
                catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            }
            throw;
        }
        bool Current() => !_disposed && _active && version == _lifecycleVersion;
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
                catch (Exception error)
                {
                    try { (result as IDisposable)?.Dispose(); }
                    catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                    throw;
                }
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
        _emptyCellStacks.Clear();
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

    private void SetRows()
    {
        var rows = _model.Rows;
        if (ReferenceEquals(rows, _rows)) return;
        unchecked { ++_cellOperationVersion; }
        ClearPool();
        if (_rows is not null) _rows.CollectionChanged -= OnRowsChanged;
        _rows = rows;
        _rows.CollectionChanged += OnRowsChanged;
        RowsChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
    }
    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_active || _disposed) return;
        unchecked { ++_cellOperationVersion; }
        RowsChanged?.Invoke(this, e);
    }
    // Flat Core rows intentionally reuse one anonymous row object and publish
    // sort completion on the source, without a collection Reset notification.
    private void OnSorted()
    {
        if (!_active || _disposed) return;
        unchecked { ++_cellOperationVersion; }
        RowsChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
        if (_active && !_disposed) RaiseSorted();
    }
    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_active && !_disposed) SynchronizeColumns();
    }
    private void OnColumnChanged(IColumn column, PropertyChangedEventArgs e)
    {
        if (!_active || _disposed || !_observed.ContainsKey(column)) return;
        if (_synchronizingColumns)
        {
            unchecked { ++_cellOperationVersion; }
            _columnsAgain = true;
        }
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(IColumn.IsVisible) or nameof(IColumn.PresentationKey))
        {
            SynchronizeColumns();
            return;
        }
        if (!_views.TryGetValue(column, out var view)) return;
        var wasNotifyingModel = _notifyingModel;
        _notifyingModel = true;
        try { view.View.ModelChanged(e); }
        finally { _notifyingModel = wasNotifyingModel; }
        // ModelChanged invokes user code. It may dispose this presentation,
        // remove the definition or replace its view before returning.
        if (_active && !_disposed && !_synchronizingColumns && _views.TryGetValue(column, out var current) && ReferenceEquals(current.View, view.View))
            ColumnsChanged?.Invoke(this, EventArgs.Empty);
    }
    private void OnViewChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_active && !_disposed && !_notifyingModel && e.PropertyName != nameof(CellColumn.ActualWidth))
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
