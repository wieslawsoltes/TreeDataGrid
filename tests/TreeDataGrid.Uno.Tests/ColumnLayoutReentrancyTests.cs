using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using Windows.Foundation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

[Collection("ColumnLayoutReentrancy")]
public sealed class ColumnLayoutReentrancyTests
{
    [Fact]
    public void Cell_measurement_can_clear_the_collection()
    {
        var column = new Column();
        var columns = new ColumnListBase<Column> { column };
        column.OnMeasure = () => columns.Clear();
        Assert.Equal(new Size(80, 20), columns.CellMeasured(0, 0, new Size(80, 20)));
        Assert.Empty(columns);
        Assert.Equal((-1, -1d), columns.GetColumnAt(0));
    }

    [Fact]
    public void Constraint_getter_can_clear_during_measurement()
    {
        var column = new Column();
        var columns = new ColumnListBase<Column> { column };
        columns.CellMeasured(0, 0, new Size(80, 20));
        columns.CommitActualWidths();
        var maximumReads = column.MaximumReads;
        column.OnMinimum = () => columns.Clear();
        columns.CellMeasured(0, 0, new Size(80, 20));
        Assert.Equal(maximumReads, column.MaximumReads);
        Assert.Empty(columns);
        columns.CommitActualWidths();
        Assert.Equal((-1, -1d), columns.GetColumnAt(0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_width_commit_remains_retryable(bool afterMutation)
    {
        var column = new Column(new GridLength(100));
        var columns = new ColumnListBase<Column> { column };
        var failure = new InvalidOperationException("Custom commit");
        column.OnCommit = () => { if (afterMutation) column.Actual = 45; throw failure; };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => columns.CommitActualWidths()));
        column.OnCommit = null;
        columns.CommitActualWidths();
        Assert.Equal(100, column.ActualWidth);
        Assert.Equal((0, 0d), columns.GetColumnAt(99));
        columns.Clear();
    }

    [Fact]
    public void Commit_cannot_publish_constraints_into_a_reentrant_replacement()
    {
        var original = new Column(new GridLength(100));
        var replacement = new Column(new GridLength(200));
        var columns = new ColumnListBase<Column> { original };
        original.OnMinimum = () => { columns[0] = replacement; columns.CommitActualWidths(); };
        columns.CommitActualWidths();
        Assert.Same(replacement, columns[0]);
        Assert.Equal(200, replacement.ActualWidth);
        Assert.Equal((0, 0d), columns.GetColumnAt(199));
        columns.Clear();
    }

    [Fact]
    public void Constraint_capture_can_clear_during_commit()
    {
        var column = new Column(new GridLength(100));
        var columns = new ColumnListBase<Column> { column };
        column.OnMinimum = () => columns.Clear();
        columns.CommitActualWidths();
        Assert.Empty(columns);
        Assert.Equal((-1, -1d), columns.GetColumnAt(0));
    }

    [Fact]
    public void Width_getter_mutation_does_not_commit_a_removed_column()
    {
        var original = new Column(new GridLength(100));
        var replacement = new Column(new GridLength(200));
        var columns = new ColumnListBase<Column> { original };
        original.OnWidth = () => { columns[0] = replacement; columns.CommitActualWidths(); };
        columns.CommitActualWidths();
        Assert.Equal(0, original.Commits);
        Assert.Equal(200, replacement.ActualWidth);
        columns.Clear();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(129)]
    [InlineData(1024)]
    public void Reentrant_geometry_lookup_cannot_append_old_widths_to_new_geometry(int count)
    {
        var original = new Column(new GridLength(100)) { Actual = 100 };
        var replacement = new Column(new GridLength(200)) { Actual = 200 };
        var columns = new ColumnListBase<Column>();
        for (var i = 0; i < count - 1; ++i) columns.Add(new Column { Actual = 60 });
        columns.Add(original);
        var lastStart = (count - 1) * 60d;
        original.OnActual = () =>
        {
            columns[count - 1] = replacement;
            Assert.Equal((count - 1, lastStart), columns.GetColumnAt(lastStart + 199));
        };
        // The outer call must use the current coherent geometry, never the
        // inner geometry with an old prefix appended after returning.
        Assert.Equal((count - 1, lastStart), columns.GetColumnAt(lastStart + 199));
        Assert.Equal((-1, -1d), columns.GetColumnAt(lastStart + 200));
        columns.Clear();
    }

    [Fact]
    public void Failed_geometry_read_leaves_a_retryable_cache()
    {
        var column = new Column { Actual = 100 };
        var columns = new ColumnListBase<Column> { column };
        var failure = new InvalidOperationException("Custom width getter");
        column.OnActual = () => throw failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => columns.GetColumnAt(50)));
        Assert.Equal((0, 0d), columns.GetColumnAt(50));
        columns.Clear();
    }

    [Fact]
    public void Width_assignment_cannot_write_a_column_retired_by_its_getter()
    {
        var original = new Column(new GridLength(100));
        var replacement = new Column(new GridLength(200));
        var columns = new ColumnListBase<Column> { original };
        original.OnWidth = () => { columns[0] = replacement; columns.CommitActualWidths(); };
        columns.SetColumnWidth(0, new GridLength(150));
        Assert.Equal(0, original.Assignments);
        Assert.Equal(200, replacement.ActualWidth);
        columns.Clear();
    }

    [Fact]
    public void Native_constraint_capture_can_clear_without_indexing_retired_storage()
    {
        var column = new Column(new GridLength(100));
        var columns = new ColumnListBase<Column> { column };
        column.OnMinimum = () => columns.Clear();
        columns.AcceptNativeWidths(300);
        Assert.Empty(columns);
        columns.CommitActualWidths();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(1024)]
    public void Warm_layout_and_geometry_queries_allocate_no_managed_storage(int count)
    {
        var column = new Column(new GridLength(100));
        var columns = new ColumnListBase<Column> { column };
        for (var i = 1; i < count; ++i) columns.Add(new Column(new GridLength(100)));
        columns.CommitActualWidths();
        for (var i = 0; i < 1024; ++i)
        {
            column.InvalidateActualWidth();
            columns.GetColumnAt(50);
        }
        var hits = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i)
        {
            columns.CommitActualWidths();
            column.InvalidateActualWidth();
            if (columns.GetColumnAt(50).index == 0) ++hits;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(4096, hits);
        Assert.Equal(0L, allocated);
        columns.Clear();
    }

    [Theory]
    [InlineData(50)]
    [InlineData(300)]
    public void Star_calculation_cannot_commit_an_older_reentrant_viewport(double viewport)
    {
        var column = new Column(new GridLength(1, GridUnitType.Star));
        var columns = new ColumnListBase<Column> { column };
        columns.ViewportChanged(new Rect(0, 0, 100, 20));
        column.OnStar = () =>
        {
            columns.ViewportChanged(new Rect(0, 0, viewport, 20));
            columns.CommitActualWidths();
        };
        columns.CommitActualWidths();
        Assert.Equal(viewport, column.ActualWidth);
        columns.CommitActualWidths();
        Assert.Equal(viewport, column.ActualWidth);
        columns.Clear();
    }

    [Fact]
    public void Native_constraint_failure_leaves_the_layout_retryable()
    {
        var column = new Column(new GridLength(100));
        var columns = new ColumnListBase<Column> { column };
        var failure = new InvalidOperationException("Native constraint");
        column.OnMinimum = () => throw failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => columns.AcceptNativeWidths(300)));
        columns.CommitActualWidths();
        Assert.Equal(100, column.ActualWidth);
        columns.Clear();
    }

    [Fact]
    public void Throwing_outer_measurement_does_not_dirty_a_newer_completed_commit()
    {
        var column = new Column(new GridLength(100));
        var columns = new ColumnListBase<Column> { column };
        columns.CommitActualWidths();
        var failure = new InvalidOperationException("Outer measurement");
        column.OnMeasure = () =>
        {
            columns.SetColumnWidth(0, new GridLength(200));
            throw failure;
        };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => columns.CellMeasured(0, 0, new Size(80, 20))));
        var commits = column.Commits;
        columns.CommitActualWidths();
        Assert.Equal(commits, column.Commits);
        Assert.Equal(200, column.ActualWidth);
        columns.Clear();
    }

    [Fact]
    public void Nested_unchanged_measurement_does_not_mark_an_aborted_commit_clean()
    {
        var first = new Column(new GridLength(100));
        var second = new Column(new GridLength(200));
        var columns = new ColumnListBase<Column> { first, second };
        columns.CommitActualWidths();
        first.OnCommit = () => columns.CellMeasured(0, 0, new Size(100, 20));
        columns.SetColumnWidth(1, new GridLength(300));
        columns.CommitActualWidths();
        Assert.Equal(300, second.ActualWidth);
        columns.Clear();
    }

    private sealed class Column : IUpdateColumnLayout
    {
        private GridLength _width;
        internal Column(GridLength? width = null) => _width = width ?? GridLength.Auto;
        internal Action? OnMeasure, OnCommit, OnMinimum, OnWidth, OnActual, OnStar;
        internal double Actual = double.NaN;
        private double _measured, _star;
        internal int Commits, Assignments, MaximumReads;
        public double ActualWidth { get { var value = Actual; InvokeOnce(ref OnActual); return value; } }
        public GridLength Width { get { var value = _width; InvokeOnce(ref OnWidth); return value; } }
        public double MinActualWidth { get { InvokeOnce(ref OnMinimum); return 0; } }
        public double MaxActualWidth { get { ++MaximumReads; return double.PositiveInfinity; } }
        public bool StarWidthWasConstrained => false;
        public bool? CanUserResize => true;
        public object? Header => null;
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public double CellMeasured(double width, int rowIndex)
        {
            _measured = Math.Max(_measured, width);
            InvokeOnce(ref OnMeasure);
            return _measured;
        }
        public void CalculateStarWidth(double availableWidth, double totalStars)
        {
            InvokeOnce(ref OnStar);
            _star = availableWidth / totalStars * _width.Value;
        }
        private static readonly PropertyChangedEventArgs ActualWidthChanged = new(nameof(ActualWidth));
        internal void InvalidateActualWidth() => PropertyChanged?.Invoke(this, ActualWidthChanged);
        public bool CommitActualWidth()
        {
            ++Commits;
            InvokeOnce(ref OnCommit);
            var width = _width.IsAuto ? _measured : _width.IsStar ? _star : _width.Value;
            var changed = !Actual.Equals(width);
            Actual = width;
            if (changed) PropertyChanged?.Invoke(this, new(nameof(ActualWidth)));
            return changed;
        }
        public void SetWidth(GridLength width) { ++Assignments; _width = width; }
        private static void InvokeOnce(ref Action? callback)
        {
            var current = callback;
            callback = null;
            current?.Invoke();
        }
    }
}

[CollectionDefinition("ColumnLayoutReentrancy", DisableParallelization = true)]
public sealed class ColumnLayoutReentrancyCollection { }
