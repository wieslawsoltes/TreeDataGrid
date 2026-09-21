using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using Windows.Foundation;
using IRow = TreeDataGridCore.Models.IRow;

namespace Uno.Controls.Primitives;

/// <summary>Reference columnar layout with row-owned cell models and bounded parented recycling.</summary>
public partial class TreeDataGridCellsPresenter : TreeDataGridColumnarPresenterBase<IColumn>
{
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(ITreeDataGridRows), typeof(TreeDataGridCellsPresenter), new PropertyMetadata(null, OnRowsChanged));

    private readonly Dictionary<int, TreeDataGridCell> _realized = new();
    private readonly Dictionary<TreeDataGridCell, CellLease> _owned = new();
    private readonly Dictionary<IColumn, Stack<TreeDataGridCell>> _pool = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<TreeDataGridCell, PoolEntry> _pooled = new();
    private readonly Dictionary<TreeDataGridCell, PoolEntry> _recycling = new();
    private readonly HashSet<TreeDataGridCell> _deferred = new();
    private readonly Dictionary<TreeDataGridCell, CellLease> _retainedModels = new();
    private TreeDataGridRow? _row;
    private ITreeDataGridRows? _observedRows;
    private bool _detached;
    private bool _deferRowRebind;
    private bool _resettingCells;
    private bool _prunePool;
    private readonly record struct PoolEntry(IColumn Column, TreeDataGridRowsPresenter? BudgetOwner);
    private readonly record struct CellLease(ITreeDataGridRows Rows, IColumn Column, ICell Model,
        TreeDataGridPresentation? Presentation, CellValue? Value, TreeDataGridRowsPresenter? Presenter,
        TreeDataGridRowsPresenter? BudgetOwner, TreeDataGridElementFactory Factory, IRow Row, object? RowModel, int ColumnIndex, int RowIndex);

    public TreeDataGridCellsPresenter()
    {
        Loaded += (_, _) => { _detached = false; ObserveRows(); };
        Unloaded += (_, _) => { _detached = true; ObserveRows(); FinalizeUnrealize(); };
    }

    private TreeDataGridRowsPresenter? Presenter => _row?.Presenter;
    private TreeDataGridPresentation? Presentation => _row?.Presentation;
    protected override Orientation Orientation => Orientation.Horizontal;
    protected override bool OwnsRecyclingPool => true;
    public ITreeDataGridRows? Rows { get => (ITreeDataGridRows?)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public int RowIndex { get; private set; } = -1;
    public IReadOnlyCollection<TreeDataGridCell> RealizedCells => _realized.Values;
    public override TreeDataGridCell? TryGetElement(int index) => _realized.GetValueOrDefault(index);
    protected override Control? GetRealizedElement(int index) => TryGetElement(index);
    public override IEnumerable<Control> GetRealizedElements() => _realized.OrderBy(pair => pair.Key).Select(pair => (Control)pair.Value);

    internal void Attach(TreeDataGridRow row)
    {
        _row = row;
        ElementFactory = row.ElementFactory ?? row.Presenter?.Owner?.ElementFactory ?? ElementFactory ?? new();
        Items = row.Columns;
        Rows = row.Rows;
        if (row.RowIndex >= 0 && RowIndex < 0) Realize(row.RowIndex);
        else if (row.RowIndex >= 0) UpdateRowIndex(row.RowIndex);
        ObserveRows();
        // Preserve synchronous row rebind before RowPrepared. The shared engine
        // tracks these pre-realized controls in its in-flight ownership map.
        if (RowIndex >= 0 && _deferred.Count > 0 && Presenter is { } presenter)
        {
            var (first, end) = presenter.VisibleColumns;
            for (var index = first; index < end; ++index)
                if (TryRealizeElementAt(index) is null) break;
        }
        InvalidateMeasure();
    }

    public void Realize(int index)
    {
        if (RowIndex >= 0) throw new InvalidOperationException("Row is already realized.");
        if (index < 0 || (Rows is { } rows && index >= rows.Count)) throw new ArgumentOutOfRangeException(nameof(index));
        ElementFactory ??= new();
        RowIndex = index;
        _deferRowRebind = false;
        ObserveRows();
        InvalidateMeasure();
    }

    public void UpdateRowIndex(int index)
    {
        if (RowIndex < 0 || index < 0 || Rows is null || index >= Rows.Count) return;
        RowIndex = index;
        foreach (var pair in _realized)
        {
            pair.Value.UpdateIndexes(index, pair.Key);
            if (_owned.TryGetValue(pair.Value, out var lease)) _owned[pair.Value] = lease with { RowIndex = index };
        }
    }

    public void Unrealize()
    {
        _deferRowRebind = Presenter is not null && !_resettingCells;
        RowIndex = -1;
        ObserveRows();
        RetireLayout();
        if (!_deferRowRebind) FinalizeUnrealize();
    }

    internal void Reset()
    {
        _resettingCells = true;
        _deferRowRebind = false;
        RowIndex = -1;
        List<Exception>? errors = null;
        Run(RetireLayout);
        Run(FinalizeUnrealize);
        foreach (var cell in _pooled.Keys.ToArray()) Run(() => RemoveRecycledElement(cell));
        Run(() => Items = null);
        Run(() => Rows = null);
        Run(() => ElementFactory = null);
        _row = null;
        ObserveRows();
        _resettingCells = false;
        ThrowErrors(errors);
        void Run(Action action) { try { action(); } catch (Exception error) { (errors ??= new()).Add(error); } }
    }

    internal void SynchronizeColumns()
    {
        if (_row is { } row) Items = row.Columns;
        PrunePooledColumns();
        UpdateSelection();
        InvalidateMeasure();
    }

    internal void UpdateSelection()
    {
        var generation = PresenterGeneration;
        foreach (var cell in _realized.Values)
        {
            if (_owned.TryGetValue(cell, out var lease) && lease.Presenter is { } presenter)
                presenter.UpdateSelection(cell);
            else
            {
                var selected = _row?.StandaloneSelection?.IsCellSelected(cell.ColumnIndex, RowIndex) == true;
                if (generation != PresenterGeneration) return;
                cell.IsSelected = selected;
            }
            if (generation != PresenterGeneration) return;
        }
    }

    protected override Control GetElementFromFactory(IColumn column, int index)
    {
        var generation = PresenterGeneration;
        var rowIndex = RowIndex;
        var rows = Rows ?? throw new InvalidOperationException("Cells require an ITreeDataGridRows collection.");
        var factory = ElementFactory ?? throw new InvalidOperationException("Cells require an element factory.");
        var row = rows[rowIndex];
        var rowModel = row.Model;
        var presentation = Presentation;
        var native = presentation is not null && ReferenceEquals(rows, presentation.Rows) && ReferenceEquals(Items, presentation.Columns);
        var nativeColumn = native ? (CellColumn)column : null;
        var nativePresenter = native ? Presenter : null;
        CellValue? value = null;
        ICell? model = null;
        TreeDataGridCell? cell = null;
        var owned = false;
        try
        {
            if (nativeColumn is not null)
            {
                value = TakeRetainedModel(nativeColumn, presentation!, index, rowIndex);
                // A custom reuse callback may replace/dispose the entire source.
                // Check retirement before attempting a fresh cell in the old view.
                EnsureGeneration(generation);
                value ??= presentation!.RealizeCell(index, rowIndex);
                EnsureGeneration(generation);
            }
            model = value?.PresentationModel ?? rows.RealizeCell(column, index, rowIndex);
            EnsureGeneration(generation);
            var legacy = nativePresenter?.Owner is { HasCustomCellFactory: true } owner && ReferenceEquals(factory, owner.ElementFactory);
            while (_pool.TryGetValue(column, out var pool) && pool.TryPop(out var candidate))
            {
                if (pool.Count == 0) _pool.Remove(column);
                ReleaseReservation(candidate);
                var reusable = false;
                try
                {
                    ReleaseRetainedModel(candidate);
                    var compatible = legacy || factory.CanReuseElement(candidate, model);
                    EnsureGeneration(generation);
                    reusable = compatible;
                }
                finally { if (!reusable) RemoveRecycledElement(candidate); }
                if (reusable) { cell = candidate; break; }
            }
            EnsureGeneration(generation);
            if (cell is null)
            {
                var created = legacy ? nativePresenter!.Owner!.CellFactory(nativeColumn!) : factory.GetOrCreateElement(model, this);
                if (created is not TreeDataGridCell candidate || candidate.RowIndex >= 0 ||
                    (candidate.Parent is not null && !ReferenceEquals(candidate.Parent, this)))
                    throw new InvalidOperationException("The cell factory must return an unrealized cell owned by this presenter or no parent.");
                cell = candidate;
            }
            EnsureGeneration(generation);
            _owned.Add(cell, new(rows, column, model, native ? presentation : null, value, nativePresenter,
                Presenter, factory, row, rowModel, index, rowIndex));
            owned = true;
            return cell;
        }
        finally
        {
            if (!owned)
            {
                try
                {
                    if (value is not null) value.Dispose();
                    else if (model is not null) rows.UnrealizeCell(model, index, rowIndex);
                }
                finally { if (cell is not null) RemoveRecycledElement(cell); }
            }
        }
    }

    protected override void RealizeElement(Control element, IColumn column, int index)
    {
        var cell = (TreeDataGridCell)element;
        var lease = _owned[cell];
        var generation = PresenterGeneration;
        cell.Presenter = lease.Presenter;
        cell.OwningRow = _row;
        cell.ContainerFactory = lease.Factory;
        if (lease.Value is { } value && lease.Column is CellColumn nativeColumn && lease.Presenter?.Owner is { } owner)
        {
            owner.CellTemplates.TryGetValue(nativeColumn.Model.PresentationKey ?? "", out var template);
            owner.CellEditingTemplates.TryGetValue(nativeColumn.Model.PresentationKey ?? "", out var editing);
            var modelTemplate = value is ExpanderCellValue ? null : value.GetCellTemplate(cell);
            EnsureGeneration(generation);
            template = modelTemplate ?? nativeColumn.GetCellTemplate(cell) ?? template;
            EnsureGeneration(generation);
            var modelEditing = value is ExpanderCellValue ? null : value.GetCellEditingTemplate(cell);
            EnsureGeneration(generation);
            editing = modelEditing ?? nativeColumn.GetCellEditingTemplate(cell) ?? editing;
            EnsureGeneration(generation);
            cell.RealizeNativeInRow(nativeColumn, value, lease.Row, lease.RowModel, index, RowIndex, template, editing);
        }
        else
            cell.RealizeInRow(lease.Factory, _row?.StandaloneSelection, lease.Model, index, RowIndex, lease.Row, lease.RowModel, column as CellColumn);
        EnsureGeneration(generation);
        if (lease.Presenter is { } presenter) presenter.UpdateSelection(cell);
        EnsureGeneration(generation);
        _realized.Add(index, cell);
        lease.BudgetOwner?.RegisterCell(cell);
        if (_deferred.Remove(cell)) cell.EndRebind(true);
        EnsureGeneration(generation);
        cell.NotifyPrepared();
        EnsureGeneration(generation);
    }

    protected override void UpdateElementIndex(Control element, int oldIndex, int newIndex)
    {
        var cell = (TreeDataGridCell)element;
        if (_realized.GetValueOrDefault(oldIndex) == cell) _realized.Remove(oldIndex);
        cell.UpdateIndexes(RowIndex, newIndex);
        _realized[newIndex] = cell;
        if (_owned.TryGetValue(cell, out var lease)) _owned[cell] = lease with { ColumnIndex = newIndex };
    }

    protected override void UnrealizeElement(Control element)
    {
        var cell = (TreeDataGridCell)element;
        if (!_owned.Remove(cell, out var lease)) return;
        if (_realized.GetValueOrDefault(cell.ColumnIndex) == cell) _realized.Remove(cell.ColumnIndex);
        lease.BudgetOwner?.UnregisterCell(cell);
        _recycling[cell] = new(lease.Column, lease.BudgetOwner);
        var defer = !_resettingCells && (_deferRowRebind || IsInLayout);
        var success = false;
        try
        {
            if (defer) { _deferred.Add(cell); cell.BeginRebind(); }
            cell.Unrealize();
            success = true;
        }
        finally
        {
            cell.OwningRow = null;
            if (success && defer && lease.Value is not null && lease.Column is CellColumn { SupportsRetainedCellReuse: true } &&
                ReferenceEquals(lease.Presentation, Presentation))
                _retainedModels.Add(cell, lease);
            else ReleaseModel(lease, pool: success);
        }
    }

    protected override void UnrealizeElementOnItemRemoved(Control element) => UnrealizeElement(element);

    protected override void RecycleElementToFactory(Control element, TreeDataGridElementFactory? factory)
    {
        var cell = (TreeDataGridCell)element;
        if (!_recycling.Remove(cell, out var entry)) return;
        var reserved = entry.BudgetOwner is { } owner ? owner.ReservePooledCell() : _pooled.Count < 64;
        if (!reserved) { RemoveRecycledElement(cell); return; }
        _pooled.Add(cell, entry);
        if (!_pool.TryGetValue(entry.Column, out var pool)) _pool[entry.Column] = pool = new();
        pool.Push(cell);
    }

    protected override void RemoveRecycledElement(Control element)
    {
        if (element is not TreeDataGridCell cell) { base.RemoveRecycledElement(element); return; }
        _recycling.Remove(cell);
        if (_pooled.Remove(cell, out var entry))
        {
            entry.BudgetOwner?.ReleasePooledCell();
            if (_pool.TryGetValue(entry.Column, out var pool))
            {
                var remaining = pool.Where(candidate => !ReferenceEquals(candidate, cell)).Reverse().ToArray();
                if (remaining.Length == 0) _pool.Remove(entry.Column);
                else _pool[entry.Column] = new(remaining);
            }
        }
        try { FinalizeRecycledElement(cell); }
        finally { cell.Presenter = null; base.RemoveRecycledElement(cell); }
    }

    protected override void FinalizeRecycledElement(Control element)
    {
        var cell = (TreeDataGridCell)element;
        try { if (_deferred.Remove(cell)) cell.EndRebind(false); }
        finally { ReleaseRetainedModel(cell); }
    }

    protected override void TrimUnrealizedChildren()
    {
        // The row-level pool owns its reservations. It is already bounded by the
        // grid-wide budget (or 64 standalone cells), so the generic pool must not
        // independently detach its controls.
        if (_prunePool) PrunePooledColumns();
        if (!_deferRowRebind) FinalizeUnrealize();
    }

    protected override void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _prunePool = true;
        base.OnItemsCollectionChanged(sender, e);
        if (!IsInLayout) PrunePooledColumns();
    }

    private void PrunePooledColumns()
    {
        _prunePool = false;
        foreach (var pair in _pooled.ToArray())
            if (Items is null || !Items.Any(item => ReferenceEquals(item, pair.Value.Column)))
                RemoveRecycledElement(pair.Key);
    }

    internal void FinalizeUnrealize()
    {
        List<Exception>? errors = null;
        while (_deferred.Count > 0)
        {
            var iterator = _deferred.GetEnumerator();
            iterator.MoveNext();
            var cell = iterator.Current;
            iterator.Dispose();
            try { FinalizeRecycledElement(cell); }
            catch (Exception error) { (errors ??= new()).Add(error); }
        }
        ThrowErrors(errors);
    }

    private void ReleaseReservation(TreeDataGridCell cell)
    {
        if (_pooled.Remove(cell, out var entry)) entry.BudgetOwner?.ReleasePooledCell();
    }

    private CellValue? TakeRetainedModel(CellColumn column, TreeDataGridPresentation presentation, int index, int rowIndex)
    {
        if (!_pool.TryGetValue(column, out var pool) || !pool.TryPeek(out var cell) || !_retainedModels.Remove(cell, out var lease))
            return null;
        var reused = false;
        try
        {
            reused = ReferenceEquals(lease.Presentation, presentation) && ReferenceEquals(lease.Column, column) &&
                presentation.TryReuseCell(index, rowIndex, lease.Value!);
            return reused ? lease.Value : null;
        }
        finally { if (!reused) ReleaseModel(lease, pool: false); }
    }

    private void ReleaseRetainedModel(TreeDataGridCell cell)
    {
        if (_retainedModels.Remove(cell, out var lease)) ReleaseModel(lease, pool: true);
    }

    private static void ReleaseModel(CellLease lease, bool pool)
    {
        if (lease.Presentation is { } presentation && lease.Value is { } value && lease.Column is CellColumn column)
        {
            if (pool) presentation.RecycleCell(column, value);
            else value.Dispose();
        }
        else lease.Rows.UnrealizeCell(lease.Model, lease.ColumnIndex, lease.RowIndex);
    }

    private static void OnRowsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var presenter = (TreeDataGridCellsPresenter)sender;
        presenter.RetireLayout();
        presenter.ObserveRows();
    }

    private void ObserveRows()
    {
        if (_observedRows is { } previous) previous.CollectionChanged -= OnStandaloneRowsChanged;
        _observedRows = !_detached && Presenter is null && RowIndex >= 0 ? Rows : null;
        if (_observedRows is { } rows) rows.CollectionChanged += OnStandaloneRowsChanged;
    }

    private void OnStandaloneRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RetireLayout();

    protected override Rect? GetParentPresenterViewPort() => Presenter?.CellViewport ?? base.GetParentPresenterViewPort();
    protected override Rect GetMeasureViewport(Rect viewport) => Presenter?.CellViewport ?? base.GetMeasureViewport(viewport);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_resettingCells || RowIndex < 0 || _row?.IsResettingCells == true || Rows is null || RowIndex >= Rows.Count || Items is not IColumns) return default;
        // The native rows presenter constrains each row to the committed extent.
        // Natural-width measurement must not be capped by that previous extent,
        // or a wider Auto cell can never grow its column on first realization.
        var result = base.MeasureOverride(Presenter is not null ? new Size(double.PositiveInfinity, availableSize.Height) : availableSize);
        return Presenter is { } presenter ? new(presenter.Geometry.TotalWidth, Math.Max(presenter.RowEstimate, result.Height)) : result;
    }

    protected override Size MeasureElement(int index, Control element, Size availableSize)
    {
        var column = Items![index] as CellColumn;
        var previousWidth = column?.AutoWidth;
        var result = MeasureColumnElement(index, RowIndex, element, availableSize);
        if (Presenter is { } presenter && previousWidth != column?.AutoWidth) presenter.WidthsChanged = true;
        return result;
    }

    protected override Rect ArrangeElement(int index, Control element, Rect rect)
    {
        if (Presenter is { } presenter)
            rect = new(presenter.Geometry.Start(index), rect.Y, presenter.Geometry.Width(index), rect.Height);
        else if (Items is IColumns columns && double.IsFinite(columns[index].ActualWidth))
            rect = new(rect.X, rect.Y, Math.Max(0, columns[index].ActualWidth), rect.Height);
        return base.ArrangeElement(index, element, rect);
    }

    private static void ThrowErrors(List<Exception>? errors)
    {
        if (errors is { Count: 1 }) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException(errors);
    }
}
