using System;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class RowViewportTests
{
    [Theory]
    [InlineData(0, 100, 1000, 0, 0, 100)]
    [InlineData(0, 100, 1000, 0.5, 0, 200)]
    [InlineData(110, 100, 1000, 0.5, 60, 260)]
    [InlineData(900, 100, 1000, 0.5, 800, 1000)]
    [InlineData(400, 100, 1000, 2, 200, 700)]
    [InlineData(0, 100, 50, 2, 0, 50)]
    [InlineData(50, 0, 1000, 2, 50, 50)]
    [InlineData(0, 100, 0, 0.5, 0, 0)]
    public void Cache_matches_reference_edge_redistribution(double offset, double height, double extent,
        double cache, double start, double end) =>
        Assert.Equal(new RowViewport(start, end), RowViewport.Calculate(offset, height, extent, cache));

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Invalid_cache_length_is_rejected(double cache) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RowViewport.Calculate(0, 100, 1000, cache));

    [Theory]
    [InlineData(0, 100, 0, 10)]
    [InlineData(0, 100.1, 0, 11)]
    [InlineData(10, 20, 1, 2)]
    [InlineData(10, 10, 1, 1)]
    [InlineData(999, 1000, 99, 100)]
    public void Row_range_has_an_exclusive_end(double start, double end, int first, int exclusiveEnd)
    {
        var rows = new RowGeometry();
        rows.Reset(100, 10);
        Assert.Equal((first, exclusiveEnd), new RowViewport(start, end).GetRows(rows));
    }

    [Fact]
    public void Variable_heights_and_source_changes_use_current_geometry()
    {
        var rows = new RowGeometry();
        rows.Reset(100, 10);
        rows.SetHeight(0, 80);
        var viewport = RowViewport.Calculate(0, 100, rows.TotalHeight, 0.5);
        Assert.Equal((0, 13), viewport.GetRows(rows));
        rows.SetHeight(0, 10);
        Assert.Equal((0, 20), viewport.GetRows(rows));
        rows.Reset(3, 10);
        Assert.Equal((0, 3), RowViewport.Calculate(0, 100, rows.TotalHeight, 0.5).GetRows(rows));
    }
}
