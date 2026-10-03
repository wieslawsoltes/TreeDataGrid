using System;
using System.Collections.Generic;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnWidthScratchTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(1000)]
    public void Warmed_width_calculation_and_geometry_commit_allocate_nothing(int count)
    {
        var columns = new CellColumn[count];
        for (var i = 0; i < count; ++i) columns[i] = new Probe(new GridLength(1, GridUnitType.Star));
        var output = new double[count];
        var geometry = new ColumnGeometry();
        for (var i = 0; i < 1024; ++i) { ColumnWidths.Calculate(columns, count * 100d, output); geometry.CommitSpan(output); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; ++i) { ColumnWidths.Calculate(columns, count * 100d, output); geometry.CommitSpan(output); }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(count, geometry.Count);
        Assert.Equal(count * 100d, geometry.TotalWidth, 7);
    }

    [Fact]
    public void Mixed_constraints_keep_the_reference_redistribution_result()
    {
        CellColumn[] columns = [new Probe(new(40)), new Probe(new(1, GridUnitType.Star), 100),
            new Probe(new(1, GridUnitType.Star), 0, 30), new Probe(new(1, GridUnitType.Star))];
        var output = new double[4];
        ColumnWidths.Calculate(columns, 300, output);
        Assert.Equal(new[] { 40d, 115d, 30d, 115d }, output);
        Assert.Equal(ColumnWidths.Calculate(columns, 300), output);
    }

    [Fact]
    public void Nested_constraint_calculations_do_not_share_live_scratch_storage()
    {
        CellColumn[] inner = [new Probe(new(1, GridUnitType.Star), 0, 25), new Probe(new(1, GridUnitType.Star))];
        var innerOutput = new double[2];
        var first = new Probe(new(1, GridUnitType.Star), 0, 100);
        CellColumn[] outer = [first, new Probe(new(1, GridUnitType.Star))];
        var output = new double[2];
        first.OnMinimum = () => ColumnWidths.Calculate(inner, 100, innerOutput);
        ColumnWidths.Calculate(outer, 300, output);
        Assert.Equal(new[] { 100d, 200d }, output);
        Assert.Equal(new[] { 25d, 75d }, innerOutput);
    }

    [Fact]
    public void Poisoned_pooled_flags_do_not_affect_a_later_calculation()
    {
        var columns = new CellColumn[300];
        for (var i = 0; i < columns.Length; ++i) columns[i] = new Probe(new(1, GridUnitType.Star));
        var output = new double[300];
        ColumnWidths.Calculate(columns, 30000, output);
        for (var i = 0; i < columns.Length; ++i) columns[i] = new Probe(new(20));
        ColumnWidths.Calculate(columns, 100, output);
        Assert.All(output, value => Assert.Equal(20d, value));
    }

    [Fact]
    public void Invalid_widths_leave_previous_span_geometry_untouched()
    {
        var geometry = new ColumnGeometry();
        geometry.CommitSpan(new[] { 10d, 0d, 20d });
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, -1d })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => geometry.CommitSpan(new[] { 3d, invalid }));
            Assert.Equal(3, geometry.Count);
            Assert.Equal(30d, geometry.TotalWidth);
            Assert.Equal(2, geometry.ColumnAt(10));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => geometry.CommitSpan(new[] { double.MaxValue, double.MaxValue }));
        Assert.Equal(30d, geometry.TotalWidth);
    }

    [Fact]
    public void Committed_geometry_does_not_retain_caller_owned_buffers()
    {
        var geometry = new ColumnGeometry();
        var values = new[] { 10d, 20d, 30d };
        geometry.CommitSpan(values);
        Array.Fill(values, 0d);
        Assert.Equal(60d, geometry.TotalWidth);
        Assert.Equal(10d, geometry.Start(1));
        Assert.Equal(30d, geometry.Start(2));
    }

    [Fact]
    public void Indexed_geometry_commit_has_no_interface_enumerator_allocation()
    {
        IReadOnlyList<double> values = new[] { 10d, 20d, 30d };
        var geometry = new ColumnGeometry();
        for (var i = 0; i < 1024; ++i) geometry.Commit(values);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) geometry.Commit(values);
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Incorrect_destination_length_is_rejected_before_writing()
    {
        CellColumn[] columns = [new Probe(new(20))];
        var output = new[] { -10d, -20d };
        Assert.Throws<ArgumentException>(() => ColumnWidths.Calculate(columns, 100, output));
        Assert.Equal(new[] { -10d, -20d }, output);
    }

    private sealed class Item;
    private sealed class Probe(GridLength width, double min = 0, double max = double.PositiveInfinity)
        : CellColumn(new TextColumn<Item, string>("Name", _ => "", width))
    {
        public Action? OnMinimum;
        public override double MinimumWidth { get { OnMinimum?.Invoke(); return min; } }
        public override double MaximumWidth => max;
        public override CellValue CreateCell(IRow row) => throw new NotSupportedException("Geometry-only fixture.");
    }
}
