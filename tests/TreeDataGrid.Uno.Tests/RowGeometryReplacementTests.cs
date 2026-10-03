using System;
using System.Collections.Generic;
using System.Linq;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class RowGeometryReplacementTests
{
    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(0, 3, 0)]
    [InlineData(0, 10, 4)]
    [InlineData(7, 3, 8)]
    [InlineData(3, 4, 1)]
    [InlineData(3, 1, 5)]
    [InlineData(3, 3, 3)]
    [InlineData(10, 0, 2)]
    [InlineData(0, 0, 0)]
    public void Replacement_matches_dense_oracle_and_preserves_surviving_measurements(int index, int oldCount, int newCount)
    {
        var geometry = new RowGeometry();
        geometry.Reset(10, 12.5);
        var oracle = Enumerable.Repeat(12.5, 10).ToList();
        for (var row = 0; row < oracle.Count; row += 2)
        {
            oracle[row] = 3.5 + row * 2;
            geometry.SetHeight(row, oracle[row]);
        }
        geometry.Replace(index, oldCount, newCount);
        oracle.RemoveRange(index, oldCount);
        oracle.InsertRange(index, Enumerable.Repeat(geometry.Estimate, newCount));
        AssertGeometry(geometry, oracle);
    }

    [Fact]
    public void Mixed_random_mutations_preserve_all_prefixes_and_adjacent_boundaries()
    {
        var random = new Random(730319);
        var geometry = new RowGeometry();
        geometry.Reset(40, 12.5);
        var oracle = Enumerable.Repeat(12.5, 40).ToList();
        for (var iteration = 0; iteration < 1500; ++iteration)
        {
            if (oracle.Count > 0 && iteration % 3 == 0)
            {
                var row = random.Next(oracle.Count);
                var height = random.Next(1, 700) / 8d;
                geometry.SetHeight(row, height);
                oracle[row] = height;
            }
            else
            {
                var start = random.Next(oracle.Count + 1);
                var removed = random.Next(Math.Min(oracle.Count - start, 15) + 1);
                var added = oracle.Count > 150 ? 0 : random.Next(12);
                geometry.Replace(start, removed, added);
                oracle.RemoveRange(start, removed);
                oracle.InsertRange(start, Enumerable.Repeat(geometry.Estimate, added));
            }
            AssertGeometry(geometry, oracle);
        }
    }

    [Theory]
    [InlineData(-1, 1, 2)]
    [InlineData(101, 0, 1)]
    [InlineData(99, 2, 1)]
    [InlineData(0, -1, 2)]
    [InlineData(0, 1, -1)]
    [InlineData(0, 1, int.MaxValue)]
    public void Invalid_replacements_do_not_mutate_count_or_sparse_state(int index, int oldCount, int newCount)
    {
        var geometry = new RowGeometry();
        geometry.Reset(100, 10);
        geometry.SetHeight(2, 35);
        var before = Enumerable.Range(0, geometry.Count + 1).Select(geometry.Start).ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => geometry.Replace(index, oldCount, newCount));
        Assert.Equal(100, geometry.Count);
        Assert.Equal(1, geometry.MeasuredCount);
        Assert.Equal(before, Enumerable.Range(0, geometry.Count + 1).Select(geometry.Start));
    }

    [Fact]
    public void Overflowing_absolute_extent_is_rejected_before_publishing_either_sparse_index()
    {
        var geometry = new RowGeometry();
        geometry.Reset(4, double.MaxValue / 8);
        geometry.SetHeight(0, double.MaxValue / 2);
        var before = Enumerable.Range(0, 5).Select(geometry.Start).ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => geometry.Replace(1, 1, 3));
        Assert.Equal(4, geometry.Count);
        Assert.Equal(1, geometry.MeasuredCount);
        Assert.Equal(double.MaxValue / 2, geometry.Height(0));
        Assert.Equal(before, Enumerable.Range(0, 5).Select(geometry.Start));
    }

    [Fact]
    public void Removing_a_large_measurement_does_not_overflow_an_intermediate_insert_extent()
    {
        var geometry = new RowGeometry();
        geometry.Reset(4, double.MaxValue / 16);
        geometry.SetHeight(1, double.MaxValue * .75);
        Assert.False(double.IsFinite(geometry.TotalHeight + 6 * geometry.Estimate));
        geometry.Replace(1, 1, 6);
        Assert.Equal(9, geometry.Count);
        Assert.Equal(0, geometry.MeasuredCount);
        Assert.Equal(9 * geometry.Estimate, geometry.TotalHeight);
        Assert.True(double.IsFinite(geometry.TotalHeight));
    }

    [Fact]
    public void Uniform_count_changing_replacements_allocate_no_per_operation_storage()
    {
        var geometry = new RowGeometry();
        geometry.Reset(10_000_000, 28);
        for (var i = 0; i < 32; ++i)
        {
            geometry.Replace(17, 2, 4);
            geometry.Replace(17, 4, 2);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; ++i)
        {
            geometry.Replace(17, 2, 4);
            geometry.Replace(17, 4, 2);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(10_000_000, geometry.Count);
        Assert.Equal(0, geometry.MeasuredCount);
        Assert.Equal(280_000_000, geometry.TotalHeight);
    }

    [Fact]
    public void Near_int_maximum_counts_do_not_overflow_fenwick_index_updates()
    {
        var geometry = new RowGeometry();
        geometry.Reset(int.MaxValue - 5, 1);
        geometry.SetHeight(int.MaxValue - 7, 3);
        geometry.Replace(10, 2, 7);
        Assert.Equal(int.MaxValue, geometry.Count);
        Assert.Equal(3, geometry.Height(int.MaxValue - 2));
        Assert.Equal(int.MaxValue + 2d, geometry.TotalHeight);
        Assert.Equal(int.MaxValue - 2, geometry.RowAt(geometry.Start(int.MaxValue - 2)));
    }

    private static void AssertGeometry(RowGeometry geometry, IReadOnlyList<double> oracle)
    {
        Assert.Equal(oracle.Count, geometry.Count);
        Assert.Equal(oracle.Count(value => value != geometry.Estimate), geometry.MeasuredCount);
        var start = 0d;
        for (var row = 0; row < oracle.Count; ++row)
        {
            Assert.Equal(start, geometry.Start(row), 8);
            Assert.Equal(oracle[row], geometry.Height(row));
            Assert.Equal(row, geometry.RowAt(start));
            Assert.Equal(row, geometry.RowAt(start + oracle[row] / 2));
            if (row > 0) Assert.Equal(row - 1, geometry.RowAt(Math.BitDecrement(start)));
            start += oracle[row];
        }
        Assert.Equal(start, geometry.TotalHeight, 8);
        Assert.Equal(oracle.Count, geometry.RowAt(start));
    }
}
