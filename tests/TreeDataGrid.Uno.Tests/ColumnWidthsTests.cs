using System;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class ColumnWidthsTests
{
    [Fact]
    public void Fixed_auto_and_weighted_stars_share_remaining_space()
    {
        var auto = Column(GridLength.Auto);
        auto.RecordWidth(80);
        var widths = ColumnWidths.Calculate([Column(new(100)), auto, Column(new(1, GridUnitType.Star)), Column(new(3, GridUnitType.Star))], 500);
        Assert.Equal([100d, 80, 80, 240], widths);
    }
    [Fact]
    public void Maximum_redistributes_unused_space()
    {
        Assert.Equal([100d, 400], ColumnWidths.Calculate([Column(GridLength.Star, max: 100), Column(GridLength.Star)], 500));
    }
    [Fact]
    public void Minimum_redistributes_remaining_space()
    {
        Assert.Equal([350d, 150], ColumnWidths.Calculate([Column(GridLength.Star, min: 350), Column(GridLength.Star)], 500));
    }
    [Fact]
    public void Mixed_min_max_does_not_freeze_a_minimum_that_later_becomes_free()
    {
        Assert.Equal([480d, 20], ColumnWidths.Calculate([Column(GridLength.Star, min: 300), Column(GridLength.Star, max: 20)], 500));
    }
    [Fact]
    public void Mixed_min_max_does_not_freeze_a_maximum_that_later_becomes_free()
    {
        Assert.Equal([180d, 20], ColumnWidths.Calculate([Column(GridLength.Star, min: 180), Column(GridLength.Star, max: 80)], 200));
    }
    [Fact]
    public void Overflow_keeps_minima_and_maximum_wins_conflicts()
    {
        Assert.Equal([90d, 200], ColumnWidths.Calculate([Column(GridLength.Star, min: 150, max: 90), Column(GridLength.Star, min: 200)], 100));
    }
    [Fact]
    public void Zero_stars_receive_only_their_minimum()
    {
        Assert.Equal([30d, 170], ColumnWidths.Calculate([Column(new(0, GridUnitType.Star), min: 30), Column(GridLength.Star)], 200));
    }
    [Fact]
    public void Auto_measurement_is_monotonic_and_has_no_magic_150_pixel_width()
    {
        var column = Column(GridLength.Auto, min: 30);
        Assert.Equal(30, ColumnWidths.Calculate([column], 500)[0]);
        Assert.True(column.RecordWidth(82));
        Assert.False(column.RecordWidth(20));
        Assert.False(column.RecordWidth(double.NaN));
        Assert.Equal(82, ColumnWidths.Calculate([column], 500)[0]);
    }
    [Fact]
    public void Auto_constraints_initialize_from_native_measurement()
    {
        var model = new TextColumn<Item, string>("Name", x => x.Name, width: GridLength.Star,
            options: new() { MinWidth = GridLength.Auto, MaxWidth = GridLength.Auto });
        var column = new ValueCellColumn<Item, string>(model, CellKind.Text);
        Assert.True(column.RequiresUnconstrainedWidthMeasurement);
        Assert.True(ColumnWidths.Calculate([column], 500)[0] > 0);
        column.RecordWidth(123);
        Assert.Equal(123, ColumnWidths.Calculate([column], 500)[0]);
    }
    [Fact]
    public void Auto_maximum_uses_measurement_after_numeric_minimum()
    {
        var model = new TextColumn<Item, string>("Name", x => x.Name, width: GridLength.Star,
            options: new() { MinWidth = new(100), MaxWidth = GridLength.Auto });
        var column = new ValueCellColumn<Item, string>(model, CellKind.Text);
        column.RecordWidth(20);
        Assert.Equal(100, ColumnWidths.Calculate([column], 500)[0]);
    }
    [Fact]
    public void Star_constraints_are_rejected_explicitly()
    {
        var model = new TextColumn<Item, string>("Name", x => x.Name, options: new() { MinWidth = GridLength.Star });
        Assert.Throws<ArgumentException>(() => new ValueCellColumn<Item, string>(model, CellKind.Text));
    }
    [Fact]
    public void Tiny_star_weight_recovers_after_a_huge_weight_reaches_its_maximum()
    {
        Assert.Equal([100d, 400], ColumnWidths.Calculate([
            Column(new(1e308, GridUnitType.Star), max: 100),
            Column(new(1e-308, GridUnitType.Star))], 500));
    }
    [Fact]
    public void Random_constraints_match_independent_monotonic_solver()
    {
        var random = new Random(42);
        for (var trial = 0; trial < 500; ++trial)
        {
            var columns = Enumerable.Range(0, 12).Select(_ => Column(new(random.Next(1, 10), GridUnitType.Star),
                min: random.Next(0, 200), max: random.Next(200, 600))).ToArray();
            var available = random.Next(0, 8000);
            var actual = ColumnWidths.Calculate(columns, available);
            var lower = 0d;
            var upper = 10000d;
            for (var step = 0; step < 80; ++step)
            {
                var unit = (lower + upper) / 2;
                var total = columns.Sum(c => Math.Min(c.MaximumWidth, Math.Max(c.MinimumWidth, c.Model.Width.Value * unit)));
                if (total > available) upper = unit; else lower = unit;
            }
            for (var i = 0; i < columns.Length; ++i)
            {
                var expected = Math.Min(columns[i].MaximumWidth, Math.Max(columns[i].MinimumWidth, columns[i].Model.Width.Value * lower));
                Assert.True(Math.Abs(actual[i] - expected) < 1e-7, $"trial={trial}, column={i}, actual={actual[i]}, expected={expected}");
            }
        }
    }
    private static CellColumn Column(GridLength width, double min = 0, double max = double.PositiveInfinity) => new TestColumn(width, min, max);
    private sealed class TestColumn(GridLength width, double min, double max) : CellColumn(new TextColumn<Item, string>("Name", x => x.Name, width))
    {
        public override double MinimumWidth => min;
        public override double MaximumWidth => max;
        public override CellValue CreateCell(IRow row) => throw new NotSupportedException();
    }
    private sealed record Item(string Name);
}
