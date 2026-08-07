using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TreeDataGridBenchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 5, iterationCount: 10)]
[InvocationCount(64)]
public class TemplateCellRecyclingBenchmarks
{
    private const int OperationCount = 1_000;

    private Window? _window;
    private Decorator? _host;
    private TreeDataGridTemplateCell? _cell;
    private TreeDataGridElementFactory? _factory;
    private TemplateCell? _firstModel;
    private TemplateCell? _secondModel;

    [GlobalSetup]
    public void GlobalSetup()
    {
        AppBuilder.Configure<BenchmarkApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .SetupWithoutStarting();

        var firstTemplate = new FuncDataTemplate<object>((_, _) => new TextBlock());
        var secondTemplate = new FuncDataTemplate<object>((_, _) => new Border());

        _firstModel = new TemplateCell(new object(), _ => firstTemplate, null, null);
        _secondModel = new TemplateCell(new object(), _ => secondTemplate, null, null);
        _factory = new TreeDataGridElementFactory();
        _cell = new TreeDataGridTemplateCell();
        _host = new Decorator { Child = _cell };
        _window = new Window { Content = _host };

        _cell.Realize(_factory, null, _firstModel, 0, 0);
        _window.Show();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _window?.Close();
        _window = null;
        _host = null;
        _cell = null;
        _factory = null;
        _firstModel = null;
        _secondModel = null;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = OperationCount)]
    public IDataTemplate? SameTemplateProvider()
    {
        var cell = _cell!;
        var factory = _factory!;
        var host = _host!;
        var model = _firstModel!;

        for (var i = 0; i < OperationCount; ++i)
        {
            host.Child = null;
            cell.Unrealize();
            cell.Realize(factory, null, model, 0, 0);
            host.Child = cell;
        }

        return cell.ContentTemplate;
    }

    [Benchmark(OperationsPerInvoke = OperationCount)]
    public IDataTemplate? AlternatingTemplateProviders()
    {
        var cell = _cell!;
        var factory = _factory!;
        var host = _host!;
        var firstModel = _firstModel!;
        var secondModel = _secondModel!;

        for (var i = 0; i < OperationCount; ++i)
        {
            var useSecondTemplate = (i & 1) == 0;

            host.Child = null;
            cell.Unrealize();
            cell.Realize(factory, null, useSecondTemplate ? secondModel : firstModel,
                useSecondTemplate ? 3 : 0, 0);
            host.Child = cell;
        }

        return cell.ContentTemplate;
    }

    private sealed class BenchmarkApplication : Application
    {
    }
}
