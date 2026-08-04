using System.Collections.Generic;
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

    [IterationSetup(Target = nameof(VerticalSmallScrolls))]
    public void SetupVerticalSmallScrolls() => CreateGrid(rowCount: 10_000, columnCount: 12);

    [IterationCleanup(Target = nameof(VerticalSmallScrolls))]
    public void CleanupVerticalSmallScrolls() => CleanupGrid();

    [Benchmark]
    public int VerticalSmallScrolls()
    {
        var scroll = _scroll!;

        for (var i = 1; i <= 500; ++i)
        {
            scroll.Offset = new Vector(0, i);
            _grid!.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(HorizontalSmallScrolls))]
    public void SetupHorizontalSmallScrolls() => CreateGrid(rowCount: 1_000, columnCount: 200);

    [IterationCleanup(Target = nameof(HorizontalSmallScrolls))]
    public void CleanupHorizontalSmallScrolls() => CleanupGrid();

    [Benchmark]
    public int HorizontalSmallScrolls()
    {
        var scroll = _scroll!;

        for (var i = 1; i <= 200; ++i)
        {
            scroll.Offset = new Vector(i, 0);
            _grid!.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(CollectionInsertRemoveBurst))]
    public void SetupCollectionInsertRemoveBurst() => CreateGrid(rowCount: 10_000, columnCount: 12);

    [IterationCleanup(Target = nameof(CollectionInsertRemoveBurst))]
    public void CleanupCollectionInsertRemoveBurst() => CleanupGrid();

    [Benchmark]
    public int CollectionInsertRemoveBurst()
    {
        var items = _items!;

        for (var i = 0; i < 100; ++i)
        {
            items.Insert(2, new RowModel(-i, $"Inserted {i}"));
            _grid!.UpdateLayout();
            items.RemoveAt(2);
            _grid.UpdateLayout();
        }

        return _grid!.RowsPresenter!.GetRealizedElements().Count();
    }

    [IterationSetup(Target = nameof(DetachReattach))]
    public void SetupDetachReattach() => CreateGrid(rowCount: 10_000, columnCount: 50);

    [IterationCleanup(Target = nameof(DetachReattach))]
    public void CleanupDetachReattach() => CleanupGrid();

    [Benchmark]
    public int DetachReattach()
    {
        var grid = _grid!;
        var window = _window!;

        for (var i = 0; i < 50; ++i)
        {
            window.Content = null;
            window.UpdateLayout();
            window.Content = grid;
            window.UpdateLayout();
        }

        return grid.RowsPresenter!.GetRealizedElements().Count();
    }

    private void CreateGrid(int rowCount, int columnCount)
    {
        _items = new AvaloniaList<RowModel>(Enumerable.Range(0, rowCount)
            .Select(x => new RowModel(x, $"Item {x}")));

        var source = new FlatTreeDataGridSource<RowModel>(_items)
        {
            Columns =
            {
                new TextColumn<RowModel, int>("ID", x => x.Id, new GridLength(80)),
            }
        };

        for (var i = 1; i < columnCount; ++i)
        {
            var column = i;
            source.Columns.Add(new TextColumn<RowModel, string>(
                $"Column {column}",
                x => $"{x.Title}-{column}",
                new GridLength(100)));
        }

        _grid = new TreeDataGridControl
        {
            Source = source,
            Template = TreeDataGridTemplate(),
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
                        new Setter(TreeDataGridRow.TemplateProperty, RowTemplate()),
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
        _window = null;
        _grid = null;
        _scroll = null;
        _items = null;
    }

    private static IControlTemplate TreeDataGridTemplate()
    {
        return new FuncControlTemplate<TreeDataGridControl>((x, ns) =>
            new ScrollViewer
            {
                Name = "PART_ScrollViewer",
                Template = ScrollViewerTemplate(),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new TreeDataGridRowsPresenter
                {
                    Name = "PART_RowsPresenter",
                    [!TreeDataGridRowsPresenter.ColumnsProperty] = x[!TreeDataGridControl.ColumnsProperty],
                    [!TreeDataGridRowsPresenter.ElementFactoryProperty] = x[!TreeDataGridControl.ElementFactoryProperty],
                    [!TreeDataGridRowsPresenter.ItemsProperty] = x[!TreeDataGridControl.RowsProperty],
                }.RegisterInNameScope(ns),
            }.RegisterInNameScope(ns));
    }

    private static IControlTemplate RowTemplate()
    {
        return new FuncControlTemplate<TreeDataGridRow>((x, ns) =>
            new TreeDataGridCellsPresenter
            {
                Name = "PART_CellsPresenter",
                [!TreeDataGridCellsPresenter.ElementFactoryProperty] = x[!TreeDataGridRow.ElementFactoryProperty],
                [!TreeDataGridCellsPresenter.ItemsProperty] = x[!TreeDataGridRow.ColumnsProperty],
                [!TreeDataGridCellsPresenter.RowsProperty] = x[!TreeDataGridRow.RowsProperty],
            }.RegisterInNameScope(ns));
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

    private sealed record RowModel(int Id, string Title);
}
