using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;

namespace Uno.Controls.Models.TreeDataGrid;

public interface ICell
{
    bool CanEdit { get; }
    BeginEditGestures EditGestures { get; }
    object? Value { get; }
}

public interface ICellOptions { BeginEditGestures BeginEditGestures { get; } }
public interface ITemplateCellOptions : ICellOptions { }
public interface ITextCellOptions : ICellOptions
{
    string StringFormat { get; }
    CultureInfo Culture { get; }
    TextTrimming TextTrimming { get; }
    TextWrapping TextWrapping { get; }
    TextAlignment TextAlignment { get; }
}

public interface ITextCell : ICell
{
    string? Text { get; set; }
    TextTrimming TextTrimming { get; }
    TextWrapping TextWrapping { get; }
    TextAlignment TextAlignment { get; }
}

/// <summary>UI column metadata; the framework-neutral definition remains in Core.</summary>
public interface IColumn : INotifyPropertyChanged
{
    double ActualWidth { get; }
    bool? CanUserResize { get; }
    object? Header { get; }
    GridLength Width { get; }
    ListSortDirection? SortDirection { get; set; }
    object? Tag { get; set; }
}

public interface IUpdateColumnLayout : IColumn
{
    double MinActualWidth { get; }
    double MaxActualWidth { get; }
    bool StarWidthWasConstrained { get; }
    double CellMeasured(double width, int rowIndex);
    void CalculateStarWidth(double availableWidth, double totalStars);
    bool CommitActualWidth();
    void SetWidth(GridLength width);
}
