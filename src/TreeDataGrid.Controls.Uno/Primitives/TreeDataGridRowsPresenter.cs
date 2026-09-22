using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using Windows.Foundation;
using IRow = TreeDataGridCore.Models.IRow;

namespace Uno.Controls.Primitives;

/// <summary>Viewport rows with configurable buffering and bounded, parented recycling.</summary>
public partial class TreeDataGridRowsPresenter : TreeDataGridPresenterBase<IRow>
{
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(IColumns), typeof(TreeDataGridRowsPresenter), new PropertyMetadata(null, ColumnsChanged));
    public static readonly DependencyProperty CacheLengthProperty = DependencyProperty.Register(
        nameof(CacheLength), typeof(double), typeof(TreeDataGridRowsPresenter), new PropertyMetadata(0d, CacheLengthChanged));
    private Rect? _measureViewport;
    private double _measureViewportHeight;
    /// <summary>Additional realization before and after the viewport, in viewport heights (0–2).</summary>
    public double CacheLength { get => (double)GetValue(CacheLengthProperty); set => SetValue(CacheLengthProperty, value); }
    private static void CacheLengthChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var presenter = (TreeDataGridRowsPresenter)sender;
        if ((double)e.NewValue is not (>= 0 and <= 2))
        {
            presenter.SetValue(CacheLengthProperty, e.OldValue);
            throw new ArgumentOutOfRangeException(nameof(CacheLength), "Cache length must be between zero and two viewport heights.");
        }
        presenter.InvalidateMeasureViewport();
        presenter.InvalidateMeasure();
    }
    private readonly Dictionary<int, TreeDataGridRow> _realized = new();
    private readonly List<TreeDataGridRow> _pool = new(32);
    private readonly HashSet<TreeDataGridCell> _cells = new();
    private TreeDataGridPresentation? _presentation;
    private ColumnGeometry? _geometry;
    private double _horizontalOffset;
    private double _verticalOffset;
    private double _viewportWidth;
    private double _viewportHeight;
    private int _pooledCells;
    private int _revision;
    private int _resetDepth;
    private bool _settingPresentation;
    private bool _detached;
    private bool _measuringRows;
    private bool _pendingColumnLayoutInvalidation;
    private bool _heightsChanged;
    private IColumns? _observedColumns;
    private TreeDataGridElementFactory? _ownerFactory;
    private readonly ColumnGeometry _standaloneGeometry = new();
    private double[] _standaloneWidths = [];
    public TreeDataGridRowsPresenter()
    {
        Loaded += (_, _) =>
        {
            _detached = false;
            _rows.Reset(Items?.Count ?? 0, RowEstimate);
            InvalidateMeasureViewport();
            ObserveColumns();
        };
        Unloaded += (_, _) => { _detached = true; ObserveColumns(); };
    }
    public IColumns? Columns { get => (IColumns?)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    protected override Orientation Orientation => Orientation.Vertical;
    protected override bool OwnsRecyclingPool => true;
    internal int Revision => _revision;
    internal TreeDataGrid? Owner { get; set; }
    internal ColumnGeometry Geometry => _geometry ?? _standaloneGeometry;
    internal (int First, int End) VisibleColumns => Geometry.VisibleRange(_horizontalOffset, _viewportWidth);
    internal Rect CellViewport => new(_horizontalOffset, 0, _viewportWidth, RowEstimate);
    internal bool WidthsChanged { get; set; }
    public IReadOnlyCollection<TreeDataGridCell> RealizedCells => _cells;
    public IReadOnlyCollection<TreeDataGridRow> RealizedRows => _realized.Values;
    public override TreeDataGridRow? TryGetElement(int index) => _realized.GetValueOrDefault(index);
    protected override Control? GetRealizedElement(int index) => TryGetElement(index);
    public override IEnumerable<Control> GetRealizedElements() => _realized.OrderBy(pair => pair.Key).Select(pair => (Control)pair.Value);
    internal void ReleaseFocusRetention()
    {
        InvalidateMeasureViewport();
        foreach (var row in _realized.Values) row.CellsPresenter?.InvalidateMeasure();
        InvalidateMeasure();
    }
    internal void RegisterCell(TreeDataGridCell cell) => _cells.Add(cell);
    internal void UnregisterCell(TreeDataGridCell cell) => _cells.Remove(cell);
    internal bool ReservePooledCell()
    {
        if (_pooledCells >= 256) return false;
        ++_pooledCells;
        return true;
    }
    internal void ReleasePooledCell() => --_pooledCells;
    internal void ResetRowCells(TreeDataGridRow row)
    {
        // A public row factory change invalidates only this row's containers.
        // Advance the shared revision before callbacks so in-flight realization
        // cannot publish a cell from the previous factory.
        ++_revision;
        if (row.IsResettingCells) return;
        row.IsResettingCells = true;
        var realization = row.RealizationVersion;
        var cells = row.CellsPresenter;
        try
        {
            if (Owner?.EditingCell is { } editing && editing.RowIndex == row.RowIndex) Owner.CancelEdit();
            if (Current()) cells?.Reset();
        }
        finally
        {
            row.IsResettingCells = false;
            // A nested factory assignment is coalesced into this reset. A source
            // replacement or recycled row, however, owns a different generation.
            if (Current()) cells?.Attach(row);
            InvalidateRowMeasurements();
            InvalidateMeasure();
        }
        bool Current() => realization == row.RealizationVersion && ReferenceEquals(row.Presenter, this) &&
            ReferenceEquals(row.CellsPresenter, cells);
    }
    internal void RefreshSelection()
    {
        var revision = _revision;
        var generation = PresenterGeneration;
        foreach (var row in _realized.Values)
        {
            row.UpdateSelection();
            if (revision != _revision || generation != PresenterGeneration) return;
        }
    }
    internal void UpdateSelection(TreeDataGridCell cell)
    {
        var generation = PresenterGeneration;
        var realization = cell.RealizationVersion;
        var interaction = _presentation?.SelectionInteraction;
        var rowSelected = interaction?.IsRowSelected(cell.RowIndex) == true;
        if (generation != PresenterGeneration || realization != cell.RealizationVersion) return;
        var cellSelected = interaction?.IsCellSelected(cell.ColumnIndex, cell.RowIndex) == true;
        if (generation != PresenterGeneration || realization != cell.RealizationVersion) return;
        cell.IsSelected = rowSelected || cellSelected;
        if (generation != PresenterGeneration || realization != cell.RealizationVersion) return;
        cell.IsRowSelected = rowSelected;
        if (generation != PresenterGeneration || realization != cell.RealizationVersion) return;
        cell.IsCurrent = Owner?.IsCurrentCell(cell.RowIndex, cell.ColumnIndex) == true;
    }
    internal void RefreshStyles()
    {
        foreach (var row in _realized.Values) row.Style = Owner?.RowStyle;
        foreach (var row in _pool) row.Style = Owner?.RowStyle;
        InvalidateRowMeasurements();
        InvalidateMeasure();
    }
    internal void SetPresentation(TreeDataGridPresentation? presentation, ColumnGeometry geometry)
    {
        if (ReferenceEquals(_presentation, presentation) && ReferenceEquals(Items, presentation?.Rows) && ReferenceEquals(Columns, presentation?.Columns))
        {
            ++_revision;
            _geometry = geometry;
            foreach (var row in _realized.Values) row.CellsPresenter?.SynchronizeColumns();
            foreach (var row in _pool) row.CellsPresenter?.SynchronizeColumns();
            InvalidateRowMeasurements();
            InvalidateMeasure();
            return;
        }
        var revision = Reset();
        if (revision != _revision) return;
        _presentation = presentation;
        _geometry = geometry;
        var previous = _settingPresentation;
        _settingPresentation = true;
        try
        {
            if (ElementFactory is null || ReferenceEquals(ElementFactory, _ownerFactory))
                ElementFactory = _ownerFactory = Owner?.ElementFactory ?? new();
            if (revision != _revision) return;
            Columns = presentation?.Columns;
            if (revision != _revision) return;
            Items = presentation?.Rows;
        }
        finally { _settingPresentation = previous; }
        InvalidateMeasure();
    }
    internal void UpdateViewport(double horizontal, double vertical, double width, double height)
    {
        var columnsChanged = horizontal != _horizontalOffset || width != _viewportWidth;
        var viewport = new Rect(horizontal, vertical, width, height);
        var needsMeasure = columnsChanged || _measureViewport is not { } measured ||
            height != _measureViewportHeight || NeedsMeasureForViewportChange(measured, viewport);
        _horizontalOffset = horizontal;
        _verticalOffset = vertical;
        _viewportWidth = width;
        _viewportHeight = height;
        if (!needsMeasure) return;
        // A vertical viewport change changes the realized row range, not the
        // horizontal constraint of retained cells. Let native measure caching
        // keep those rows valid. New/rebound rows invalidate themselves, and
        // content/column/font changes have their own invalidation paths.
        // Horizontal changes still require an explicit child measure: a row's
        // full content extent is unchanged while its visible columns change.
        if (columnsChanged)
            foreach (var row in _realized.Values) row.CellsPresenter?.InvalidateMeasure();
        InvalidateMeasure();
    }
    protected override Rect GetMeasureViewport(Rect viewport)
    {
        if (Owner is not null) viewport = new(_horizontalOffset, _verticalOffset, _viewportWidth, _viewportHeight);
        else
        {
            _horizontalOffset = viewport.X;
            _verticalOffset = viewport.Y;
            _viewportWidth = viewport.Width;
            _viewportHeight = viewport.Height;
        }
        if (CacheLength > 0 && _measureViewport is { } cached && _measureViewportHeight == viewport.Height &&
            !NeedsMeasureForViewportChange(cached, viewport)) return cached;
        var range = RowViewport.Calculate(viewport.Top, viewport.Height, _rows.TotalHeight, CacheLength);
        _measureViewport = new(viewport.X, range.Start, viewport.Width, range.End - range.Start);
        _measureViewportHeight = viewport.Height;
        return _measureViewport.Value;
    }
    protected override bool NeedsMeasureForViewportChange(Rect measureViewport, Rect viewport)
    {
        if (measureViewport.Width != viewport.Width) return true;
        if (CacheLength > 0) return viewport.Top < measureViewport.Top ||
            Math.Min(_rows.TotalHeight, viewport.Bottom) > measureViewport.Bottom;
        var (first, end) = new RowViewport(Math.Max(0, viewport.Top), Math.Min(_rows.TotalHeight, viewport.Bottom)).GetRows(_rows);
        return end > first && (!_realized.ContainsKey(first) || !_realized.ContainsKey(end - 1));
    }
    private void InvalidateMeasureViewport() => _measureViewport = null;
    protected override void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var configuring = _settingPresentation && sender is null && ReferenceEquals(Items, _presentation?.Rows);
        if (!configuring) ++_revision;
        if (sender is null && !ReferenceEquals(Items, _presentation?.Rows))
        {
            _presentation = null;
            _geometry = null;
        }
        var revision = _revision;
        UpdateRowGeometry(e);
        base.OnItemsCollectionChanged(sender, e);
        if (revision != _revision || IsInLayout) return;
        // Replacement/sort scopes finish when the next layout rebinds retained
        // rows. Closing them here sends EndRebind(false) before reuse and clears
        // retained template state. Do not eagerly realize rows inside Reset:
        // a source may still be completing its items/selection transaction.
        // Source detachment, empty collections and actual removals must release
        // ownership now; surplus recycled rows are finalized after measurement.
        if (sender is null || Items is null || Items.Count == 0 ||
            e.Action is not (NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset))
            FinalizeUnrealize();
        RefreshSelection();
        foreach (var row in _realized.Values) row.NotifyAutomationStateChanged();
    }

    protected override void OnItemsChanged(IReadOnlyList<IRow>? oldItems, IReadOnlyList<IRow>? newItems)
    {
        base.OnItemsChanged(oldItems, newItems);
        // Source replacement is not ordinary viewport recycling: a collapsed
        // row must not keep the previous view/source alive through its cells.
        // Keep the row parented, but release its old cell/model ownership.
        ++_resetDepth;
        try
        {
            foreach (var row in _pool.ToArray())
                if (row.RowIndex < 0 && !ReferenceEquals(row.Rows, Items)) row.Release();
        }
        finally { --_resetDepth; }
    }

    protected override Control GetElementFromFactory(IRow item, int index)
    {
        var generation = PresenterGeneration;
        var factory = ElementFactory ?? throw new InvalidOperationException("Rows require an element factory.");
        while (_pool.Count > 0)
        {
            // Keep a retained display slot attached to its original native row
            // across sorting/replacement. Otherwise LIFO reverses the viewport
            // and moves template/focus state to a different visible row.
            // The pool is capped at 32; this scan allocates nothing and does not
            // enumerate source rows or maintain another source-sized index.
            var pooledIndex = _pool.Count - 1;
            for (var i = pooledIndex; i >= 0; --i)
                if (_pool[i].RecycledRowIndex == index && ReferenceEquals(_pool[i].Rows, Items))
                { pooledIndex = i; break; }
            var candidate = _pool[pooledIndex];
            _pool.RemoveAt(pooledIndex);
            var reusable = false;
            try
            {
                var compatible = factory.CanReuseElement(candidate, item);
                EnsureGeneration(generation);
                reusable = compatible;
            }
            finally { if (!reusable) RemoveRecycledElement(candidate); }
            if (reusable) return candidate;
        }
        var created = factory.GetOrCreateElement(item, this);
        if (created is not TreeDataGridRow row || row.RowIndex >= 0 ||
            (row.Parent is not null && !ReferenceEquals(row.Parent, this)))
            throw new InvalidOperationException("The row factory must return an unrealized row owned by this presenter or no parent.");
        try { EnsureGeneration(generation); return row; }
        catch { RemoveRecycledElement(row); throw; }
    }

    protected override void RealizeElement(Control element, IRow item, int index)
    {
        var row = (TreeDataGridRow)element;
        var revision = _revision;
        var generation = PresenterGeneration;
        if (Owner is not null) row.Style = Owner.RowStyle;
        EnsureGeneration(generation);
        _realized.Add(index, row);
        if (_presentation is { } presentation && ReferenceEquals(Items, presentation.Rows) && ReferenceEquals(Columns, presentation.Columns))
            row.Realize(this, presentation, index);
        else
            row.Realize(this, ElementFactory, null, Columns, Items as ITreeDataGridRows, index);
        // Column/factory callbacks can retire an in-flight row without changing
        // Items. Do not publish a partly initialized container into the range.
        if ((revision != _revision || row.RowIndex != index) && generation == PresenterGeneration) RetireLayout();
        EnsureGeneration(generation);
    }

    protected override void UpdateElementIndex(Control element, int oldIndex, int newIndex)
    {
        var row = (TreeDataGridRow)element;
        if (_realized.GetValueOrDefault(oldIndex) == row) _realized.Remove(oldIndex);
        _realized[newIndex] = row;
        row.UpdateIndex(newIndex);
    }

    protected override void UnrealizeElement(Control element) => UnrealizeRow((TreeDataGridRow)element, TreeDataGridRowUnrealizeReason.Recycle);
    protected override void UnrealizeElementOnItemRemoved(Control element) => UnrealizeRow((TreeDataGridRow)element, TreeDataGridRowUnrealizeReason.ItemRemoved);
    private void UnrealizeRow(TreeDataGridRow row, TreeDataGridRowUnrealizeReason reason)
    {
        row.RecycledRowIndex = row.RowIndex;
        if (_realized.GetValueOrDefault(row.RowIndex) == row) _realized.Remove(row.RowIndex);
        else
        {
            // An application hook may have called row.Unrealize directly.
            // Remove the unpublished/retired map entry even after its index was
            // cleared; the normal recycling path uses the constant-time lookup.
            foreach (var pair in _realized)
                if (ReferenceEquals(pair.Value, row)) { _realized.Remove(pair.Key); break; }
        }
        row.Unrealize(reason);
    }

    protected override void RecycleElementToFactory(Control element, TreeDataGridElementFactory? factory)
    {
        if (_resetDepth != 0)
        {
            RemoveRecycledElement(element);
            return;
        }
        var row = (TreeDataGridRow)element;
        var generation = PresenterGeneration;
        var retained = false;
        try
        {
            if (!ReferenceEquals(row.Rows, Items)) row.Release();
            if (generation != PresenterGeneration) return;
            // A previous, larger viewport can fill the pool before CacheLength
            // shrinks. Discarding the incoming row then destroys the most recent
            // viewport's template tree. Evict the oldest pooled row instead;
            // the 32-row and 256-cell limits remain unchanged.
            if (_pool.Count == 32) RemoveRecycledElement(_pool[0]);
            // Releasing the victim invokes native/application cleanup callbacks.
            // A nested source/factory reset must not admit this retired row.
            if (generation == PresenterGeneration && _resetDepth == 0 && _pool.Count < 32)
            {
                _pool.Add(row);
                retained = true;
            }
        }
        finally { if (!retained) RemoveRecycledElement(row); }
    }

    protected override void RemoveRecycledElement(Control element)
    {
        if (element is TreeDataGridRow row)
        {
            _pool.Remove(row);
            try { row.Release(); }
            finally { base.RemoveRecycledElement(row); }
        }
        else base.RemoveRecycledElement(element);
    }

    protected override void FinalizeRecycledElement(Control element) => ((TreeDataGridRow)element).CellsPresenter?.FinalizeUnrealize();
    protected override void TrimUnrealizedChildren() => FinalizeUnrealize();
    internal int Reset()
    {
        var revision = ++_revision;
        var pooled = _pool.ToArray();
        // Detach bookkeeping first. Clearing handlers may synchronously install
        // and lay out a new source; its rows must not join this reset's cleanup.
        _realized.Clear();
        _pool.Clear();
        _cells.Clear();
        // Reservations are released by their owning presenters, including a
        // row currently inside a reentrant RowClearing callback.
        _presentation = null;
        _geometry = null;
        InvalidateMeasureViewport();
        _rows.Reset(0, RowEstimate);
        _anchor = null;
        _verticalOffset = 0;
        List<Exception>? errors = null;
        var previous = _settingPresentation;
        _settingPresentation = true;
        ++_resetDepth;
        try
        {
            try { Items = null; }
            catch (Exception error) { (errors ??= new()).Add(error); }
            // Clear all active/in-flight rows through the shared lifetime owner.
            // Nested RowClearing callbacks may already have installed new Items;
            // that configuration must survive this cleanup.
            try { RetireLayout(); }
            catch (Exception error) { (errors ??= new()).Add(error); }
            if (revision == _revision)
            {
                try { Columns = null; }
                catch (Exception error) { (errors ??= new()).Add(error); }
            }
            foreach (var row in pooled) Release(row);
        }
        finally { --_resetDepth; _settingPresentation = previous; }
        if (errors is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException(errors);
        return revision;

        void Release(TreeDataGridRow row)
        {
            try { row.Release(); }
            catch (Exception error) { (errors ??= new()).Add(error); }
            finally { Children.Remove(row); }
        }
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        if (_resetDepth > 0 || _measuringRows) return DesiredSize;
        _measuringRows = true;
        try
        {
            var gatherNaturalWidths = true;
            var measuredWidth = Geometry.TotalWidth;
            bool repeat;
            do
            {
                var revision = _revision;
                var generation = PresenterGeneration;
                var passAnchor = CaptureAnchor();
                _heightsChanged = WidthsChanged = false;
                _pendingColumnLayoutInvalidation = false;
                Columns?.ViewportChanged(GetViewportForMeasure(availableSize));
                Columns?.CommitActualWidths();
                CommitStandaloneGeometry();
                var batch = gatherNaturalWidths ? Columns as IColumnLayoutBatch : null;
                var needsFinalMeasure = false;
                batch?.BeginActualWidthBatch();
                try { measuredWidth = base.MeasureOverride(availableSize).Width; }
                finally { needsFinalMeasure = batch?.EndActualWidthBatch() ?? false; }
                // As in Avalonia, only the gathering pass defers width commits.
                // The final pass must measure cells with the committed widths.
                gatherNaturalWidths = false;
                if (generation != PresenterGeneration || revision != _revision)
                {
                    InvalidateMeasure();
                    return default;
                }
                var widthsChanged = Owner is not null ? WidthsChanged && Owner.CommitColumnMeasurements() : CommitStandaloneGeometry();
                if (generation != PresenterGeneration || revision != _revision)
                {
                    InvalidateMeasure();
                    return default;
                }
                repeat = needsFinalMeasure || widthsChanged || _pendingColumnLayoutInvalidation || _heightsChanged;
                if (_heightsChanged) { InvalidateMeasureViewport(); RestoreAnchor(passAnchor); }
                if (needsFinalMeasure || widthsChanged || _pendingColumnLayoutInvalidation)
                    foreach (var row in _realized.Values) row.CellsPresenter?.InvalidateMeasure();
            } while (repeat);
            if (_anchor is { } anchor && (uint)anchor.Row < (uint)_rows.Count)
            {
                _verticalOffset = _rows.Start(anchor.Row) + Math.Min(anchor.Offset, Math.Max(0, _rows.Height(anchor.Row) - 0.001));
                Owner?.QueueVerticalAnchor(_verticalOffset);
            }
            _anchor = null;
            return new(Math.Max(Geometry.TotalWidth, measuredWidth), _rows.TotalHeight);
        }
        finally { _measuringRows = false; FinalizeUnrealize(); }
    }
    protected override Size MeasureElement(int index, Control element, Size availableSize)
    {
        // Columns already own the cells' finite widths. Measure the row's full
        // content extent so a theme border participates in layout instead of
        // clipping the last column inside the previously committed extent.
        MeasureNative(element, new(double.PositiveInfinity, AutoRowHeight ? double.PositiveInfinity : RowHeight));
        EnsureCurrentLayout();
        var (firstColumn, endColumn) = VisibleColumns;
        if (AutoRowHeight)
        {
            var height = Math.Max(RowEstimate, element.DesiredSize.Height);
            var complete = firstColumn == 0 && endColumn == Geometry.Count;
            _heightsChanged |= _rows.SetHeight(index, complete ? height : Math.Max(_rows.Height(index), height));
        }
        return new(Math.Max(Geometry.TotalWidth, element.DesiredSize.Width), _rows.Height(index));
    }

    protected override (int index, double position) GetElementAt(double position)
    {
        var index = _rows.RowAt(position);
        return index < _rows.Count ? (index, _rows.Start(index)) : (-1, -1);
    }
    protected override double GetElementPosition(int index) => _rows.Start(index);
    protected override double CalculateSizeU(Size availableSize) => _rows.TotalHeight;
    protected override double EstimateElementSizeU() => RowEstimate;
    protected override (int index, double position) GetOrEstimateAnchorElementForViewport(double viewportStart, double viewportEnd, int itemCount)
    {
        var index = Math.Min(itemCount - 1, _rows.RowAt(viewportStart));
        // Re-measure an invalidated tall anchor before using its estimated end.
        if (_anchor is { } pending && _viewportHeight > 0) index = Math.Min(index, pending.Row);
        return (index, _rows.Start(index));
    }
    protected override Rect? GetParentPresenterViewPort() => Owner is not null
        ? new(_horizontalOffset, _verticalOffset, _viewportWidth, _viewportHeight) : base.GetParentPresenterViewPort();

    protected override void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        base.OnEffectiveViewportChanged(sender, e);
        if (Owner is null && double.IsFinite(Viewport.X) && double.IsFinite(Viewport.Y))
        {
            // The generic vertical stack invalidates for vertical changes.
            // A standalone row also has horizontally virtualized cells.
            UpdateViewport(Viewport.X, Viewport.Y, Viewport.Width, Viewport.Height);
            Columns?.ViewportChanged(Viewport);
        }
    }

    protected override Rect ArrangeElement(int index, Control element, Rect rect) =>
        base.ArrangeElement(index, element, new(0, _rows.Start(index), Math.Max(rect.Width, Geometry.TotalWidth), _rows.Height(index)));
    protected override Size ArrangeOverride(Size finalSize)
    {
        Columns?.CommitActualWidths();
        CommitStandaloneGeometry();
        return base.ArrangeOverride(finalSize);
    }

    private bool CommitStandaloneGeometry()
    {
        if (_geometry is not null) return false;
        var count = Columns?.Count ?? 0;
        if (_standaloneWidths.Length != count) _standaloneWidths = new double[count];
        for (var i = 0; i < count; ++i)
            _standaloneWidths[i] = double.IsFinite(Columns![i].ActualWidth) ? Math.Max(0, Columns[i].ActualWidth) : 0;
        return _standaloneGeometry.Commit(_standaloneWidths);
    }

    private static void ColumnsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var presenter = (TreeDataGridRowsPresenter)sender;
        var configuring = presenter._settingPresentation && ReferenceEquals(presenter.Columns, presenter._presentation?.Columns);
        if (!configuring) ++presenter._revision;
        presenter.ObserveColumns();
        if (configuring) return;
        if (!ReferenceEquals(presenter.Columns, presenter._presentation?.Columns)) presenter._geometry = null;
        // Changing the column collection changes each row's cells. Width-only
        // notifications below keep the existing row and cell containers alive.
        presenter.RetireLayout();
        presenter.InvalidateRowMeasurements();
        presenter.InvalidateMeasure();
    }
    private void ObserveColumns()
    {
        if (_observedColumns is not null) _observedColumns.LayoutInvalidated -= OnColumnLayoutInvalidated;
        _observedColumns = _detached ? null : Columns;
        if (_observedColumns is not null) _observedColumns.LayoutInvalidated += OnColumnLayoutInvalidated;
    }
    private void OnColumnLayoutInvalidated(object? sender, EventArgs e)
    {
        if (_measuringRows) _pendingColumnLayoutInvalidation = true;
        else
        {
            foreach (var row in _realized.Values) row.CellsPresenter?.InvalidateMeasure();
            InvalidateRowMeasurements();
            InvalidateMeasure();
        }
    }
    private void FinalizeUnrealize()
    {
        var revision = _revision;
        var generation = PresenterGeneration;
        foreach (var row in _realized.Values)
        {
            row.CellsPresenter?.FinalizeUnrealize();
            if (revision != _revision || generation != PresenterGeneration) return;
        }
        foreach (var row in _pool)
        {
            row.CellsPresenter?.FinalizeUnrealize();
            if (revision != _revision || generation != PresenterGeneration) return;
        }
    }
}
