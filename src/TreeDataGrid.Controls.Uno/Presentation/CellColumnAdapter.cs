using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using UICell = Uno.Controls.Models.TreeDataGrid.ICell;

namespace Uno.Controls.Presentation;

using UI = global::Uno.Controls.Models.TreeDataGrid;

/// <summary>Adapts a public column implementation without copying its Core source.</summary>
internal sealed partial class CellColumnAdapter<TModel> : CellColumn where TModel : class
{
    private readonly ICellColumn<TModel> _inner;
    private bool _disposed;
    private bool _initializing = true;
    private bool _observingInner;
    public CellColumnAdapter(IColumn model, ICellColumn<TModel> inner) : base(model)
    {
        _inner = inner;
        Exception? failure = null;
        try
        {
            _inner.SetWidth(Width);
            _inner.SortDirection = model.SortDirection;
            _observingInner = true;
            _inner.PropertyChanged += OnInnerChanged;
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
        catch (Exception error) { failure = error; _disposed = true; throw; }
        finally
        {
            _initializing = false;
            if (_disposed)
            {
                var detach = _observingInner;
                _observingInner = false;
                // Failed construction leaves the custom column with its factory.
                try { ReleaseAdapterModel(_inner, false, OnInnerChanged, detach); }
                catch (Exception cleanup) when (failure is not null)
                { throw new AggregateException(failure, cleanup); }
            }
        }
    }
    public override object? Header { get => _inner.Header; set => throw new NotSupportedException("The custom column owns its header."); }
    public override bool? CanUserResize => _inner.CanUserResize;
    public override double MinimumWidth => GetConstraint(maximum: false);
    public override double MaximumWidth => GetConstraint(maximum: true);
    private double GetConstraint(bool maximum)
    {
        var value = maximum ? _inner.MaxActualWidth : _inner.MinActualWidth;
        if (!double.IsNaN(value) || HasWidthMeasurement || _inner is not CellColumnBase<TModel> column)
            return value;
        // The reference custom-column base reports NaN for unmeasured Auto
        // constraints. Give native layout a discovery interval, without faking
        // a measurement or changing that public contract. Only this known base
        // receives the bridge; invalid arbitrary custom constraints still fail.
        var minimum = column.Options.MinWidth.IsAuto ? 0 : column.Options.MinWidth.Value;
        var limit = column.Options.MaxWidth is { IsAuto: false } bound ? bound.Value : double.PositiveInfinity;
        return Math.Min(limit, Math.Max(minimum, maximum ? double.PositiveInfinity : 0));
    }
    // Match Avalonia's opt-in measurement contract. Unannotated custom
    // columns retain conservative natural measurement for Auto constraints.
    public override bool RequiresUnconstrainedWidthMeasurement =>
        (_inner as UI.IColumnMeasurementOptions)?.RequiresUnconstrainedWidthMeasurement ?? true;
    public override CellValue CreateCell(IRow row)
    {
        var cell = Adapt(CreateOwnedCell(row), true, null);
        try { ObjectDisposedException.ThrowIf(_disposed, this); return cell; }
        catch (Exception error)
        {
            try { cell.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }
    // Public view-row realization transfers the actual UI model to its caller.
    // Do not create a native adapter that would need another ownership registry.
    internal override UICell CreateCellModel(IRow row) => CreateOwnedCell(row);
    private UICell CreateOwnedCell(IRow row)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var cell = _inner.CreateCell((IRow<TModel>)row) ?? throw new InvalidOperationException("The column returned no cell.");
        try { ObjectDisposedException.ThrowIf(_disposed, this); return cell; }
        catch (Exception error)
        {
            try { (cell as IDisposable)?.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }
    internal static CellValue Adapt(UICell cell, bool ownsModel, HashSet<UICell>? ancestors = null)
    {
        if (cell is CellValue native) return native;
        CellValue result;
        try
        {
            if (cell is UI.IExpanderCellPresentation expander)
            {
                ancestors ??= new(ReferenceEqualityComparer.Instance);
                if (!ancestors.Add(cell)) throw new InvalidOperationException("An expander cell cannot contain itself or an ancestor.");
                try { result = new CustomExpanderValue(expander, ownsModel, ancestors); }
                finally { ancestors.Remove(cell); }
            }
            else result = new CustomCellValue(cell, ownsModel);
        }
        catch (Exception error)
        {
            try { if (ownsModel) (cell as IDisposable)?.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
        // Construction failures above belong to the factory. Ownership has now
        // transferred, so failure while retiring a completed adapter must not
        // make that factory dispose the same raw model for a second time.
        if (result is CustomCellValue leaf) leaf.CompleteConstruction();
        else if (result is CustomExpanderValue parent) parent.CompleteConstruction();
        return result;
    }
    // Third-party columns select each row's actual cell model/kind. Do not
    // overwrite it with this adapter's default column kind.
    internal override void ConfigureCell(CellValue value) { }
    internal override bool SupportsRetainedCellReuse => true;
    internal override bool TryReuseCell(CellValue value, IRow row)
    {
        if (_disposed) return false;
        // Even rejected or throwing reuse can partially mutate a custom cell.
        // Supersede old writes before entering that application callback.
        if (value is CustomCellValue customCell) customCell.InvalidateWrite();
        else if (value is CustomExpanderValue customExpander) customExpander.InvalidateWrite();
        if (!_inner.TryReuseCell(value.PresentationModel, (IRow<TModel>)row) || _disposed) return false;
        if (value is CustomCellValue cell) cell.RefreshAfterRetarget();
        if (value is CustomExpanderValue expander) expander.RefreshAfterRetarget();
        return !_disposed;
    }
    internal override bool RecordWidth(double width, int rowIndex = -1)
    {
        _inner.CellMeasured(width, rowIndex);
        return base.RecordWidth(width, rowIndex);
    }
    internal override bool SetActualWidth(double width)
    {
        if (Width.IsStar) _inner.CalculateStarWidth(width, Width.Value > 0 ? Width.Value : 1);
        _inner.CommitActualWidth();
        return base.SetActualWidth(width);
    }
    internal override void ModelChanged(PropertyChangedEventArgs e)
    {
        _inner.SetWidth(Width);
        _inner.SortDirection = Model.SortDirection;
        base.ModelChanged(e);
    }
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_initializing) return;
        var detach = _observingInner;
        _observingInner = false;
        ReleaseAdapterModel(_inner, true, OnInnerChanged, detach);
    }
    private void OnInnerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_disposed) RaisePropertyChanged(e);
    }
}
