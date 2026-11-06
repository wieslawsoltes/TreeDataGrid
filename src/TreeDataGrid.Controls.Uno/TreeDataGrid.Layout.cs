using System;
using Microsoft.UI.Xaml;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    public static readonly DependencyProperty RowHeightProperty = DependencyProperty.Register(
        nameof(RowHeight), typeof(double), typeof(TreeDataGrid), new PropertyMetadata(double.NaN, OnRowSizingChanged));
    public static readonly DependencyProperty MinRowHeightProperty = DependencyProperty.Register(
        nameof(MinRowHeight), typeof(double), typeof(TreeDataGrid), new PropertyMetadata(28d, OnRowSizingChanged));
    private double? _pendingVerticalAnchor;
    /// <summary>NaN measures each row's native content; a positive value fixes row height.</summary>
    public double RowHeight { get => (double)GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public double MinRowHeight { get => (double)GetValue(MinRowHeightProperty); set => SetValue(MinRowHeightProperty, value); }
    private static void OnRowSizingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var grid = (TreeDataGrid)sender;
        if ((!double.IsNaN(grid.RowHeight) && (!double.IsFinite(grid.RowHeight) || grid.RowHeight <= 0)) ||
            !double.IsFinite(grid.MinRowHeight) || grid.MinRowHeight <= 0)
        {
            grid.SetValue(e.Property, e.OldValue);
            throw new ArgumentOutOfRangeException(e.Property == RowHeightProperty ? nameof(RowHeight) : nameof(MinRowHeight));
        }
        grid._presenter?.ConfigureRows(grid.RowHeight, grid.MinRowHeight);
    }
    internal void QueueVerticalAnchor(double offset)
    {
        if (_scroll is null || Math.Abs((_pendingVerticalAnchor ?? _scroll.VerticalOffset) - offset) < 1e-7) return;
        _pendingVerticalAnchor = Math.Max(0, offset);
    }
    private void OnLayoutUpdated(object? sender, object e)
    {
        if (_pendingVerticalAnchor is not { } offset || _scroll is null) return;
        _pendingVerticalAnchor = null;
        var maximum = Math.Max(0, _scroll.ExtentHeight - _scroll.ViewportHeight);
        _scroll.ChangeView(null, Math.Min(offset, maximum), null, true);
        UpdateViewport();
    }
}
