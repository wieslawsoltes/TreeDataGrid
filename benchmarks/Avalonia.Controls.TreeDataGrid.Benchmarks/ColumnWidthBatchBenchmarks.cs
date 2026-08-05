using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TreeDataGridBenchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, invocationCount: 8192, warmupCount: 10, iterationCount: 20)]
public class ColumnWidthBatchBenchmarks
{
    [Params(ColumnLayout.Auto, ColumnLayout.Mixed)]
    public ColumnLayout Layout { get; set; }

    [Params(20, 40)]
    public int RowCount { get; set; }

    [Benchmark(Baseline = true)]
    public int IncrementalWidthCommits()
    {
        var columns = CreateColumns();

        for (var row = 0; row < RowCount; ++row)
        {
            MeasureRow(columns, row);
            columns.CommitActualWidths();
        }

        return Checksum(columns);
    }

    [Benchmark]
    public int BatchedWidthCommit()
    {
        var columns = CreateColumns();
        var batch = (IColumnLayoutBatch)columns;
        batch.BeginActualWidthBatch();

        for (var row = 0; row < RowCount; ++row)
        {
            MeasureRow(columns, row);
            columns.CommitActualWidths();
            batch.RequestFinalMeasure();
        }

        _ = batch.EndActualWidthBatch();
        return Checksum(columns);
    }

    private ColumnList<Model> CreateColumns()
    {
        var result = new ColumnList<Model>();

        for (var column = 0; column < 6; ++column)
        {
            var width = Layout switch
            {
                ColumnLayout.Auto => GridLength.Auto,
                ColumnLayout.Mixed => (column % 3) switch
                {
                    0 => GridLength.Auto,
                    1 => new GridLength(1, GridUnitType.Star),
                    _ => new GridLength(124),
                },
                _ => throw new ArgumentOutOfRangeException(),
            };
            result.Add(new TextColumn<Model, string>(null, x => x.Value, width));
        }

        result.ViewportChanged(new Rect(0, 0, 800, 500));
        return result;
    }

    private void MeasureRow(ColumnList<Model> columns, int row)
    {
        for (var column = 0; column < columns.Count; ++column)
        {
            columns.CellMeasured(
                column,
                row,
                new Size(40 + row + column, 24));
        }
    }

    private static int Checksum(ColumnList<Model> columns)
    {
        var result = 0;

        for (var column = 0; column < columns.Count; ++column)
            result += (int)columns[column].ActualWidth;

        return result;
    }

    public enum ColumnLayout
    {
        Auto,
        Mixed,
    }

    private sealed class Model
    {
        public string Value { get; set; } = string.Empty;
    }
}
