using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Uno.Controls.Primitives;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    public static readonly DependencyProperty RowStyleProperty = DependencyProperty.Register(
        nameof(RowStyle), typeof(Style), typeof(TreeDataGrid), new PropertyMetadata(null, RowStyleChanged));
    public Style? RowStyle { get => (Style?)GetValue(RowStyleProperty); set => SetValue(RowStyleProperty, value); }
    public event EventHandler<TreeDataGridRowEventArgs>? RowPrepared;
    public event EventHandler<TreeDataGridRowEventArgs>? RowClearing;
    internal void RaiseRowPrepared(TreeDataGridRow row, int index) => RowPrepared?.Invoke(this, new(row, index));
    internal void RaiseRowClearing(TreeDataGridRow row, int index) => RowClearing?.Invoke(this, new(row, index));
    private static void RowStyleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var grid = (TreeDataGrid)sender;
        grid._presenter?.RefreshStyles();
        grid.InvalidateAppearanceMeasurements();
    }

    public TreeDataGridRow? TryGetRow(int rowIndex) => _presenter?.TryGetElement(rowIndex);
    public Control? TryGetCell(int columnIndex, int rowIndex) => TryGetRow(rowIndex)?.TryGetCell(columnIndex);
    public bool TryGetRow(DependencyObject? element, [NotNullWhen(true)] out TreeDataGridRow? result)
    {
        for (var current = element; current is not null && current != this; current = VisualTreeHelper.GetParent(current))
        {
            if (current is TreeDataGridRow { RowIndex: >= 0 } row && row.Presenter?.Owner == this)
            { result = row; return true; }
            // Never resolve a row through an embedded TreeDataGrid.
            if (current is TreeDataGrid) break;
        }
        result = null;
        return false;
    }
    public bool TryGetCell(DependencyObject? element, [NotNullWhen(true)] out TreeDataGridCell? result)
    {
        for (var current = element; current is not null && current != this; current = VisualTreeHelper.GetParent(current))
        {
            if (current is TreeDataGridCell { RowIndex: >= 0, ColumnIndex: >= 0 } cell && cell.Presenter?.Owner == this)
            { result = cell.OwningCell ?? cell; return true; }
            if (current is TreeDataGrid) break;
        }
        result = null;
        return false;
    }
    public bool TryGetRowModel<TModel>(DependencyObject element, [NotNullWhen(true)] out TModel? result) where TModel : notnull
    {
        if (TryGetRow(element, out var row) && row.Model is TModel model)
        { result = model; return true; }
        result = default;
        return false;
    }
}
