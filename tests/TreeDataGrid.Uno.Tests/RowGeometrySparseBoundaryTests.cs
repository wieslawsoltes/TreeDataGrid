using System;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class RowGeometrySparseBoundaryTests
{
    [Fact]
    public void Sparse_lookup_does_not_cross_a_boundary_early()
    {
        var rows = new RowGeometry();
        rows.Reset(8, 25.6);
        rows.SetHeight(0, 64);
        rows.SetHeight(2, 128);
        var boundary = rows.Start(3);
        Assert.Equal(2, rows.RowAt(Math.BitDecrement(boundary)));
        Assert.Equal(3, rows.RowAt(boundary));
        Assert.Equal(3, rows.RowAt(Math.BitIncrement(boundary)));
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(25.6)]
    [InlineData(28.1)]
    [InlineData(1.0 / 3.0)]
    public void Sparse_lookup_matches_every_canonical_boundary(double estimate)
    {
        const int count = 257;
        var rows = new RowGeometry();
        rows.Reset(count, estimate);
        for (var row = 0; row < count; row += 7)
            rows.SetHeight(row, estimate * (1.25 + (row % 11) * 0.375));
        Assert.True(rows.MeasuredCount > 0);
        AssertBoundaries(rows);
        Assert.Equal(count - 1, rows.RowAt(Math.BitDecrement(rows.TotalHeight)));
        Assert.Equal(count, rows.RowAt(rows.TotalHeight));
        Assert.Equal(count, rows.RowAt(double.PositiveInfinity));
        Assert.Equal(0, rows.RowAt(double.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => rows.RowAt(double.NaN));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void Partial_invalidation_removes_near_estimate_measurements(int count)
    {
        var rows = new RowGeometry();
        rows.Reset(16, 28);
        rows.SetHeight(3, 100);
        rows.SetHeight(3, Math.BitIncrement(28d));
        rows.SetHeight(12, 64);
        Assert.Equal(2, rows.MeasuredCount);
        Assert.NotEqual(28d, rows.Height(3));
        rows.Invalidate(3, count);
        Assert.Equal(1, rows.MeasuredCount);
        Assert.Equal(28d, rows.Height(3));
        Assert.Equal(64d, rows.Height(12));
        rows.Invalidate(12, 1);
        Assert.Equal(0, rows.MeasuredCount);
        Assert.Equal(16 * 28d, rows.TotalHeight);
    }

    [Fact]
    public void Sparse_lookup_matches_boundaries_after_structural_mutations()
    {
        var rows = new RowGeometry();
        rows.Reset(64, 25.6);
        rows.SetHeight(0, 64);
        rows.SetHeight(2, 128);
        rows.SetHeight(31, 80.25);
        rows.SetHeight(63, 42.5);
        AssertBoundaries(rows);
        rows.Insert(2, 5);
        AssertBoundaries(rows);
        rows.Move(0, 20, 3);
        AssertBoundaries(rows);
        rows.Remove(9, 4);
        AssertBoundaries(rows);
        rows.Invalidate(15, 10);
        AssertBoundaries(rows);
    }

    [Fact]
    public void Sparse_queries_have_no_warmed_managed_allocation()
    {
        var rows = new RowGeometry();
        rows.Reset(8, 25.6);
        rows.SetHeight(0, 64);
        rows.SetHeight(2, 128);
        var offset = Math.BitDecrement(rows.Start(3));
        var checksum = 0;
        for (var iteration = 0; iteration < 1024; ++iteration)
            checksum ^= rows.RowAt(offset);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 4096; ++iteration)
            checksum ^= rows.RowAt(offset);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(0, checksum);
        Assert.Equal(2, rows.RowAt(offset));
    }

    [Fact]
    public void Invalidating_a_large_range_leaves_only_outside_measurements()
    {
        var rows = new RowGeometry();
        rows.Reset(1000, 28);
        foreach (var row in new[] { 2, 10, 200, 700, 998 })
        {
            rows.SetHeight(row, 100);
            rows.SetHeight(row, Math.BitIncrement(28d));
        }
        rows.Invalidate(1, 800);
        Assert.Equal(1, rows.MeasuredCount);
        Assert.Equal(Math.BitIncrement(28d), rows.Height(998));
        rows.Invalidate(998, 1);
        Assert.Equal(0, rows.MeasuredCount);
        Assert.Equal(28000d, rows.TotalHeight);
    }

    private static void AssertBoundaries(RowGeometry rows)
    {
        for (var row = 1; row < rows.Count; ++row)
        {
            var boundary = rows.Start(row);
            Assert.True(rows.Start(row - 1) < boundary);
            Assert.True(boundary < rows.Start(row + 1));
            Assert.Equal(row - 1, rows.RowAt(Math.BitDecrement(boundary)));
            Assert.Equal(row, rows.RowAt(boundary));
            Assert.Equal(row, rows.RowAt(Math.BitIncrement(boundary)));
        }
        Assert.Equal(rows.Count, rows.RowAt(rows.TotalHeight));
    }
}
