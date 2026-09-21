using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Presentation;

public abstract partial class CellColumn : IUpdateColumnLayout
{
    private double _actualWidth = double.NaN;
    private double _starWidth = double.NaN;
    private bool _starWidthWasConstrained;
    private object? _header;
    private bool _hasHeader;
    public virtual double ActualWidth => _actualWidth;
    public virtual object? Header
    {
        get => _hasHeader ? _header : Model.Header;
        set { _hasHeader = true; RaiseAndSetIfChanged(ref _header, value); }
    }
    public virtual GridLength Width => new(Model.Width.Value, (GridUnitType)Model.Width.GridUnitType);
    public virtual ListSortDirection? SortDirection { get => Model.SortDirection; set => Model.SortDirection = value; }
    public virtual object? Tag { get => Model.Tag; set => Model.Tag = value; }
    public virtual double MinActualWidth => ConstrainWidth(0);
    public virtual double MaxActualWidth => ConstrainWidth(double.PositiveInfinity);
    public bool StarWidthWasConstrained => _starWidthWasConstrained;
    public virtual double CellMeasured(double width, int rowIndex)
    {
        RecordWidth(width, rowIndex);
        return Width.IsAuto ? ConstrainWidth(AutoWidth) : double.IsNaN(ActualWidth) ? MinActualWidth : ActualWidth;
    }
    public virtual void CalculateStarWidth(double availableWidth, double totalStars)
    {
        if (!Width.IsStar) throw new InvalidOperationException("The column is not star-sized.");
        if (!double.IsFinite(availableWidth) || availableWidth < 0 || !double.IsFinite(totalStars) || totalStars <= 0)
            throw new ArgumentOutOfRangeException(nameof(availableWidth));
        var proposed = availableWidth * (Width.Value / totalStars);
        _starWidth = ConstrainWidth(proposed);
        _starWidthWasConstrained = Math.Abs(_starWidth - proposed) > 0.000001;
    }
    public virtual bool CommitActualWidth()
    {
        var width = Width.IsAuto ? ConstrainWidth(AutoWidth) : Width.IsStar ? _starWidth : ConstrainWidth(Width.Value);
        var changed = SetActualWidth(width);
        _starWidthWasConstrained = false;
        return changed;
    }
    public virtual void SetWidth(GridLength width) =>
        Model.Width = new TreeDataGridCore.GridLength(width.Value, (TreeDataGridCore.GridUnitType)width.GridUnitType);
    internal virtual bool SetActualWidth(double width)
    {
        if (_actualWidth.Equals(width)) return false;
        _actualWidth = width;
        RaisePropertyChanged(nameof(ActualWidth));
        return true;
    }
    internal virtual void ModelChanged(PropertyChangedEventArgs e) => RaisePropertyChanged(e);
    private double ConstrainWidth(double width) => Math.Min(MaximumWidth, Math.Max(MinimumWidth, width));
}
