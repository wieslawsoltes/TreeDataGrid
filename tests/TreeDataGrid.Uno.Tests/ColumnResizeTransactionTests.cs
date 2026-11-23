using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnResizeTransactionTests
{
    [Theory]
    [InlineData(10, 0, 300, 110)]
    [InlineData(-200, 20, 300, 20)]
    [InlineData(10, 200, 150, 150)]
    [InlineData(1000, 20, 160, 160)]
    public void Pixel_resizing_preserves_constraint_precedence(double delta, double minimum, double maximum, double expected)
    {
        var column = new Column { Minimum = minimum, Maximum = maximum };
        var columns = new Columns { column };
        Assert.True(Resize(columns, column, new State(), delta));
        Assert.Equal(expected, column.Width.Value);
        Assert.Equal(1, columns.Writes);
        columns.Clear();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Auto_and_star_resizing_start_at_the_native_arranged_width(bool star)
    {
        var column = new Column();
        column.SetWidth(star ? new GridLength(2, GridUnitType.Star) : GridLength.Auto);
        var columns = new Columns { column };
        Assert.True(ColumnResizeTransaction.Resize(columns, column, 0, 80, 12, new Guard(new State())));
        Assert.Equal(new GridLength(92), column.Width);
        columns.Clear();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Retirement_after_each_getter_prevents_later_callbacks_and_the_write(int stage)
    {
        var column = new Column();
        var columns = new Columns { column };
        var state = new State();
        column.Callbacks[stage] = () => state.Active = false;
        Assert.False(Resize(columns, column, state, 10));
        Assert.Equal(0, columns.Writes);
        for (var index = stage + 1; index < 3; ++index) Assert.Equal(0, column.Reads[index]);
        columns.Clear();
    }

    [Fact]
    public void Reactivation_of_the_same_owner_still_invalidates_the_old_operation()
    {
        var column = new Column();
        var columns = new Columns { column };
        var state = new State();
        column.Callbacks[0] = () => { state.Active = false; ++state.Revision; state.Active = true; };
        Assert.False(Resize(columns, column, state, 10));
        Assert.Equal(0, columns.Writes);
        columns.Clear();
    }

    [Fact]
    public void Equal_but_different_column_at_the_same_index_cannot_receive_the_old_resize()
    {
        var column = new Column();
        var replacement = new Column();
        var columns = new Columns { column };
        column.Callbacks[0] = () => columns[0] = replacement;
        Assert.False(Resize(columns, column, new State(), 10));
        Assert.Equal(0, columns.Writes);
        Assert.Equal(100, replacement.Width.Value);
        columns.Clear();
    }

    [Fact]
    public void Removing_the_target_during_a_getter_cannot_write_out_of_range()
    {
        var column = new Column();
        var columns = new Columns { column };
        column.Callbacks[0] = () => columns.Clear();
        Assert.False(Resize(columns, column, new State(), 10));
        Assert.Equal(0, columns.Writes);
    }

    [Fact]
    public void Newer_nested_resize_wins_without_an_obsolete_second_commit()
    {
        var column = new Column();
        var columns = new Columns { column };
        var state = new State();
        column.Callbacks[0] = () =>
        {
            ++state.Revision;
            Assert.True(Resize(columns, column, state, 30));
        };
        Assert.False(Resize(columns, column, state, 10));
        Assert.Equal(130, column.Width.Value);
        Assert.Equal(1, columns.Writes);
        columns.Clear();
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Nonfinite_delta_does_not_evaluate_application_getters(double delta)
    {
        var column = new Column();
        var columns = new Columns { column };
        Assert.False(Resize(columns, column, new State(), delta));
        Assert.Equal(new[] { 0, 0, 0 }, column.Reads);
        Assert.Equal(0, columns.Writes);
        columns.Clear();
    }

    [Fact]
    public void Overflowing_extent_does_not_reach_constraint_getters_or_commit()
    {
        var column = new Column();
        column.SetWidth(new GridLength(double.MaxValue));
        var columns = new Columns { column };
        Assert.False(Resize(columns, column, new State(), double.MaxValue));
        Assert.Equal(new[] { 1, 0, 0 }, column.Reads);
        Assert.Equal(0, columns.Writes);
        columns.Clear();
    }

    [Fact]
    public void Throwing_getter_preserves_exception_identity_without_committing()
    {
        var column = new Column();
        var columns = new Columns { column };
        var failure = new InvalidOperationException("Application maximum");
        column.Callbacks[2] = () => throw failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => Resize(columns, column, new State(), 10)));
        Assert.Equal(0, columns.Writes);
        Assert.True(Resize(columns, column, new State(), 10));
        columns.Clear();
    }

    [Fact]
    public void Reset_to_auto_uses_the_same_identity_and_lifetime_boundary()
    {
        var column = new Column();
        var columns = new Columns { column };
        var state = new State();
        var guard = new Guard(state);
        ++state.Revision;
        Assert.False(ColumnResizeTransaction.SetWidth(columns, column, 0, GridLength.Auto, guard));
        Assert.Equal(0, columns.Writes);
        Assert.True(ColumnResizeTransaction.SetWidth(columns, column, 0, GridLength.Auto, new Guard(state)));
        Assert.True(column.Width.IsAuto);
        columns.Clear();
    }

    [Fact]
    public void Warm_guarded_resize_has_no_boxing_or_transaction_allocation()
    {
        var column = new Column();
        var columns = new Columns { column };
        var state = new State();
        for (var i = 0; i < 1024; ++i) Resize(columns, column, state, (i & 1) == 0 ? 1 : -1);
        var successes = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) if (Resize(columns, column, state, (i & 1) == 0 ? 1 : -1)) ++successes;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(4096, successes);
        Assert.Equal(100, column.Width.Value);
        columns.Clear();
    }

    private static bool Resize(Columns columns, Column column, State state, double delta) =>
        ColumnResizeTransaction.Resize(columns, column, 0, 90, delta, new Guard(state));

    private sealed class State { internal int Revision; internal bool Active = true; }
    private readonly struct Guard : IColumnResizeGuard
    {
        private readonly State _state;
        private readonly int _revision;
        internal Guard(State state) { _state = state; _revision = state.Revision; }
        public bool IsCurrent => _state.Active && _revision == _state.Revision;
    }
    private sealed class Columns : ColumnListBase<Column>, IColumns
    {
        internal int Writes;
        void IColumns.SetColumnWidth(int index, GridLength width) { ++Writes; this[index].SetWidth(width); }
    }
    private sealed class Column : IUpdateColumnLayout
    {
        private GridLength _width = new(100);
        internal readonly Action?[] Callbacks = new Action?[3];
        internal readonly int[] Reads = new int[3];
        internal double Minimum, Maximum = double.PositiveInfinity;
        public GridLength Width { get { var value = _width; Read(0); return value; } }
        public double MinActualWidth { get { var value = Minimum; Read(1); return value; } }
        public double MaxActualWidth { get { var value = Maximum; Read(2); return value; } }
        public double ActualWidth => _width.Value;
        public bool StarWidthWasConstrained => false;
        public bool? CanUserResize => true;
        public object? Header => null;
        public object? Tag { get; set; }
        public ListSortDirection? SortDirection { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public double CellMeasured(double width, int rowIndex) => width;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(GridLength width) => _width = width;
        public override bool Equals(object? obj) => obj is Column;
        public override int GetHashCode() => 0;
        private void Read(int index) { ++Reads[index]; var callback = Callbacks[index]; Callbacks[index] = null; callback?.Invoke(); }
    }
}
