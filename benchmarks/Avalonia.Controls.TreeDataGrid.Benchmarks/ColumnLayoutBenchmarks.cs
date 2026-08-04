using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TreeDataGridBenchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 5, iterationCount: 10)]
public class ColumnLayoutBenchmarks
{
    private const int CommitCount = 100_000;
    private ColumnList<RowModel>? _columns;

    [Params(20, 200, 1_000)]
    public int ColumnCount { get; set; }

    [IterationSetup]
    public void Setup()
    {
        _columns = new ColumnList<RowModel>();

        for (var i = 0; i < ColumnCount; ++i)
        {
            _columns.Add(new TextColumn<RowModel, string>(
                $"Column {i}",
                x => x.Value,
                i % 10 == 0 ? GridLength.Auto : new GridLength(80)));
        }

        _columns.ViewportChanged(new Rect(0, 0, 800, 500));

        for (var i = 0; i < ColumnCount; ++i)
            _columns.CellMeasured(i, 0, new Size(80 + (i % 5), 24));

        _columns.CommitActualWidths();
    }

    [Benchmark(OperationsPerInvoke = CommitCount)]
    public void CommitUnchangedWidths()
    {
        var columns = _columns!;

        for (var i = 0; i < CommitCount; ++i)
            columns.CommitActualWidths();
    }

    private sealed record RowModel(string Value);
}
