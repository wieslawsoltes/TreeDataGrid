using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TreeDataGridBenchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 5, iterationCount: 10)]
public class ElementFactoryBenchmarks
{
    private const int OperationCount = 500_000;
    private readonly object _data = new();
    private BenchmarkElementFactory? _factory;
    private StackPanel[]? _parents;

    [Params(10, 100, 1_000)]
    public int PoolSize { get; set; }

    [IterationSetup]
    public void Setup()
    {
        _factory = new BenchmarkElementFactory();
        _parents = new StackPanel[PoolSize];
        var elements = new Control[PoolSize];

        for (var i = 0; i < PoolSize; ++i)
        {
            var parent = _parents[i] = new StackPanel();
            var element = elements[i] = _factory.GetOrCreateElement(_data, parent);
            parent.Children.Add(element);
        }

        for (var i = 0; i < PoolSize; ++i)
            _factory.RecycleElement(elements[i]);
    }

    [Benchmark(OperationsPerInvoke = OperationCount)]
    public void SameParentReuse()
    {
        var factory = _factory!;
        var parents = _parents!;

        for (var i = 0; i < OperationCount; ++i)
        {
            // The multiplier walks every pool position without a sequential access bias.
            var parent = parents[(i * 397) % parents.Length];
            var element = factory.GetOrCreateElement(_data, parent);
            factory.RecycleElement(element);
        }
    }

    private sealed class BenchmarkElementFactory : TreeDataGridElementFactory
    {
        protected override Control CreateElement(object? data) => new Border();

        protected override string GetDataRecycleKey(object? data) => "element";

        protected override string GetElementRecycleKey(Control element) => "element";
    }
}
