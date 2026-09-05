using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Controls.Presentation;
using Windows.Foundation;

namespace Uno.Controls.Primitives;

/// <summary>Horizontally virtualized headers using the same committed geometry as cells.</summary>
public partial class TreeDataGridColumnHeadersPresenter : Panel
{
    private readonly Dictionary<CellColumn, Button> _realized = new(ReferenceEqualityComparer.Instance);
    private readonly Stack<Button> _pool = new();
    private TreeDataGridPresentation? _presentation;
    private ColumnGeometry? _geometry;
    private double _offset;
    private double _viewport;
    internal TreeDataGrid? Owner { get; set; }
    public int RealizedCount => _realized.Count;

    internal void Update(TreeDataGridPresentation? presentation, ColumnGeometry geometry, double offset, double viewport)
    {
        if (!ReferenceEquals(_presentation, presentation))
        {
            foreach (var header in _realized.Values) header.Tag = null;
            _realized.Clear();
            _pool.Clear();
            Children.Clear();
        }
        _presentation = presentation;
        _geometry = geometry;
        _offset = offset;
        _viewport = viewport;
        InvalidateMeasure();
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        Size result;
        bool repeat;
        do { result = MeasureViewport(out repeat); } while (repeat);
        return result;
    }
    private Size MeasureViewport(out bool repeat)
    {
        repeat = false;
        if (_presentation is null || _geometry is null) return default;
        var (start, end) = _geometry.VisibleRange(_offset, _viewport);
        var desired = new HashSet<CellColumn>(ReferenceEqualityComparer.Instance);
        for (var i = start; i < end; ++i) desired.Add(_presentation.Columns[i]);
        foreach (var pair in _realized.ToArray())
        {
            if (desired.Contains(pair.Key)) continue;
            _realized.Remove(pair.Key);
            pair.Value.Tag = null;
            pair.Value.Content = null;
            pair.Value.Visibility = Visibility.Collapsed;
            if (_pool.Count < 32) _pool.Push(pair.Value);
            else Children.Remove(pair.Value);
        }
        var height = 32d;
        var widthsChanged = false;
        for (var i = start; i < end; ++i)
        {
            var column = _presentation.Columns[i];
            if (!_realized.TryGetValue(column, out var header))
            {
                if (!_pool.TryPop(out header))
                {
                    header = new Button { HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new(8, 4, 8, 4), MinWidth = 0 };
                    header.Click += OnHeaderClick;
                    Children.Add(header);
                }
                header.Tag = column;
                header.Visibility = Visibility.Visible;
                _realized.Add(column, header);
            }
            var suffix = column.Model.SortDirection switch
            {
                ListSortDirection.Ascending => " ▲",
                ListSortDirection.Descending => " ▼",
                _ => "",
            };
            header.Content = suffix.Length == 0 ? column.Model.Header : $"{column.Model.Header}{suffix}";
            if (column.RequiresUnconstrainedWidthMeasurement)
            {
                header.Measure(new(double.PositiveInfinity, double.PositiveInfinity));
                widthsChanged |= column.RecordWidth(header.DesiredSize.Width);
            }
            header.Measure(new(_geometry.Width(i), double.PositiveInfinity));
            height = Math.Max(height, header.DesiredSize.Height);
        }
        if (widthsChanged) repeat = Owner?.CommitColumnMeasurements() == true;
        return new(_geometry.TotalWidth, height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_presentation is null || _geometry is null) return finalSize;
        var (start, end) = _geometry.VisibleRange(_offset, _viewport);
        for (var i = start; i < end; ++i)
            if (_realized.TryGetValue(_presentation.Columns[i], out var header))
                header.Arrange(new(_geometry.Start(i), 0, _geometry.Width(i), finalSize.Height));
        return finalSize;
    }
    private void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CellColumn column })
            Owner?.Model?.SortBy(column.Model, column.Model.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending : ListSortDirection.Ascending);
    }
}
