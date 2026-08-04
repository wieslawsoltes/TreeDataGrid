using System;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.TreeDataGridTests;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.Primitives;

public class TreeDataGridFirstLayoutTests
{
    private readonly ITestOutputHelper _output;

    public TreeDataGridFirstLayoutTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaTheory(Timeout = 10000)]
    [InlineData(ColumnLayout.Fixed, 0.0)]
    [InlineData(ColumnLayout.Fixed, 0.5)]
    [InlineData(ColumnLayout.Auto, 0.0)]
    [InlineData(ColumnLayout.Auto, 0.5)]
    [InlineData(ColumnLayout.Star, 0.0)]
    [InlineData(ColumnLayout.Star, 0.5)]
    [InlineData(ColumnLayout.Mixed, 0.0)]
    [InlineData(ColumnLayout.Mixed, 0.5)]
    public void First_layout_pass_counts(ColumnLayout layout, double cacheLength)
    {
        var counters = new LayoutCounters();
        var items = new AvaloniaList<RowModel>(Enumerable.Range(0, 10_000)
            .Select(x => new RowModel(x, $"Item {x}{new string('x', x % 20)}")));
        var source = new FlatTreeDataGridSource<RowModel>(items);

        for (var i = 0; i < 6; ++i)
        {
            source.Columns.Add(new TextColumn<RowModel, string>(
                $"Column {i}",
                x => $"{x.Title}-{i}",
                GetWidth(layout, i)));
        }

        var target = new CountingTreeDataGrid(counters)
        {
            ElementFactory = new CountingElementFactory(counters),
            Source = source,
            Template = TreeDataGridTemplate(counters, cacheLength),
        };
        var root = new TestWindow(target, new Size(800, 500))
        {
            Styles =
            {
                new Style(x => x.Is<TreeDataGridRow>())
                {
                    Setters =
                    {
                        new Setter(
                            TreeDataGridRow.TemplateProperty,
                            RowTemplate(counters)),
                        new Setter(TreeDataGridRow.HeightProperty, 24.0),
                    }
                }
            }
        };

        source.Columns.LayoutInvalidated += (_, _) => ++counters.ColumnLayoutInvalidations;
        counters.Reset();
        root.UpdateLayout();
        var afterInitialUpdate = counters.ToString();
        Dispatcher.UIThread.RunJobs();
        var afterDispatcher = counters.ToString();
        root.UpdateLayout();

        _output.WriteLine($"Initial={afterInitialUpdate}");
        _output.WriteLine($"Dispatcher={afterDispatcher}");
        _output.WriteLine($"Final={counters}");
        _output.WriteLine($"Rows={target.RowsPresenter!.GetRealizedElements().Count()}");
        _output.WriteLine($"Widths={string.Join(",", Enumerable.Range(0, source.Columns.Count).Select(x => source.Columns[x].ActualWidth))}");

        Assert.NotEmpty(target.RowsPresenter.GetRealizedElements());
        for (var i = 0; i < source.Columns.Count; ++i)
            Assert.True(double.IsFinite(source.Columns[i].ActualWidth));

        var expectedCellMeasures = (layout, cacheLength) switch
        {
            (ColumnLayout.Fixed, 0) => 126,
            (ColumnLayout.Fixed, _) => 252,
            (ColumnLayout.Auto, 0) => 744,
            (ColumnLayout.Auto, _) => 1248,
            (ColumnLayout.Star, 0) => 246,
            (ColumnLayout.Star, _) => 492,
            (ColumnLayout.Mixed, 0) => 372,
            (ColumnLayout.Mixed, _) => 744,
            _ => throw new ArgumentOutOfRangeException(nameof(layout)),
        };

        Assert.Equal(expectedCellMeasures, counters.CellMeasures);
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

    private static IControlTemplate TreeDataGridTemplate(LayoutCounters counters, double cacheLength)
    {
        return new FuncControlTemplate<TreeDataGrid>((x, ns) =>
            new DockPanel
            {
                Children =
                {
                    new ScrollViewer
                    {
                        Name = "PART_HeaderScrollViewer",
                        Template = TestTemplates.ScrollViewerTemplate(),
                        Height = 24,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        [DockPanel.DockProperty] = Dock.Top,
                        Content = new CountingColumnHeadersPresenter(counters)
                        {
                            Name = "PART_ColumnHeadersPresenter",
                            [!TreeDataGridColumnHeadersPresenter.ElementFactoryProperty] =
                                x[!TreeDataGrid.ElementFactoryProperty],
                            [!TreeDataGridColumnHeadersPresenter.ItemsProperty] =
                                x[!TreeDataGrid.ColumnsProperty],
                        }.RegisterInNameScope(ns),
                    }.RegisterInNameScope(ns),
                    new ScrollViewer
                    {
                        Name = "PART_ScrollViewer",
                        Template = TestTemplates.ScrollViewerTemplate(),
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = new CountingRowsPresenter(counters)
                        {
                            Name = "PART_RowsPresenter",
                            CacheLength = cacheLength,
                            [!TreeDataGridRowsPresenter.ColumnsProperty] = x[!TreeDataGrid.ColumnsProperty],
                            [!TreeDataGridRowsPresenter.ElementFactoryProperty] =
                                x[!TreeDataGrid.ElementFactoryProperty],
                            [!TreeDataGridRowsPresenter.ItemsProperty] = x[!TreeDataGrid.RowsProperty],
                        }.RegisterInNameScope(ns),
                    }.RegisterInNameScope(ns),
                }
            });
    }

    private static IControlTemplate RowTemplate(LayoutCounters counters)
    {
        return new FuncControlTemplate<TreeDataGridRow>((x, ns) =>
            new CountingCellsPresenter(counters)
            {
                Name = "PART_CellsPresenter",
                [!TreeDataGridCellsPresenter.ElementFactoryProperty] =
                    x[!TreeDataGridRow.ElementFactoryProperty],
                [!TreeDataGridCellsPresenter.ItemsProperty] = x[!TreeDataGridRow.ColumnsProperty],
                [!TreeDataGridCellsPresenter.RowsProperty] = x[!TreeDataGridRow.RowsProperty],
            }.RegisterInNameScope(ns));
    }

    public enum ColumnLayout
    {
        Fixed,
        Auto,
        Star,
        Mixed,
    }

    private sealed class LayoutCounters
    {
        public int GridMeasures;
        public int GridArranges;
        public int RowsPresenterMeasures;
        public int RowsPresenterArranges;
        public int HeadersPresenterMeasures;
        public int HeadersPresenterArranges;
        public int CellsPresenterMeasures;
        public int CellsPresenterArranges;
        public int CellPresenterMeasuresWithSameConstraint;
        public int CellPresenterMeasuresFromFiniteToInfiniteWidth;
        public int CellPresenterMeasuresFromFiniteToWiderWidth;
        public int RowMeasures;
        public int RowArranges;
        public int CellMeasures;
        public int CellArranges;
        public int CellMeasuresWithInfiniteWidth;
        public int CellMeasuresWithInfiniteHeight;
        public int HeaderMeasures;
        public int HeaderArranges;
        public int ColumnLayoutInvalidations;

        public void Reset()
        {
            GridMeasures = GridArranges = 0;
            RowsPresenterMeasures = RowsPresenterArranges = 0;
            HeadersPresenterMeasures = HeadersPresenterArranges = 0;
            CellsPresenterMeasures = CellsPresenterArranges = 0;
            CellPresenterMeasuresWithSameConstraint = 0;
            CellPresenterMeasuresFromFiniteToInfiniteWidth = 0;
            CellPresenterMeasuresFromFiniteToWiderWidth = 0;
            RowMeasures = RowArranges = 0;
            CellMeasures = CellArranges = 0;
            CellMeasuresWithInfiniteWidth = CellMeasuresWithInfiniteHeight = 0;
            HeaderMeasures = HeaderArranges = 0;
            ColumnLayoutInvalidations = 0;
        }

        public override string ToString()
        {
            return $"Grid={GridMeasures}/{GridArranges}; " +
                $"RowsPresenter={RowsPresenterMeasures}/{RowsPresenterArranges}; " +
                $"HeadersPresenter={HeadersPresenterMeasures}/{HeadersPresenterArranges}; " +
                $"CellsPresenter={CellsPresenterMeasures}/{CellsPresenterArranges}; " +
                $"CellConstraints=Same:{CellPresenterMeasuresWithSameConstraint},FiniteToInf:{CellPresenterMeasuresFromFiniteToInfiniteWidth},FiniteToWider:{CellPresenterMeasuresFromFiniteToWiderWidth}; " +
                $"Rows={RowMeasures}/{RowArranges}; " +
                $"Cells={CellMeasures}/{CellArranges} (InfW={CellMeasuresWithInfiniteWidth},InfH={CellMeasuresWithInfiniteHeight}); " +
                $"Headers={HeaderMeasures}/{HeaderArranges}; " +
                $"ColumnInvalidations={ColumnLayoutInvalidations}";
        }
    }

    private sealed class CountingTreeDataGrid : TreeDataGrid
    {
        private readonly LayoutCounters _counters;

        public CountingTreeDataGrid(LayoutCounters counters) => _counters = counters;

        protected override Size MeasureOverride(Size availableSize)
        {
            ++_counters.GridMeasures;
            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ++_counters.GridArranges;
            return base.ArrangeOverride(finalSize);
        }

    }

    private sealed class CountingRowsPresenter : TreeDataGridRowsPresenter
    {
        private readonly LayoutCounters _counters;

        public CountingRowsPresenter(LayoutCounters counters) => _counters = counters;

        protected override Size MeasureOverride(Size availableSize)
        {
            ++_counters.RowsPresenterMeasures;
            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ++_counters.RowsPresenterArranges;
            return base.ArrangeOverride(finalSize);
        }
    }

    private sealed class CountingColumnHeadersPresenter : TreeDataGridColumnHeadersPresenter
    {
        private readonly LayoutCounters _counters;

        public CountingColumnHeadersPresenter(LayoutCounters counters) => _counters = counters;

        protected override Size MeasureOverride(Size availableSize)
        {
            ++_counters.HeadersPresenterMeasures;
            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ++_counters.HeadersPresenterArranges;
            return base.ArrangeOverride(finalSize);
        }
    }

    private sealed class CountingCellsPresenter : TreeDataGridCellsPresenter
    {
        private readonly LayoutCounters _counters;

        public CountingCellsPresenter(LayoutCounters counters) => _counters = counters;

        protected override Size MeasureOverride(Size availableSize)
        {
            ++_counters.CellsPresenterMeasures;
            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ++_counters.CellsPresenterArranges;
            return base.ArrangeOverride(finalSize);
        }

        protected override Size MeasureElement(int index, Control element, Size availableSize)
        {
            if (LayoutInformation.GetPreviousMeasureConstraint(element) is { } previous)
            {
                if (previous == availableSize)
                    ++_counters.CellPresenterMeasuresWithSameConstraint;
                else if (double.IsFinite(previous.Width) && double.IsPositiveInfinity(availableSize.Width))
                    ++_counters.CellPresenterMeasuresFromFiniteToInfiniteWidth;
                else if (double.IsFinite(previous.Width) && availableSize.Width > previous.Width)
                    ++_counters.CellPresenterMeasuresFromFiniteToWiderWidth;
            }

            return base.MeasureElement(index, element, availableSize);
        }
    }

    private sealed class CountingElementFactory : TreeDataGridElementFactory
    {
        private readonly LayoutCounters _counters;

        public CountingElementFactory(LayoutCounters counters) => _counters = counters;

        protected override Control CreateElement(object? data)
        {
            return data switch
            {
                ICell => new CountingTextCell(_counters),
                IColumn => new CountingColumnHeader(_counters),
                IRow => new CountingRow(_counters),
                _ => base.CreateElement(data),
            };
        }

        protected override string GetDataRecycleKey(object? data)
        {
            return data switch
            {
                ICell => "counting-cell",
                IColumn => "counting-header",
                IRow => "counting-row",
                _ => base.GetDataRecycleKey(data),
            };
        }

        protected override string GetElementRecycleKey(Control element)
        {
            return element switch
            {
                CountingTextCell => "counting-cell",
                CountingColumnHeader => "counting-header",
                CountingRow => "counting-row",
                _ => base.GetElementRecycleKey(element),
            };
        }
    }

    private sealed class CountingRow : TreeDataGridRow
    {
        private readonly LayoutCounters _counters;

        public CountingRow(LayoutCounters counters) => _counters = counters;

        protected override Size MeasureOverride(Size availableSize)
        {
            ++_counters.RowMeasures;
            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ++_counters.RowArranges;
            return base.ArrangeOverride(finalSize);
        }
    }

    private sealed class CountingTextCell : TreeDataGridTextCell
    {
        private readonly LayoutCounters _counters;

        public CountingTextCell(LayoutCounters counters) => _counters = counters;

        protected override Size MeasureOverride(Size availableSize)
        {
            ++_counters.CellMeasures;
            if (double.IsPositiveInfinity(availableSize.Width))
                ++_counters.CellMeasuresWithInfiniteWidth;
            if (double.IsPositiveInfinity(availableSize.Height))
                ++_counters.CellMeasuresWithInfiniteHeight;
            var width = ((Value?.Length ?? 0) * 4) + 8;
            return new Size(Math.Min(width, availableSize.Width), 24);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ++_counters.CellArranges;
            return finalSize;
        }
    }

    private sealed class CountingColumnHeader : TreeDataGridColumnHeader
    {
        private readonly LayoutCounters _counters;

        public CountingColumnHeader(LayoutCounters counters) => _counters = counters;

        protected override Size MeasureOverride(Size availableSize)
        {
            ++_counters.HeaderMeasures;
            var width = ((Header?.ToString()?.Length ?? 0) * 4) + 8;
            return new Size(Math.Min(width, availableSize.Width), 24);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ++_counters.HeaderArranges;
            return finalSize;
        }
    }

    private sealed record RowModel(int Id, string Title);
}
