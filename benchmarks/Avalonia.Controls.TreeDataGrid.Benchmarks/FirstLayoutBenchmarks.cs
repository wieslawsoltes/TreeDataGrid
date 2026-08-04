using System;
using System.ComponentModel;
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
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 30, iterationCount: 30)]
public class FirstLayoutBenchmarks
{
    private const int FirstLayoutOperations = 16;
    private AppBuilder? _appBuilder;
    private Window[]? _windows;
    private TreeDataGridControl[]? _grids;
    private Window[]? _legacyWindows;
    private TreeDataGridControl[]? _legacyGrids;

    [Params(ColumnLayout.Fixed, ColumnLayout.Auto, ColumnLayout.Star, ColumnLayout.Mixed)]
    public ColumnLayout Layout { get; set; }

    [Params(0.0, 0.5)]
    public double CacheLength { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _appBuilder = AppBuilder.Configure<BenchmarkApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
        _appBuilder.SetupWithoutStarting();
    }

    [IterationSetup(Target = nameof(ResolvedWidthConstraint))]
    public void ResolvedIterationSetup()
    {
        _windows = new Window[FirstLayoutOperations];
        _grids = new TreeDataGridControl[FirstLayoutOperations];

        for (var i = 0; i < FirstLayoutOperations; ++i)
            (_windows[i], _grids[i]) = CreateGrid(useLegacyNaturalWidthProbe: false);
    }

    [IterationSetup(Target = nameof(LegacyNaturalWidthProbe))]
    public void LegacyIterationSetup()
    {
        _legacyWindows = new Window[FirstLayoutOperations];
        _legacyGrids = new TreeDataGridControl[FirstLayoutOperations];

        for (var i = 0; i < FirstLayoutOperations; ++i)
            (_legacyWindows[i], _legacyGrids[i]) = CreateGrid(useLegacyNaturalWidthProbe: true);
    }

    [IterationCleanup(Target = nameof(ResolvedWidthConstraint))]
    public void ResolvedIterationCleanup()
    {
        if (_windows is not null)
        {
            foreach (var window in _windows)
                window.Close();
        }

        Dispatcher.UIThread.RunJobs();
        _windows = null;
        _grids = null;
    }

    [IterationCleanup(Target = nameof(LegacyNaturalWidthProbe))]
    public void LegacyIterationCleanup()
    {
        if (_legacyWindows is not null)
        {
            foreach (var window in _legacyWindows)
                window.Close();
        }

        Dispatcher.UIThread.RunJobs();
        _legacyWindows = null;
        _legacyGrids = null;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = FirstLayoutOperations)]
    public int LegacyNaturalWidthProbe() => RunFirstLayout(_legacyWindows!, _legacyGrids!);

    [Benchmark(OperationsPerInvoke = FirstLayoutOperations)]
    public int ResolvedWidthConstraint() => RunFirstLayout(_windows!, _grids!);

    private static int RunFirstLayout(Window[] windows, TreeDataGridControl[] grids)
    {
        var checksum = 0;

        for (var operation = 0; operation < FirstLayoutOperations; ++operation)
        {
            var window = windows[operation];
            var grid = grids[operation];

            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var columns = grid.Columns!;
            checksum += grid.RowsPresenter!.GetRealizedElements().Count();

            for (var i = 0; i < columns.Count; ++i)
                checksum += (int)columns[i].ActualWidth;
        }

        return checksum;
    }

    private (Window window, TreeDataGridControl grid) CreateGrid(bool useLegacyNaturalWidthProbe)
    {
        var items = new AvaloniaList<RowModel>(Enumerable.Range(0, 1_000)
            .Select(x => new RowModel(x, $"Item {x}{new string('x', x % 20)}")));
        var source = new FlatTreeDataGridSource<RowModel>(items);

        for (var i = 0; i < 6; ++i)
        {
            var column = i;
            IColumn<RowModel> columnModel = new TextColumn<RowModel, string>(
                $"Column {column}",
                x => $"{x.Title}-{column}",
                GetWidth(Layout, column));
            source.Columns.Add(useLegacyNaturalWidthProbe ?
                new LegacyNaturalWidthColumn(columnModel) :
                columnModel);
        }

        var grid = new TreeDataGridControl
        {
            ElementFactory = new SyntheticElementFactory(),
            Source = source,
            Template = TreeDataGridTemplate(CacheLength),
        };
        var window = new Window
        {
            Width = 800,
            Height = 500,
            Content = grid,
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

        return (window, grid);
    }

    private static GridLength GetWidth(ColumnLayout layout, int index)
    {
        return layout switch
        {
            ColumnLayout.Fixed => new GridLength(124),
            ColumnLayout.Auto => GridLength.Auto,
            ColumnLayout.Star => new GridLength(1, GridUnitType.Star),
            ColumnLayout.Mixed => (index % 3) switch
            {
                0 => GridLength.Auto,
                1 => new GridLength(1, GridUnitType.Star),
                _ => new GridLength(124),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(layout)),
        };
    }

    private static IControlTemplate TreeDataGridTemplate(double cacheLength)
    {
        return new FuncControlTemplate<TreeDataGridControl>((x, ns) =>
            new DockPanel
            {
                Children =
                {
                    new ScrollViewer
                    {
                        Name = "PART_HeaderScrollViewer",
                        Template = ScrollViewerTemplate(),
                        Height = 24,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        [DockPanel.DockProperty] = Dock.Top,
                        Content = new TreeDataGridColumnHeadersPresenter
                        {
                            Name = "PART_ColumnHeadersPresenter",
                            [!TreeDataGridColumnHeadersPresenter.ElementFactoryProperty] =
                                x[!TreeDataGridControl.ElementFactoryProperty],
                            [!TreeDataGridColumnHeadersPresenter.ItemsProperty] =
                                x[!TreeDataGridControl.ColumnsProperty],
                        }.RegisterInNameScope(ns),
                    }.RegisterInNameScope(ns),
                    new ScrollViewer
                    {
                        Name = "PART_ScrollViewer",
                        Template = ScrollViewerTemplate(),
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = new TreeDataGridRowsPresenter
                        {
                            Name = "PART_RowsPresenter",
                            CacheLength = cacheLength,
                            [!TreeDataGridRowsPresenter.ColumnsProperty] = x[!TreeDataGridControl.ColumnsProperty],
                            [!TreeDataGridRowsPresenter.ElementFactoryProperty] =
                                x[!TreeDataGridControl.ElementFactoryProperty],
                            [!TreeDataGridRowsPresenter.ItemsProperty] = x[!TreeDataGridControl.RowsProperty],
                        }.RegisterInNameScope(ns),
                    }.RegisterInNameScope(ns),
                }
            });
    }

    private static IControlTemplate RowTemplate()
    {
        return new FuncControlTemplate<TreeDataGridRow>((x, ns) =>
            new TreeDataGridCellsPresenter
            {
                Name = "PART_CellsPresenter",
                [!TreeDataGridCellsPresenter.ElementFactoryProperty] =
                    x[!TreeDataGridRow.ElementFactoryProperty],
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

    public enum ColumnLayout
    {
        Fixed,
        Auto,
        Star,
        Mixed,
    }

    private sealed class SyntheticElementFactory : TreeDataGridElementFactory
    {
        protected override Control CreateElement(object? data)
        {
            return data switch
            {
                ICell => new SyntheticTextCell(),
                IColumn => new SyntheticColumnHeader(),
                _ => base.CreateElement(data),
            };
        }

        protected override string GetDataRecycleKey(object? data)
        {
            return data switch
            {
                ICell => "synthetic-cell",
                IColumn => "synthetic-header",
                _ => base.GetDataRecycleKey(data),
            };
        }

        protected override string GetElementRecycleKey(Control element)
        {
            return element switch
            {
                SyntheticTextCell => "synthetic-cell",
                SyntheticColumnHeader => "synthetic-header",
                _ => base.GetElementRecycleKey(element),
            };
        }
    }

    private sealed class SyntheticTextCell : TreeDataGridTextCell
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var width = ((Value?.Length ?? 0) * 4) + 8;
            return new Size(Math.Min(width, availableSize.Width), 24);
        }
    }

    private sealed class SyntheticColumnHeader : TreeDataGridColumnHeader
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var width = ((Header?.ToString()?.Length ?? 0) * 4) + 8;
            return new Size(Math.Min(width, availableSize.Width), 24);
        }
    }

    private sealed class BenchmarkApplication : Application
    {
    }

    private sealed class LegacyNaturalWidthColumn : IColumn<RowModel>, IUpdateColumnLayout
    {
        private readonly IColumn<RowModel> _column;
        private readonly IUpdateColumnLayout _layout;

        public LegacyNaturalWidthColumn(IColumn<RowModel> column)
        {
            _column = column;
            _layout = (IUpdateColumnLayout)column;
        }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => _column.PropertyChanged += value;
            remove => _column.PropertyChanged -= value;
        }

        public double ActualWidth => _column.ActualWidth;
        public bool? CanUserResize => _column.CanUserResize;
        public object? Header => _column.Header;
        public GridLength Width => _column.Width;

        public ListSortDirection? SortDirection
        {
            get => _column.SortDirection;
            set => _column.SortDirection = value;
        }

        public object? Tag
        {
            get => _column.Tag;
            set => _column.Tag = value;
        }

        public double MinActualWidth => _layout.MinActualWidth;
        public double MaxActualWidth => _layout.MaxActualWidth;
        public bool StarWidthWasConstrained => _layout.StarWidthWasConstrained;

        public ICell CreateCell(IRow<RowModel> row) => _column.CreateCell(row);

        public Comparison<RowModel?>? GetComparison(ListSortDirection direction) =>
            _column.GetComparison(direction);

        public double CellMeasured(double width, int rowIndex) =>
            _layout.CellMeasured(width, rowIndex);

        public void CalculateStarWidth(double availableWidth, double totalStars) =>
            _layout.CalculateStarWidth(availableWidth, totalStars);

        public bool CommitActualWidth() => _layout.CommitActualWidth();

        public void SetWidth(GridLength width) => _layout.SetWidth(width);
    }

    private sealed record RowModel(int Id, string Title);
}
