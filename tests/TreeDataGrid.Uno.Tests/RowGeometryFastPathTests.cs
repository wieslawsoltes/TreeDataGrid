using System;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class RowGeometryFastPathTests
{
    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0 / 3)]
    [InlineData(28.1)]
    [InlineData(1e-200)]
    [InlineData(1e200)]
    public void Uniform_lookup_respects_exact_starts_and_adjacent_double_values(double estimate)
    {
        var rows = new RowGeometry();
        rows.Reset(int.MaxValue, estimate);
        foreach (var index in new[] { 1, 2, 7, 127, 4095, 1_000_000, int.MaxValue - 1 })
        {
            var start = rows.Start(index);
            Assert.Equal(index, rows.RowAt(start));
            Assert.Equal(index - 1, rows.RowAt(Math.BitDecrement(start)));
            Assert.Equal(index, rows.RowAt(Math.BitIncrement(start)));
        }
        Assert.Equal(int.MaxValue, rows.RowAt(rows.TotalHeight));
        Assert.Equal(int.MaxValue, rows.RowAt(double.PositiveInfinity));
        Assert.Equal(0, rows.RowAt(double.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => rows.RowAt(double.NaN));
    }

    [Fact]
    public void Invalidation_preserves_measurements_outside_each_range()
    {
        var rows = new RowGeometry();
        rows.Reset(10_000, 28);
        rows.SetHeight(1, 50);
        rows.SetHeight(500, 100);
        rows.SetHeight(501, 200);
        rows.SetHeight(9999, 30);
        rows.Invalidate(500, 1);
        Assert.Equal(3, rows.MeasuredCount);
        Assert.Equal(28, rows.Height(500));
        Assert.Equal(200, rows.Height(501));
        rows.Invalidate(400, 1000);
        Assert.Equal(2, rows.MeasuredCount);
        Assert.Equal(50, rows.Height(1));
        Assert.Equal(30, rows.Height(9999));
        Assert.Equal(280024, rows.TotalHeight);
        rows.Invalidate(0, rows.Count);
        Assert.Equal(0, rows.MeasuredCount);
        Assert.Equal(280000, rows.TotalHeight);
        Assert.Equal(9999, rows.RowAt(rows.Start(9999)));
    }

    [Fact]
    public void Single_row_invalidations_have_no_warmed_allocation()
    {
        var rows = new RowGeometry();
        rows.Reset(10_000_000, 28);
        rows.SetHeight(8_000_000, 50);
        for (var i = 0; i < 512; ++i) { rows.SetHeight(20, 70); rows.Invalidate(20, 1); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) { rows.SetHeight(20, 70); rows.Invalidate(20, 1); }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(1, rows.MeasuredCount);
        Assert.Equal(280000022, rows.TotalHeight);
    }

    [Fact]
    public void Uniform_structure_changes_do_not_allocate_row_storage()
    {
        var rows = new RowGeometry();
        rows.Reset(1_000_000_000, 28);
        for (var i = 0; i < 512; ++i) Mutate();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) Mutate();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(1_000_000_000, rows.Count);
        Assert.Equal(0, rows.MeasuredCount);
        Assert.Equal(28_000_000_000d, rows.TotalHeight);
        void Mutate() { rows.Insert(10, 3); rows.Move(10, 100, 3); rows.Remove(100, 3); rows.Invalidate(0, rows.Count); }
    }

    [Fact]
    public void Overflowing_insert_is_rejected_without_mutating_geometry()
    {
        var rows = new RowGeometry();
        rows.Reset(1, double.MaxValue / 2);
        rows.SetHeight(0, double.MaxValue * .75);
        var height = rows.TotalHeight;
        Assert.Throws<ArgumentOutOfRangeException>(() => rows.Insert(0, 1));
        Assert.Equal(1, rows.Count);
        Assert.Equal(1, rows.MeasuredCount);
        Assert.Equal(height, rows.TotalHeight);
        Assert.Equal(height, rows.Height(0));
        rows.Reset(1, double.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => rows.Insert(1, 1));
        Assert.Equal(1, rows.Count);
        Assert.Equal(double.MaxValue, rows.TotalHeight);
    }

    [Fact]
    public void Zero_length_mutations_preserve_measurements()
    {
        var rows = new RowGeometry();
        rows.Reset(4, 28);
        rows.SetHeight(1, 80);
        rows.Insert(2, 0);
        rows.Remove(2, 0);
        rows.Move(1, 3, 0);
        rows.Move(1, 1, 1);
        rows.Invalidate(4, 0);
        Assert.Equal(4, rows.Count);
        Assert.Equal(1, rows.MeasuredCount);
        Assert.Equal(164, rows.TotalHeight);
        Assert.Throws<ArgumentOutOfRangeException>(() => rows.Invalidate(3, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => rows.Invalidate(0, -1));
        Assert.Equal(80, rows.Height(1));
    }
}
