using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TreeDataGridBenchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 5, iterationCount: 10)]
public class ColumnGeometryBenchmarks
{
    private const int Lookups = 1024;
    private ColumnList<RowModel> _columns = null!;

    [Params(20, 200, 1000)]
    public int ColumnCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _columns = new();
        for (var i = 0; i < ColumnCount; ++i)
            _columns.Add(new TextColumn<RowModel, string>(null, x => x.Value, new GridLength(100)));
    }

    [Benchmark(OperationsPerInvoke = Lookups)]
    public int LocateViewport()
    {
        var result = 0;
        for (var i = 0; i < Lookups; ++i)
            result += _columns.GetColumnAt((i % ColumnCount) * 100 + 50).index;
        return result;
    }

    private sealed record RowModel(string Value);
}
