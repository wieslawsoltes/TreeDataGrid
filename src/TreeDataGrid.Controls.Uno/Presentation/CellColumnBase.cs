// Layout contract ported from Avalonia TreeDataGrid CellColumnBase<TModel> (MIT).
// Copyright (c) .NET Foundation and Contributors.
// This is view-owned geometry, not a second Core column or source implementation.
using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using Uno.Controls.Models.TreeDataGrid;
using ICell = Uno.Controls.Models.TreeDataGrid.ICell;
using IColumn = Uno.Controls.Models.TreeDataGrid.IColumn;

namespace Uno.Controls.Presentation;

/// <summary>
/// Base for custom view columns over shared Core rows. Native presenters adapt
/// this public layout contract through the same path as other ICellColumn types.
/// </summary>
public abstract class CellColumnBase<TModel> : NotifyingBase, ICellColumn<TModel>,
    global::Uno.Controls.Models.TreeDataGrid.IColumn<TModel>, IColumnMeasurementOptions
{
    private double _actualWidth = double.NaN;
    private GridLength _width;
    private double _autoWidth = double.NaN;
    private double _starWidth = double.NaN;
    private bool _starWidthWasConstrained;
    private object? _header;
    private ListSortDirection? _sortDirection;

    protected CellColumnBase(object? header, GridLength? width, CellColumnOptions options)
    {
        _header = header;
        Options = options;
        SetWidth(width ?? GridLength.Auto);
    }

    public double ActualWidth
    {
        get => _actualWidth;
        private set => RaiseAndSetIfChanged(ref _actualWidth, value);
    }
    public GridLength Width
    {
        get => _width;
        private set => RaiseAndSetIfChanged(ref _width, value);
    }
    public object? Header
    {
        get => _header;
        set => RaiseAndSetIfChanged(ref _header, value);
    }
    public CellColumnOptions Options { get; }
    public ListSortDirection? SortDirection
    {
        get => _sortDirection;
        set => RaiseAndSetIfChanged(ref _sortDirection, value);
    }
    public object? Tag { get; set; }

    bool? IColumn.CanUserResize => Options.CanUserResizeColumn;
    double IUpdateColumnLayout.MinActualWidth => CoerceActualWidth(0);
    double IUpdateColumnLayout.MaxActualWidth => CoerceActualWidth(double.PositiveInfinity);
    bool IUpdateColumnLayout.StarWidthWasConstrained => _starWidthWasConstrained;
    bool IColumnMeasurementOptions.RequiresUnconstrainedWidthMeasurement =>
        Width.IsAuto || Options.MinWidth.IsAuto || Options.MaxWidth?.IsAuto == true;

    public abstract ICell CreateCell(IRow<TModel> row);
    // Independent interfaces preserve this bridge when a subclass reimplements
    // only the legacy factory. Dispatch through its actual interface slot.
    ICell global::Uno.Controls.Models.TreeDataGrid.IColumn<TModel>.CreateCell(IRow<TModel> row) =>
        ((ICellColumn<TModel>)this).CreateCell(row);
    Comparison<TModel?>? global::Uno.Controls.Models.TreeDataGrid.IColumn<TModel>.GetComparison(
        ListSortDirection direction) => null;

    double IUpdateColumnLayout.CellMeasured(double width, int rowIndex)
    {
        _autoWidth = Math.Max(double.IsNaN(_autoWidth) ? 0 : _autoWidth, CoerceMeasuredWidth(width));
        if (Width.IsAuto) return _autoWidth;
        if (!double.IsNaN(ActualWidth)) return ActualWidth;
        return Width.IsStar ? ((IUpdateColumnLayout)this).MinActualWidth : _autoWidth;
    }

    void IUpdateColumnLayout.CalculateStarWidth(double availableWidth, double totalStars)
    {
        if (!Width.IsStar)
            throw new InvalidOperationException("Attempt to calculate star width on a non-star column.");
        // Preserve the reference operation order, including its IEEE-754 behavior.
        var proposed = (availableWidth / totalStars) * Width.Value;
        _starWidth = CoerceActualWidth(proposed);
        _starWidthWasConstrained = !AreClose(_starWidth, proposed);
    }

    bool IUpdateColumnLayout.CommitActualWidth()
    {
        var width = Width.GridUnitType switch
        {
            GridUnitType.Auto => double.IsNaN(_autoWidth) ? CoerceActualWidth(0) : _autoWidth,
            GridUnitType.Pixel => CoerceActualWidth(Width.Value),
            GridUnitType.Star => _starWidth,
            _ => throw new NotSupportedException(),
        };
        var previous = ActualWidth;
        ActualWidth = width;
        _starWidthWasConstrained = false;
        return !(double.IsNaN(previous) && double.IsNaN(width)) && !AreClose(previous, width);
    }

    void IUpdateColumnLayout.SetWidth(GridLength width) => SetWidth(width);

    private double CoerceActualWidth(double width)
    {
        width = Options.MinWidth.GridUnitType switch
        {
            GridUnitType.Auto => Math.Max(width, _autoWidth),
            GridUnitType.Pixel => Math.Max(width, Options.MinWidth.Value),
            GridUnitType.Star => throw new NotImplementedException("Star minimum width is not part of this layout contract."),
            _ => width,
        };
        return Options.MaxWidth?.GridUnitType switch
        {
            GridUnitType.Auto => Math.Min(width, _autoWidth),
            GridUnitType.Pixel => Math.Min(width, Options.MaxWidth.Value.Value),
            GridUnitType.Star => throw new NotImplementedException("Star maximum width is not part of this layout contract."),
            _ => width,
        };
    }

    private double CoerceMeasuredWidth(double width)
    {
        // Auto constraints derive from this measurement; consulting _autoWidth
        // here would form a circular NaN before the first measurement.
        width = Options.MinWidth.GridUnitType switch
        {
            GridUnitType.Auto => width,
            GridUnitType.Pixel => Math.Max(width, Options.MinWidth.Value),
            GridUnitType.Star => throw new NotImplementedException("Star minimum width is not part of this layout contract."),
            _ => width,
        };
        return Options.MaxWidth?.GridUnitType switch
        {
            GridUnitType.Auto => width,
            GridUnitType.Pixel => Math.Min(width, Options.MaxWidth.Value.Value),
            GridUnitType.Star => throw new NotImplementedException("Star maximum width is not part of this layout contract."),
            _ => width,
        };
    }

    private void SetWidth(GridLength width)
    {
        // Like the reference contract, fixed widths publish ActualWidth at once;
        // Auto and Star widths remain at their last committed layout value.
        _width = width;
        if (width.IsAbsolute) ActualWidth = width.Value;
    }

    private static bool AreClose(double first, double second)
    {
        if (first == second) return true;
        var epsilon = (Math.Abs(first) + Math.Abs(second) + 10) * 2.2204460492503131e-16;
        var delta = first - second;
        return -epsilon < delta && delta < epsilon;
    }
}
