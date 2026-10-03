using System;
using System.Linq;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class RowGeometryTests
{
    [Fact]
    public void Millions_of_unknown_rows_do_not_allocate_per_row_measurements()
    {
        var rows = new RowGeometry();
        rows.Reset(10_000_000, 28);
        Assert.Equal(0, rows.MeasuredCount);
        Assert.Equal(280_000_000, rows.TotalHeight);
        Assert.Equal(9_000_000, rows.RowAt(252_000_000));
        rows.SetHeight(3, 70);
        Assert.Equal(1, rows.MeasuredCount);
        Assert.Equal(280_000_042, rows.TotalHeight);
    }
    [Fact]
    public void Height_changes_support_growing_shrinking_and_exact_boundaries()
    {
        var rows = new RowGeometry();
        rows.Reset(4, 10);
        rows.SetHeight(1, 30);
        Assert.Equal(1, rows.RowAt(10));
        Assert.Equal(1, rows.RowAt(39));
        Assert.Equal(2, rows.RowAt(40));
        rows.SetHeight(1, 5);
        Assert.Equal(15, rows.Start(2));
        Assert.Equal(2, rows.RowAt(15));
        Assert.Equal(4, rows.RowAt(rows.TotalHeight));
    }
    [Fact]
    public void Insert_remove_and_move_preserve_surviving_measurements()
    {
        var rows = new RowGeometry();
        rows.Reset(5, 10);
        rows.SetHeight(1, 20);
        rows.SetHeight(3, 40);
        rows.Insert(1, 2);
        Assert.Equal(20, rows.Height(3));
        Assert.Equal(40, rows.Height(5));
        rows.Remove(2, 2);
        Assert.Equal(40, rows.Height(3));
        Assert.Equal(1, rows.MeasuredCount);
        rows.Move(3, 0, 1);
        Assert.Equal(40, rows.Height(0));
        rows.Invalidate(0, 1);
        Assert.Equal(50, rows.TotalHeight);
    }
    [Fact]
    public void Reset_releases_all_measurements_and_changes_the_estimate()
    {
        var rows = new RowGeometry();
        rows.Reset(100, 10);
        rows.SetHeight(20, 80);
        rows.Reset(2, 30);
        Assert.Equal(0, rows.MeasuredCount);
        Assert.Equal(60, rows.TotalHeight);
        Assert.Throws<ArgumentOutOfRangeException>(() => rows.SetHeight(2, 40));
        Assert.Throws<ArgumentOutOfRangeException>(() => rows.SetHeight(0, double.NaN));
    }
    [Fact]
    public void Random_updates_match_dense_reference_geometry()
    {
        var random = new Random(23);
        var rows = new RowGeometry();
        rows.Reset(500, 28);
        var expected = Enumerable.Repeat(28d, 500).ToArray();
        for (var iteration = 0; iteration < 2000; ++iteration)
        {
            var index = random.Next(expected.Length);
            expected[index] = random.Next(1, 300);
            rows.SetHeight(index, expected[index]);
            var query = random.Next(expected.Length);
            var start = expected.Take(query).Sum();
            Assert.Equal(start, rows.Start(query));
            Assert.Equal(query, rows.RowAt(start));
            Assert.Equal(query, rows.RowAt(start + expected[query] - 0.5));
        }
    }
}
