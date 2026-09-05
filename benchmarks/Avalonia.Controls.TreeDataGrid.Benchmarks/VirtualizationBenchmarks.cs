using System;
using System.Collections.Generic;
using Avalonia.Controls.Adapters;
using Core = global::TreeDataGridCore;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TreeDataGridControl = Avalonia.Controls.TreeDataGrid;

namespace TreeDataGridBenchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 5, iterationCount: 10)]
public class VirtualizationBenchmarks
{
    private const int VerticalScrollOperations = 10_000;
    private const int StationaryLayoutOperations = 25_000;
    private const int HorizontalScrollOperations = 6_000;
    private const int HorizontalColumnOperations = 1_000;
    private const int CollectionEditOperations = 1_500;
    private const int CollectionMoveOperations = 6_000;
    private const int DetachReattachOperations = 200;

    [Params(false, true)]
    public bool NeutralSource { get; set; }

    private IDisposable? _sourceLifetime;
    private AppBuilder? _appBuilder;
    private Window? _window;
    private TreeDataGridControl? _grid;
    private ScrollViewer? _scroll;
    private AvaloniaList<RowModel>? _items;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _appBuilder = AppBuilder.Configure<BenchmarkApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
        _appBuilder.SetupWithoutStarting();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _window?.Close();
        _window = null;
        _grid = null;
        _scroll = null;
        _items = null;
    }

    [Benchmark]
    public int CreateAndLayoutGrid()
    {
        CreateGrid(rowCount: 1_000, columnCount: 12);
        try { return _grid!.RowsPresenter!.GetRealizedElements().Count(); }
        finally { CleanupGrid(); }
    }

    [IterationSetup(Target = nameof(VerticalSmallScrolls))]
    public void SetupVerticalSmallScrolls() => CreateGrid(rowCount: 10_000, columnCount: 12);

    [IterationCleanup(Target = nameof(VerticalSmallScrolls))]
    public void CleanupVerticalSmallScrolls() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = VerticalScrollOperations)]
    public int VerticalSmallScrolls()
    {
        var scroll = _scroll!;

        for (var i = 1; i <= VerticalScrollOperations; ++i)
        {
            scroll.Offset = new Vector(0, i);
            _grid!.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(VerticalUnbatchedSmallScrolls))]
    public void SetupVerticalUnbatchedSmallScrolls() =>
        CreateGrid(rowCount: 10_000, columnCount: 12, alwaysMeasureViewportChanges: true);

    [IterationCleanup(Target = nameof(VerticalUnbatchedSmallScrolls))]
    public void CleanupVerticalUnbatchedSmallScrolls() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = VerticalScrollOperations)]
    public int VerticalUnbatchedSmallScrolls()
    {
        var scroll = _scroll!;

        for (var i = 1; i <= VerticalScrollOperations; ++i)
        {
            scroll.Offset = new Vector(0, i);
            _grid!.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(ForcedStationaryVerticalLayouts))]
    public void SetupForcedStationaryVerticalLayouts() => CreateGrid(rowCount: 10_000, columnCount: 12);

    [IterationCleanup(Target = nameof(ForcedStationaryVerticalLayouts))]
    public void CleanupForcedStationaryVerticalLayouts() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = StationaryLayoutOperations)]
    public int ForcedStationaryVerticalLayouts()
    {
        var grid = _grid!;
        var presenter = grid.RowsPresenter!;

        for (var i = 0; i < StationaryLayoutOperations; ++i)
        {
            presenter.InvalidateMeasure();
            grid.UpdateLayout();
        }

        return presenter.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(VerticalRowScrolls))]
    public void SetupVerticalRowScrolls() => CreateGrid(rowCount: 10_000, columnCount: 12);

    [IterationCleanup(Target = nameof(VerticalRowScrolls))]
    public void CleanupVerticalRowScrolls() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = VerticalScrollOperations)]
    public int VerticalRowScrolls()
    {
        var grid = _grid!;
        var presenter = grid.RowsPresenter!;
        var scroll = _scroll!;

        for (var i = 1; i <= VerticalScrollOperations; ++i)
        {
            scroll.Offset = new Vector(0, i * 24);
            presenter.InvalidateMeasure();
            grid.UpdateLayout();
        }

        return presenter.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(VerticalBufferedSmallScrolls))]
    public void SetupVerticalBufferedSmallScrolls() =>
        CreateGrid(rowCount: 10_000, columnCount: 12, cacheLength: 0.1);

    [IterationCleanup(Target = nameof(VerticalBufferedSmallScrolls))]
    public void CleanupVerticalBufferedSmallScrolls() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = VerticalScrollOperations)]
    public int VerticalBufferedSmallScrolls()
    {
        var scroll = _scroll!;

        for (var i = 1; i <= VerticalScrollOperations; ++i)
        {
            scroll.Offset = new Vector(0, i);
            _grid!.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(VerticalJitterScrolls))]
    public void SetupVerticalJitterScrolls() => CreateGrid(rowCount: 10_000, columnCount: 12);

    [IterationCleanup(Target = nameof(VerticalJitterScrolls))]
    public void CleanupVerticalJitterScrolls() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = VerticalScrollOperations)]
    public int VerticalJitterScrolls()
    {
        var scroll = _scroll!;

        for (var i = 0; i < VerticalScrollOperations; ++i)
        {
            scroll.Offset = new Vector(0, (i & 1) == 0 ? 100 : 0);
            _grid!.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(VerticalBufferedJitterScrolls))]
    public void SetupVerticalBufferedJitterScrolls() =>
        CreateGrid(rowCount: 10_000, columnCount: 12, cacheLength: 0.1);

    [IterationCleanup(Target = nameof(VerticalBufferedJitterScrolls))]
    public void CleanupVerticalBufferedJitterScrolls() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = VerticalScrollOperations)]
    public int VerticalBufferedJitterScrolls()
    {
        var scroll = _scroll!;

        for (var i = 0; i < VerticalScrollOperations; ++i)
        {
            scroll.Offset = new Vector(0, (i & 1) == 0 ? 100 : 0);
            _grid!.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(HorizontalSmallScrolls))]
    public void SetupHorizontalSmallScrolls() => CreateGrid(rowCount: 1_000, columnCount: 200);

    [IterationCleanup(Target = nameof(HorizontalSmallScrolls))]
    public void CleanupHorizontalSmallScrolls() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = HorizontalScrollOperations)]
    public int HorizontalSmallScrolls()
    {
        var scroll = _scroll!;

        for (var i = 1; i <= HorizontalScrollOperations; ++i)
        {
            scroll.Offset = new Vector(i, 0);
            _grid!.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(HorizontalUnbatchedSmallScrolls))]
    public void SetupHorizontalUnbatchedSmallScrolls() =>
        CreateGrid(rowCount: 1_000, columnCount: 200, alwaysMeasureColumnViewportChanges: true);

    [IterationCleanup(Target = nameof(HorizontalUnbatchedSmallScrolls))]
    public void CleanupHorizontalUnbatchedSmallScrolls() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = HorizontalScrollOperations)]
    public int HorizontalUnbatchedSmallScrolls()
    {
        var scroll = _scroll!;

        for (var i = 1; i <= HorizontalScrollOperations; ++i)
        {
            scroll.Offset = new Vector(i, 0);
            _grid!.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(HorizontalColumnScrolls))]
    public void SetupHorizontalColumnScrolls() => CreateGrid(rowCount: 1_000, columnCount: 200);

    [IterationCleanup(Target = nameof(HorizontalColumnScrolls))]
    public void CleanupHorizontalColumnScrolls() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = HorizontalColumnOperations)]
    public int HorizontalColumnScrolls()
    {
        var scroll = _scroll!;

        for (var i = 0; i < HorizontalColumnOperations; ++i)
        {
            scroll.Offset = new Vector((i & 1) == 0 ? 100 : 200, 0);
            _grid!.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(HorizontalFarColumnScrolls))]
    public void SetupHorizontalFarColumnScrolls()
    {
        CreateGrid(rowCount: 1_000, columnCount: 1_000);
        _scroll!.Offset = new Vector(90_000, 0);
        _grid!.UpdateLayout();
    }

    [IterationCleanup(Target = nameof(HorizontalFarColumnScrolls))]
    public void CleanupHorizontalFarColumnScrolls() => CleanupGrid();

    // The initial jump is outside the timed region. Exercise the same recycling workload
    // near column 900, where a scan from column zero is repeated by every realized row.
    [Benchmark(OperationsPerInvoke = HorizontalColumnOperations)]
    public int HorizontalFarColumnScrolls()
    {
        var scroll = _scroll!;
        for (var i = 0; i < HorizontalColumnOperations; ++i)
        {
            scroll.Offset = new Vector((i & 1) == 0 ? 90_100 : 90_000, 0);
            _grid!.UpdateLayout();
        }
        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(CollectionInsertRemoveBurst))]
    public void SetupCollectionInsertRemoveBurst() => CreateGrid(rowCount: 10_000, columnCount: 12);

    [IterationCleanup(Target = nameof(CollectionInsertRemoveBurst))]
    public void CleanupCollectionInsertRemoveBurst() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = CollectionEditOperations)]
    public int CollectionInsertRemoveBurst()
    {
        var items = _items!;

        for (var i = 0; i < CollectionEditOperations; ++i)
        {
            items.Insert(2, new RowModel(-i, $"Inserted {i}"));
            _grid!.UpdateLayout();
            items.RemoveAt(2);
            _grid.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(CollectionMoveBurst))]
    public void SetupCollectionMoveBurst() => CreateGrid(rowCount: 10_000, columnCount: 12);

    [IterationCleanup(Target = nameof(CollectionMoveBurst))]
    public void CleanupCollectionMoveBurst() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = CollectionMoveOperations)]
    public int CollectionMoveBurst()
    {
        var items = _items!;

        for (var i = 0; i < CollectionMoveOperations; ++i)
        {
            items.Move(2, 12);
            _grid!.UpdateLayout();
            items.Move(12, 2);
            _grid.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(DetachReattach))]
    public void SetupDetachReattach() => CreateGrid(rowCount: 10_000, columnCount: 50);

    [IterationCleanup(Target = nameof(DetachReattach))]
    public void CleanupDetachReattach() => CleanupGrid();

    [Benchmark(OperationsPerInvoke = DetachReattachOperations)]
    public int DetachReattach()
    {
        var grid = _grid!;
        var window = _window!;

        for (var i = 0; i < DetachReattachOperations; ++i)
        {
            window.Content = null;
            window.UpdateLayout();
            window.Content = grid;
            window.UpdateLayout();
        }

        return grid.RowsPresenter!.GetRealizedElements().Count();
    }

    private void CreateGrid(
        int rowCount,
        int columnCount,
        double cacheLength = 0,
        bool alwaysMeasureViewportChanges = false,
        bool alwaysMeasureColumnViewportChanges = false)
    {
        _items = new AvaloniaList<RowModel>(Enumerable.Range(0, rowCount)
            .Select(x => new RowModel(x, $"Item {x}")));

        ITreeDataGridSource? source = null;
        Core.ITreeDataGridSource? neutralModel = null;
        if (NeutralSource)
        {
            var model = new Core.FlatTreeDataGridSource<RowModel>(_items);
            model.Columns.Add(new Core.Models.TextColumn<RowModel, int>("ID", x => x.Id, new Core.GridLength(80)));
            for (var i = 1; i < columnCount; ++i)
            {
                var column = i;
                model.Columns.Add(new Core.Models.TextColumn<RowModel, string>($"Column {column}", x => $"{x.Title}-{column}", new Core.GridLength(100)));
            }
            neutralModel = model;
            _sourceLifetime = model;
        }
        else
        {
        var legacySource = new FlatTreeDataGridSource<RowModel>(_items)
        {
            Columns =
            {
                new TextColumn<RowModel, int>("ID", x => x.Id, new GridLength(80)),
            }
        };

        for (var i = 1; i < columnCount; ++i)
        {
            var column = i;
            legacySource.Columns.Add(new TextColumn<RowModel, string>(
                $"Column {column}",
                x => $"{x.Title}-{column}",
                new GridLength(100)));
        }

            source = legacySource;
            _sourceLifetime = legacySource;
        }

        _grid = new TreeDataGridControl
        {
            Source = source,
            Model = neutralModel,
            Template = TreeDataGridTemplate(cacheLength, alwaysMeasureViewportChanges),
        };

        _window = new Window
        {
            Width = 800,
            Height = 500,
            Content = _grid,
            Styles =
            {
                new Style(x => x.OfType<TreeDataGridRow>())
                {
                    Setters =
                    {
                        new Setter(
                            TreeDataGridRow.TemplateProperty,
                            RowTemplate(alwaysMeasureColumnViewportChanges)),
                        new Setter(TreeDataGridRow.HeightProperty, 24.0),
                    }
                }
            }
        };

        _window.Show();
        Dispatcher.UIThread.RunJobs();
        _window.UpdateLayout();
        _scroll = (ScrollViewer)_grid.GetTemplateChildren().Single(x => x.Name == "PART_ScrollViewer");
    }

    private void CleanupGrid()
    {
        _window?.Close();
        Dispatcher.UIThread.RunJobs();
        _sourceLifetime?.Dispose();
        _sourceLifetime = null;
        _window = null;
        _grid = null;
        _scroll = null;
        _items = null;
    }

    private static IControlTemplate TreeDataGridTemplate(
        double cacheLength,
        bool alwaysMeasureViewportChanges)
    {
        return new FuncControlTemplate<TreeDataGridControl>((x, ns) =>
        {
            var rowsPresenter = alwaysMeasureViewportChanges ?
                new AlwaysMeasureRowsPresenter() :
                new TreeDataGridRowsPresenter();
            rowsPresenter.Name = "PART_RowsPresenter";
            rowsPresenter.CacheLength = cacheLength;
            rowsPresenter[!TreeDataGridRowsPresenter.ColumnsProperty] = x[!TreeDataGridControl.ColumnsProperty];
            rowsPresenter[!TreeDataGridRowsPresenter.ElementFactoryProperty] = x[!TreeDataGridControl.ElementFactoryProperty];
            rowsPresenter[!TreeDataGridRowsPresenter.ItemsProperty] = x[!TreeDataGridControl.RowsProperty];
            rowsPresenter.RegisterInNameScope(ns);

            return new ScrollViewer
            {
                Name = "PART_ScrollViewer",
                Template = ScrollViewerTemplate(),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = rowsPresenter,
            }.RegisterInNameScope(ns);
        });
    }

    private static IControlTemplate RowTemplate(bool alwaysMeasureViewportChanges)
    {
        return new FuncControlTemplate<TreeDataGridRow>((x, ns) =>
        {
            var cellsPresenter = alwaysMeasureViewportChanges ?
                new AlwaysMeasureCellsPresenter() :
                new TreeDataGridCellsPresenter();
            cellsPresenter.Name = "PART_CellsPresenter";
            cellsPresenter[!TreeDataGridCellsPresenter.ElementFactoryProperty] = x[!TreeDataGridRow.ElementFactoryProperty];
            cellsPresenter[!TreeDataGridCellsPresenter.ItemsProperty] = x[!TreeDataGridRow.ColumnsProperty];
            cellsPresenter[!TreeDataGridCellsPresenter.RowsProperty] = x[!TreeDataGridRow.RowsProperty];

            return cellsPresenter.RegisterInNameScope(ns);
        });
    }

    private static IControlTemplate ScrollViewerTemplate()
    {
        return new FuncControlTemplate<ScrollViewer>((x, ns) =>
            new ScrollContentPresenter
            {
                Name = "PART_ContentPresenter",
                [~ContentPresenter.ContentProperty] = x[~ContentControl.ContentProperty],
                [~~ScrollContentPresenter.OffsetProperty] = x[~~ScrollViewer.OffsetProperty],
            }.RegisterInNameScope(ns));
    }

    private sealed class BenchmarkApplication : Application
    {
    }

    private sealed class AlwaysMeasureRowsPresenter : TreeDataGridRowsPresenter
    {
        protected override bool NeedsMeasureForViewportChange(Rect measureViewport, Rect viewport) => true;
    }

    private sealed class AlwaysMeasureCellsPresenter : TreeDataGridCellsPresenter
    {
        protected override bool NeedsMeasureForViewportChange(Rect measureViewport, Rect viewport) => true;
    }

    private sealed record RowModel(int Id, string Title);
}
