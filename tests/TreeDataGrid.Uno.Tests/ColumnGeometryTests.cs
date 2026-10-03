using System;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class ColumnGeometryTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 3)]
    [InlineData(29, 3)]
    [InlineData(30, -1)]
    [InlineData(-1, -1)]
    public void Upper_bound_skips_zero_widths(double x, int expected)
    {
        var geometry = new ColumnGeometry();
        geometry.Commit([0, 10, 0, 20, 0]);
        Assert.Equal(expected, geometry.ColumnAt(x));
    }

    [Fact]
    public void Exact_right_edge_excludes_next_column()
    {
        var geometry = new ColumnGeometry();
        geometry.Commit([10, 20, 30]);
        Assert.Equal((0, 1), geometry.VisibleRange(0, 10));
        Assert.Equal((1, 2), geometry.VisibleRange(10, 20));
        Assert.Equal((1, 3), geometry.VisibleRange(10, 21));
    }

    [Fact]
    public void Resizing_and_reordering_replace_committed_geometry()
    {
        var geometry = new ColumnGeometry();
        geometry.Commit([10, 20, 30]);
        geometry.Commit([30, 20, 10]);
        Assert.Equal(0, geometry.ColumnAt(20));
        Assert.Equal(30, geometry.Start(1));
        Assert.Equal(10, geometry.Width(2));
        geometry.Commit([7]);
        Assert.Equal(7, geometry.TotalWidth);
        Assert.Equal(-1, geometry.ColumnAt(7));
    }

    [Fact]
    public void Invalid_commit_preserves_previous_geometry()
    {
        var geometry = new ColumnGeometry();
        geometry.Commit([10, 20]);
        Assert.Throws<ArgumentOutOfRangeException>(() => geometry.Commit([30, double.NaN]));
        Assert.Equal(30, geometry.TotalWidth);
        Assert.Equal(1, geometry.ColumnAt(10));
    }

    [Fact]
    public void Wide_grid_only_returns_viewport_columns()
    {
        var geometry = new ColumnGeometry();
        var widths = new double[1000];
        Array.Fill(widths, 100d);
        geometry.Commit(widths);
        Assert.Equal((900, 905), geometry.VisibleRange(90_000, 500));
    }
}
