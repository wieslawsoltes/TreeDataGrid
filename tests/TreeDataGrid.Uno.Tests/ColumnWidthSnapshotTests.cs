using System;
using System.Collections;
using System.Collections.Generic;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnWidthSnapshotTests
{
    [Fact]
    public void Redistribution_reads_each_constraint_once()
    {
        var capped = new Probe(GridLength.Star, 0, 20);
        var minimum = new Probe(GridLength.Star, 300);
        var output = ColumnWidths.Calculate([minimum, capped], 500);
        Assert.Equal(new[] { 480d, 20d }, output);
        Assert.Equal(1, capped.MinimumReads);
        Assert.Equal(1, capped.MaximumReads);
        Assert.Equal(1, minimum.MinimumReads);
        Assert.Equal(1, minimum.MaximumReads);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(512)]
    public void Throwing_constraint_does_not_partially_overwrite_the_destination(int count)
    {
        var columns = new CellColumn[count];
        for (var i = 0; i < count; ++i) columns[i] = new Probe(new GridLength(20));
        var failure = new InvalidOperationException("Application constraint failed.");
        ((Probe)columns[^1]).ReadMinimum = () => throw failure;
        var output = new double[count];
        Array.Fill(output, -13d);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => ColumnWidths.Calculate(columns, 500, output)));
        Assert.All(output, value => Assert.Equal(-13d, value));
    }

    [Theory]
    [InlineData(double.NaN, 100d)]
    [InlineData(-1d, 100d)]
    [InlineData(double.PositiveInfinity, 100d)]
    [InlineData(0d, double.NaN)]
    [InlineData(0d, -1d)]
    public void Invalid_constraints_preserve_the_entire_output(double minimum, double maximum)
    {
        CellColumn[] columns = [new Probe(new GridLength(20)), new Probe(GridLength.Star, minimum, maximum)];
        var output = new[] { -13d, -17d };
        Assert.Throws<ArgumentOutOfRangeException>(() => ColumnWidths.Calculate(columns, 500, output));
        Assert.Equal(new[] { -13d, -17d }, output);
    }

    [Fact]
    public void Native_widths_are_not_reread_after_constraint_callbacks()
    {
        var first = new Probe(GridLength.Star, 0, 20);
        var second = new Probe(GridLength.Star);
        first.ReadMinimum = () => { first.Model.Width = new GridLength(9, GridUnitType.Star); return 0; };
        var output = ColumnWidths.Calculate([first, second], 100);
        Assert.Equal(new[] { 20d, 80d }, output);
        Assert.Equal(1, first.MinimumReads);
    }

    [Fact]
    public void Indexed_inputs_are_read_once_and_not_enumerated()
    {
        var columns = new ReadOnceColumns([new Probe(new GridLength(30)), new Probe(GridLength.Star, 0, 20), new Probe(GridLength.Star)]);
        var output = new double[3];
        ColumnWidths.Calculate(columns, 300, output);
        Assert.Equal(new[] { 30d, 20d, 250d }, output);
        Assert.Equal(1, columns.CountReads);
        Assert.All(columns.Reads, reads => Assert.Equal(1, reads));
    }

    [Fact]
    public void Throwing_indexer_leaves_destination_unchanged()
    {
        var columns = new ReadOnceColumns([new Probe(new GridLength(30)), new Probe(GridLength.Star)]) { ThrowAt = 1 };
        var output = new[] { -13d, -17d };
        Assert.Throws<InvalidOperationException>(() => ColumnWidths.Calculate(columns, 300, output));
        Assert.Equal(new[] { -13d, -17d }, output);
    }

    [Fact]
    public void A_changing_constraint_is_not_used_as_a_different_solver_input()
    {
        var capped = new Probe(GridLength.Star, 0, 20);
        capped.ReadMinimum = () => capped.MinimumReads == 1 ? 0 : throw new InvalidOperationException("Constraint reread.");
        Assert.Equal(new[] { 20d, 480d }, ColumnWidths.Calculate([capped, new Probe(GridLength.Star)], 500));
    }

    [Fact]
    public void Reentrant_calculations_own_distinct_snapshots()
    {
        CellColumn[] nested = [new Probe(GridLength.Star, 80), new Probe(GridLength.Star)];
        var nestedResult = new double[2];
        var first = new Probe(GridLength.Star, 0, 20);
        first.ReadMinimum = () => { ColumnWidths.Calculate(nested, 100, nestedResult); return 0; };
        Assert.Equal(new[] { 20d, 480d }, ColumnWidths.Calculate([first, new Probe(GridLength.Star)], 500));
        Assert.Equal(new[] { 80d, 20d }, nestedResult);
        Assert.Equal(1, first.MinimumReads);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(512)]
    public void Failed_snapshots_do_not_poison_reused_storage(int count)
    {
        var columns = new CellColumn[count];
        for (var i = 0; i < count; ++i) columns[i] = new Probe(GridLength.Star, 0, 50);
        var output = new double[count];
        var last = (Probe)columns[^1];
        last.ReadMinimum = () => throw new InvalidOperationException("Expected.");
        Assert.Throws<InvalidOperationException>(() => ColumnWidths.Calculate(columns, count * 100d, output));
        last.ReadMinimum = null;
        ColumnWidths.Calculate(columns, count * 100d, output);
        Assert.All(output, width => Assert.Equal(50d, width));
        for (var i = 0; i < count; ++i) columns[i] = new Probe(new GridLength(23));
        ColumnWidths.Calculate(columns, 100, output);
        Assert.All(output, width => Assert.Equal(23d, width));
    }

    [Fact]
    public void Allocating_overload_rejects_null_consistently()
    {
        Assert.Throws<ArgumentNullException>(() => ColumnWidths.Calculate(null!, 100));
    }

    private sealed class Item;
    private sealed class Probe(GridLength width, double min = 0, double max = double.PositiveInfinity)
        : CellColumn(new TextColumn<Item, string>("Name", _ => "", width))
    {
        public int MinimumReads, MaximumReads;
        public Func<double>? ReadMinimum;
        public override double MinimumWidth { get { ++MinimumReads; return ReadMinimum?.Invoke() ?? min; } }
        public override double MaximumWidth { get { ++MaximumReads; return max; } }
        public override CellValue CreateCell(IRow row) => throw new NotSupportedException();
    }
    private sealed class ReadOnceColumns(CellColumn[] values) : IReadOnlyList<CellColumn>
    {
        public int CountReads;
        public int[] Reads { get; } = new int[values.Length];
        public int ThrowAt { get; init; } = -1;
        public int Count { get { ++CountReads; return values.Length; } }
        public CellColumn this[int index]
        {
            get
            {
                if (index == ThrowAt) throw new InvalidOperationException("Indexer failed.");
                if (++Reads[index] != 1) throw new InvalidOperationException("Indexer reread.");
                return values[index];
            }
        }
        public IEnumerator<CellColumn> GetEnumerator() => throw new InvalidOperationException("Must use indexed input.");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
