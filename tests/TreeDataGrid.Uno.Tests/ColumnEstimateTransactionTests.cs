using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Uno.Controls.Models.TreeDataGrid;
using Windows.Foundation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

[Collection("ColumnLayoutReentrancy")]
public sealed class ColumnEstimateTransactionTests
{
    [Theory]
    [InlineData("width")]
    [InlineData("actual")]
    [InlineData("minimum")]
    public void A_getter_cannot_publish_the_extent_of_a_replaced_collection(string getter)
    {
        var original = new Column(getter == "minimum" ? new GridLength(1, GridUnitType.Star) : GridLength.Auto, 20, 5);
        var tail = new Column(GridLength.Auto, 30);
        var replacement = new Column(GridLength.Auto, 123);
        var columns = new ColumnListBase<Column> { original, tail };
        original.On(getter, () => { columns.Clear(); columns.Add(replacement); });
        try
        {
            Assert.Equal(123, columns.GetEstimatedWidth(double.PositiveInfinity));
            Assert.Same(replacement, Assert.Single(columns));
            Assert.Equal(0, tail.WidthReads);
            if (getter == "width") Assert.Equal(0, original.ActualReads);
        }
        finally { columns.Clear(); }
    }

    [Fact]
    public void Same_cardinality_replacement_and_nested_estimate_win_over_the_outer_query()
    {
        var first = new Column(GridLength.Auto, 20);
        var last = new Column(GridLength.Auto, 30);
        var replacement = new Column(GridLength.Auto, 70);
        var columns = new ColumnListBase<Column> { first, last };
        last.OnActual = () =>
        {
            columns[0] = replacement;
            Assert.Equal(100, columns.GetEstimatedWidth(500));
        };
        try { Assert.Equal(100, columns.GetEstimatedWidth(500)); }
        finally { columns.Clear(); }
    }

    [Fact]
    public void A_later_getter_cannot_leave_an_earlier_changed_actual_width_in_the_total()
    {
        var first = new Column(GridLength.Auto, 20);
        var last = new Column(GridLength.Auto, 30);
        var columns = new ColumnListBase<Column> { first, last };
        last.OnActual = () => first.SetActual(120);
        try { Assert.Equal(150, columns.GetEstimatedWidth(double.PositiveInfinity)); }
        finally { columns.Clear(); }
    }

    [Fact]
    public void Clearing_from_a_star_minimum_does_not_fabricate_a_viewport_sized_extent()
    {
        var star = new Column(new GridLength(1, GridUnitType.Star), 40, 10);
        var columns = new ColumnListBase<Column> { star };
        star.OnMinimum = columns.Clear;
        Assert.Equal(0, columns.GetEstimatedWidth(500));
        Assert.Empty(columns);
    }

    [Fact]
    public void Layout_policy_changes_without_actual_width_notifications_restart_the_query()
    {
        var first = new Column(GridLength.Auto, 40);
        var last = new Column(GridLength.Auto, 30);
        var columns = new ColumnListBase<Column> { first, last };
        ((IColumnLayoutBatch)columns).BeginActualWidthBatch();
        last.OnActual = () => columns.SetColumnWidth(0, new GridLength(1, GridUnitType.Star));
        try
        {
            Assert.Equal(500, columns.GetEstimatedWidth(500));
            Assert.True(first.Width.IsStar);
        }
        finally
        {
            ((IColumnLayoutBatch)columns).EndActualWidthBatch();
            columns.Clear();
        }
    }

    [Theory]
    [InlineData("width")]
    [InlineData("actual")]
    [InlineData("minimum")]
    public void Application_failures_propagate_unchanged_and_do_not_poison_later_queries(string getter)
    {
        var column = new Column(getter == "minimum" ? new GridLength(1, GridUnitType.Star) : GridLength.Auto, 40, 17);
        var columns = new ColumnListBase<Column> { column };
        var failure = new InvalidOperationException("Application width getter");
        column.On(getter, () => throw failure);
        try
        {
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => columns.GetEstimatedWidth(double.PositiveInfinity)));
            Assert.Equal(getter == "minimum" ? 17 : 40, columns.GetEstimatedWidth(double.PositiveInfinity));
        }
        finally { columns.Clear(); }
    }

    [Theory]
    [InlineData(500d, 500d)]
    [InlineData(10d, 100d)]
    [InlineData(double.PositiveInfinity, 100d)]
    [InlineData(double.NaN, 100d)]
    public void Stable_mixed_widths_preserve_reference_estimation_and_read_each_property_once(double constraint, double expected)
    {
        var measured = new Column(GridLength.Auto, 40);
        var unknown = new Column(GridLength.Auto, double.NaN);
        var star = new Column(new GridLength(2, GridUnitType.Star), 999, 10);
        var columns = new ColumnListBase<Column> { measured, unknown, star };
        try
        {
            Assert.Equal(expected, columns.GetEstimatedWidth(constraint));
            Assert.Equal(1, measured.WidthReads);
            Assert.Equal(1, measured.ActualReads);
            Assert.Equal(0, measured.MinimumReads);
            Assert.Equal(1, unknown.WidthReads);
            Assert.Equal(1, unknown.ActualReads);
            Assert.Equal(0, unknown.MinimumReads);
            Assert.Equal(1, star.WidthReads);
            Assert.Equal(0, star.ActualReads);
            Assert.Equal(1, star.MinimumReads);
        }
        finally { columns.Clear(); }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    [InlineData(1024)]
    public void Warm_estimation_allocates_no_managed_storage(int count)
    {
        var columns = new ColumnListBase<Column>();
        for (var index = 0; index < count; ++index) columns.Add(new Column(GridLength.Auto, 40));
        try
        {
            for (var iteration = 0; iteration < 1024; ++iteration) columns.GetEstimatedWidth(500);
            var sum = 0d;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var iteration = 0; iteration < 4096; ++iteration) sum += columns.GetEstimatedWidth(500);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0L, allocated);
            Assert.Equal(4096d * count * 40, sum);
        }
        finally { columns.Clear(); }
    }

    private sealed class Column(GridLength width, double actual, double minimum = 0) : IUpdateColumnLayout
    {
        private static readonly PropertyChangedEventArgs ActualChanged = new(nameof(ActualWidth));
        private GridLength _width = width;
        private double _actual = actual;
        internal Action? OnWidth, OnActual, OnMinimum;
        internal int WidthReads, ActualReads, MinimumReads;
        public GridLength Width { get { ++WidthReads; var value = _width; Invoke(ref OnWidth); return value; } }
        public double ActualWidth { get { ++ActualReads; var value = _actual; Invoke(ref OnActual); return value; } }
        public double MinActualWidth { get { ++MinimumReads; Invoke(ref OnMinimum); return minimum; } }
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public bool? CanUserResize => true;
        public object? Header => null;
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public double CellMeasured(double value, int rowIndex) => value;
        public bool CommitActualWidth() => false;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public void SetWidth(GridLength value) => _width = value;
        internal void SetActual(double value) { _actual = value; PropertyChanged?.Invoke(this, ActualChanged); }
        internal void On(string getter, Action callback)
        {
            switch (getter)
            {
                case "width": OnWidth = callback; break;
                case "actual": OnActual = callback; break;
                case "minimum": OnMinimum = callback; break;
                default: throw new ArgumentOutOfRangeException(nameof(getter));
            }
        }
        private static void Invoke(ref Action? callback)
        {
            var current = callback;
            callback = null;
            current?.Invoke();
        }
    }
}
