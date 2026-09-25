using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Xunit;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnViewportSnapshotTests
{
    [Fact]
    public void Cold_anchor_and_mean_use_one_complete_snapshot()
    {
        var columns = Create(10, 20, double.NaN, 30);
        var estimate = 77d;
        Assert.Equal((1, 10d), Anchor(columns, 15, 16, ref estimate));
        Assert.Equal(77, estimate);
        Assert.Equal(20, ((U.IColumnViewportEstimator)columns).EstimateElementSize());
        Assert.All(columns, column => Assert.Equal(1, column.Reads));
        columns.Clear();
    }

    [Fact]
    public void Warm_exact_anchor_does_not_read_live_columns_again()
    {
        var columns = Create(10, 20, 30);
        Assert.Equal((0, 0d), columns.GetColumnAt(5));
        var estimate = 77d;
        Assert.Equal((2, 30d), Anchor(columns, 45, 46, ref estimate));
        Assert.Equal(77, estimate);
        Assert.All(columns, column => Assert.Equal(1, column.Reads));
        columns.Clear();
    }

    [Fact]
    public void Warm_fallback_shares_the_hit_test_snapshot()
    {
        var columns = Create(0, 20, 40);
        Assert.Equal((1, 0d), columns.GetColumnAt(5));
        var estimate = 77d;
        Assert.Equal((2, 60d), Anchor(columns, 1000, 1001, ref estimate));
        Assert.Equal(30, estimate);
        Assert.All(columns, column => Assert.Equal(1, column.Reads));
        columns.Clear();
    }

    [Fact]
    public void Replacement_during_a_getter_cannot_publish_a_retired_anchor()
    {
        var columns = Create(10, 20);
        var retired = columns[0];
        retired.OnRead = () =>
        {
            retired.OnRead = null;
            columns.Clear();
            columns.Add(new(100));
            columns.Add(new(200));
        };
        var estimate = 77d;
        Assert.Equal((0, 0d), Anchor(columns, 15, 16, ref estimate));
        Assert.Equal(0, retired.Subscribers);
        Assert.All(columns, column => Assert.Equal(1, column.Subscribers));
        Assert.Equal((0, 0d), columns.GetColumnAt(15));
        columns.Clear();
    }

    [Fact]
    public void Invalidating_an_earlier_width_retries_the_whole_snapshot()
    {
        var columns = Create(10, 20, 30);
        var later = columns[1];
        later.OnRead = () => { later.OnRead = null; columns[0].Change(100); };
        var estimate = 77d;
        Assert.Equal((0, 0d), Anchor(columns, 15, 16, ref estimate));
        Assert.Equal(50, ((U.IColumnViewportEstimator)columns).EstimateElementSize());
        Assert.Equal((1, 100d), columns.GetColumnAt(110));
        columns.Clear();
    }

    [Fact]
    public void Reentrant_clear_does_not_return_an_index_into_the_retired_collection()
    {
        var columns = Create(80, 80);
        var retired = columns[0];
        retired.OnRead = () => { retired.OnRead = null; columns.Clear(); };
        var estimate = 77d;
        Assert.Equal((-1, 0d), Anchor(columns, 1, 2, ref estimate));
        Assert.Equal(77, estimate);
        Assert.Empty(columns);
        Assert.Equal(0, retired.Subscribers);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(300)]
    public void A_nested_geometry_query_keeps_its_newer_snapshot(int count)
    {
        var columns = new U.ColumnListBase<ProbeColumn>();
        for (var i = 0; i < count; ++i) columns.Add(new(10));
        var retired = columns[0];
        retired.OnRead = () =>
        {
            retired.OnRead = null;
            columns.Clear();
            columns.Add(new(100));
            columns.Add(new(200));
            Assert.Equal((1, 100d), columns.GetColumnAt(150));
        };
        var estimate = 77d;
        Assert.Equal((0, 0d), Anchor(columns, 15, 16, ref estimate));
        Assert.Equal(150, ((U.IColumnViewportEstimator)columns).EstimateElementSize());
        Assert.All(columns, column => Assert.Equal(1, column.Reads));
        columns.Clear();
    }

    [Fact]
    public void Getter_failure_preserves_exception_identity_and_can_be_retried()
    {
        var columns = Create(10, 20, 30);
        var failing = columns[1];
        var error = new InvalidOperationException("width");
        failing.OnRead = () => throw error;
        var estimate = 77d;
        Assert.Same(error, Record.Exception(() => Anchor(columns, 15, 16, ref estimate)));
        Assert.Equal(77, estimate);
        failing.OnRead = null;
        Assert.Equal((1, 10d), Anchor(columns, 15, 16, ref estimate));
        Assert.Equal((2, 30d), columns.GetColumnAt(45));
        columns.Clear();
    }

    [Fact]
    public void Nonzero_origin_preserves_sequential_floating_point_addition()
    {
        // Subtracting adjacent cumulative ends would turn the final widths into
        // zero; translating cumulative ends would also lose both one-unit steps.
        var columns = Create(1e16, 1, 1);
        columns.GetColumnAt(1);
        var estimate = 77d;
        var estimator = (U.IColumnViewportEstimator)columns;
        Assert.Equal((1, 0d), estimator.GetOrEstimateColumnAt(.5, 1.5, 3, -1e16, 0, ref estimate));
        Assert.Equal((2, 1d), estimator.GetOrEstimateColumnAt(1.5, 2.5, 3, -1e16, 0, ref estimate));
        Assert.Equal(77, estimate);
        Assert.All(columns, column => Assert.Equal(1, column.Reads));
        columns.Clear();
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(double.NaN)]
    [InlineData(-1d)]
    public void Nonpositive_or_unknown_prefix_uses_all_positive_widths_for_its_mean(double first)
    {
        var columns = Create(first, 20, 40);
        var estimate = 77d;
        Assert.Equal((1, 30d), Anchor(columns, 35, 36, ref estimate));
        Assert.Equal(30, estimate);
        columns.Clear();
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_prior_estimates_cannot_produce_nonfinite_or_negative_anchors(double estimate)
    {
        var columns = Create(double.NaN, double.NaN, double.NaN, double.NaN);
        Assert.Equal((2, 50d), Anchor(columns, 51, 52, ref estimate));
        Assert.Equal(25, estimate);
        columns.Clear();
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(double.NegativeInfinity, 0)]
    [InlineData(-1000d, 0)]
    [InlineData(double.PositiveInfinity, 3)]
    [InlineData(double.MaxValue, 3)]
    public void Estimated_indexes_are_clamped_before_numeric_conversion(double start, int index)
    {
        var columns = Create(double.NaN, double.NaN, double.NaN, double.NaN);
        var estimate = 25d;
        Assert.Equal((index, index * 25d), Anchor(columns, start, start + 1, ref estimate));
        columns.Clear();
    }

    [Fact]
    public void Nonfinite_measured_mean_uses_a_finite_prior_estimate()
    {
        var columns = Create(0, double.PositiveInfinity, 20);
        var estimate = 7d;
        Assert.Equal((2, 14d), Anchor(columns, 100, 101, ref estimate));
        Assert.Equal(7, estimate);
        columns.Clear();
    }

    [Fact]
    public void Overflowing_estimated_position_is_saturated()
    {
        var columns = Create(double.NaN, double.NaN, double.NaN);
        var estimate = double.MaxValue;
        Assert.Equal((2, double.MaxValue), Anchor(columns, double.PositiveInfinity, double.PositiveInfinity, ref estimate));
        columns.Clear();
    }

    [Fact]
    public void Stale_or_shifted_input_counts_cannot_escape_current_column_bounds()
    {
        var columns = Create(10, 20, 30);
        var estimator = (U.IColumnViewportEstimator)columns;
        var estimate = 77d;
        Assert.Equal((2, 40d), estimator.GetOrEstimateColumnAt(10000, 10001, 100, 0, 0, ref estimate));
        estimate = 77;
        Assert.Equal((2, 40d), estimator.GetOrEstimateColumnAt(25, 26, 3, 0, 2, ref estimate));
        Assert.Equal((2, 40d), estimator.GetOrEstimateColumnAt(100, 101, 3, 0, int.MaxValue, ref estimate));
        columns.Clear();
        Assert.Equal((-1, 0d), estimator.GetOrEstimateColumnAt(0, 1, 3, 0, 0, ref estimate));
    }

    [Fact]
    public void Empty_and_origin_shortcuts_do_not_read_application_getters()
    {
        var columns = Create(10, 20);
        foreach (var column in columns) column.OnRead = () => throw new InvalidOperationException("unexpected getter");
        var estimate = 77d;
        Assert.Equal((-1, 0d), ((U.IColumnViewportEstimator)columns).GetOrEstimateColumnAt(15, 16, 0, 0, 0, ref estimate));
        Assert.Equal((0, 0d), Anchor(columns, 0, 1, ref estimate));
        Assert.Equal((0, 0d), Anchor(columns, 1e-16, 1, ref estimate));
        Assert.Equal(77, estimate);
        columns.Clear();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(300)]
    public void Warm_queries_allocate_no_storage_and_do_not_repeat_width_getters(int count)
    {
        var columns = new U.ColumnListBase<ProbeColumn>();
        for (var i = 0; i < count; ++i) columns.Add(new(10));
        var estimator = (U.IColumnViewportEstimator)columns;
        columns.GetColumnAt(1);
        var estimate = 25d;
        for (var i = 0; i < 1024; ++i)
            estimator.GetOrEstimateColumnAt(count * 10 - 1, count * 10, count, 0, 0, ref estimate);
        var before = GC.GetAllocatedBytesForCurrentThread();
        long sum = 0;
        for (var i = 0; i < 4096; ++i)
        {
            sum += estimator.GetOrEstimateColumnAt(count * 10 - 1, count * 10, count, 0, 0, ref estimate).index;
            sum += estimator.GetOrEstimateColumnAt(count * 10 + 1, count * 10 + 2, count, 0, 0, ref estimate).index;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(8192L * (count - 1), sum);
        Assert.All(columns, column => Assert.Equal(1, column.Reads));
        columns.Clear();
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(3.25d)]
    [InlineData(-7.5d)]
    public void Cached_lookup_matches_the_linear_reference_for_ordinary_inputs(double origin)
    {
        double[][] cases = [[10, 20, 30, 40], [10, 0, 30, 40], [10, double.NaN, 30, 40], [10, -1, 30, 40]];
        foreach (var widths in cases)
        {
            var columns = Create(widths);
            for (var start = 1d; start <= 150; start += .5)
            {
                var expectedEstimate = 25d;
                var expected = Reference(widths, start, start + 5, origin, ref expectedEstimate);
                var actualEstimate = 25d;
                var actual = ((U.IColumnViewportEstimator)columns).GetOrEstimateColumnAt(start, start + 5,
                    widths.Length, origin, 0, ref actualEstimate);
                Assert.Equal(expected, actual);
                Assert.Equal(expectedEstimate, actualEstimate);
            }
            columns.Clear();
        }
    }

    private static (int, double) Reference(double[] widths, double start, double end, double origin, ref double estimate)
    {
        var position = origin;
        for (var i = 0; i < widths.Length; ++i)
        {
            var width = widths[i];
            if (double.IsNaN(width) || width <= 0) break;
            var next = position + width;
            if (next > start && position < end) return (i, position);
            position = next;
        }
        var total = 0d;
        var measured = 0;
        foreach (var width in widths)
            if (width > 0) { total += width; ++measured; }
        if (measured > 0) estimate = total / measured;
        var index = Math.Min((int)(start / estimate), widths.Length - 1);
        return (index, index * estimate);
    }

    private static U.ColumnListBase<ProbeColumn> Create(params double[] widths)
    {
        var result = new U.ColumnListBase<ProbeColumn>();
        foreach (var width in widths) result.Add(new(width));
        return result;
    }

    private static (int, double) Anchor(U.ColumnListBase<ProbeColumn> columns, double start, double end, ref double estimate) =>
        ((U.IColumnViewportEstimator)columns).GetOrEstimateColumnAt(start, end, columns.Count, 0, 0, ref estimate);

    private sealed class ProbeColumn(double width) : U.IUpdateColumnLayout
    {
        private PropertyChangedEventHandler? _changed;
        private double _actual = width;
        public Action? OnRead;
        public int Reads, Subscribers;
        public double ActualWidth { get { ++Reads; var value = _actual; OnRead?.Invoke(); return value; } }
        public bool? CanUserResize => true;
        public object? Header => null;
        public GridLength Width { get; private set; } = new(80);
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public double CellMeasured(double measured, int rowIndex) => measured;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(GridLength value) => Width = value;
        public void Change(double value) { _actual = value; _changed?.Invoke(this, new(nameof(ActualWidth))); }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
}
