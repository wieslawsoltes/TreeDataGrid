using System;
using System.Linq;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class RowGeometryTransactionalReplacementTests
{
    [Fact]
    public void Equal_count_replacement_rejects_overflow_without_partial_mutation()
    {
        var rows = new RowGeometry();
        rows.Reset(4, double.MaxValue / 8);
        rows.SetHeight(1, rows.Estimate / 4);
        rows.SetHeight(2, rows.Estimate / 4);
        rows.SetHeight(0, double.MaxValue * (11d / 16));
        Assert.True(double.IsFinite(rows.TotalHeight));
        var count = rows.Count;
        var measuredCount = rows.MeasuredCount;
        var heights = Enumerable.Range(0, count).Select(rows.Height).ToArray();
        var starts = Enumerable.Range(0, count + 1).Select(rows.Start).ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => rows.Replace(1, 2, 2));
        Assert.Equal(count, rows.Count);
        Assert.Equal(measuredCount, rows.MeasuredCount);
        Assert.Equal(heights, Enumerable.Range(0, count).Select(rows.Height));
        Assert.Equal(starts, Enumerable.Range(0, count + 1).Select(rows.Start));
    }

    [Fact]
    public void Equal_count_replacement_accepts_a_finite_final_extent()
    {
        var rows = new RowGeometry();
        rows.Reset(4, double.MaxValue / 8);
        rows.SetHeight(0, rows.Estimate / 4);
        rows.SetHeight(1, rows.Estimate / 4);
        rows.SetHeight(2, double.MaxValue * (11d / 16));
        Assert.True(double.IsFinite(rows.TotalHeight));
        rows.Replace(0, 3, 3);
        Assert.Equal(4, rows.Count);
        Assert.Equal(0, rows.MeasuredCount);
        Assert.Equal(4 * rows.Estimate, rows.TotalHeight);
        for (var row = 0; row < rows.Count; ++row)
            Assert.Equal(rows.Estimate, rows.Height(row));
    }

    [Fact]
    public void Equal_count_replacement_preserves_measurements_outside_the_range()
    {
        var rows = new RowGeometry();
        rows.Reset(6, 10);
        rows.SetHeight(0, 20);
        rows.SetHeight(1, 7);
        rows.SetHeight(2, 9);
        rows.SetHeight(5, 30);
        rows.Replace(1, 2, 2);
        Assert.Equal(6, rows.Count);
        Assert.Equal(2, rows.MeasuredCount);
        Assert.Equal(new double[] { 20, 10, 10, 10, 10, 30 },
            Enumerable.Range(0, rows.Count).Select(rows.Height));
        Assert.Equal(new double[] { 0, 20, 30, 40, 50, 60, 90 },
            Enumerable.Range(0, rows.Count + 1).Select(rows.Start));
    }
}
