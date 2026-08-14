using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Primitives
{
    public class TreeDataGridCellsPresenterTests
    {
        [AvaloniaFact(Timeout = 10000)]
        public void Creates_Initial_Cells()
        {
            var (target, _) = CreateTarget();

            AssertColumnIndexes(target, 0, 10);
            AssertRecyclable(target, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Updates_Column_ActualWidth()
        {
            var (target, _) = CreateTarget();

            for (var i = 0; i < target.Items!.Count; ++i)
            {
                var column = target.Items[i];
                Assert.Equal(i < 10 ? 10 : 0, column.ActualWidth);
            }
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Scrolls_Right_One_Cell()
        {
            var (target, scroll) = CreateTarget();
            
            scroll.Offset = new Vector(10, 0);
            Layout(target);

            AssertColumnIndexes(target, 1, 10);
            AssertRecyclable(target, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Full_Cell_Scroll_Reuses_Controls_Without_Tree_Reattachment()
        {
            var (target, scroll) = CreateTarget();
            var cells = target.RealizedElements.Cast<TreeDataGridCell>().ToHashSet();
            var logicalAttaches = 0;
            var logicalDetaches = 0;
            var visualAttaches = 0;
            var visualDetaches = 0;

            foreach (var cell in cells)
            {
                cell.AttachedToLogicalTree += (_, _) => ++logicalAttaches;
                cell.DetachedFromLogicalTree += (_, _) => ++logicalDetaches;
                cell.AttachedToVisualTree += (_, _) => ++visualAttaches;
                cell.DetachedFromVisualTree += (_, _) => ++visualDetaches;
            }

            scroll.Offset = new Vector(10, 0);
            Layout(target);

            Assert.Equal(cells, target.RealizedElements.Cast<TreeDataGridCell>().ToHashSet());
            Assert.Equal(0, logicalAttaches);
            Assert.Equal(0, logicalDetaches);
            Assert.Equal(0, visualAttaches);
            Assert.Equal(0, visualDetaches);
            AssertColumnIndexes(target, 1, 10);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Small_Scrolls_Within_Realized_Cells_Do_Not_Repeat_Measure()
        {
            var presenter = new CountingCellsPresenter();
            var (target, scroll) = CreateTarget(presenter: presenter);

            scroll.Offset = new Vector(1, 0);
            Layout(target);
            var measureCountAfterRealizingTrailingCell = presenter.MeasureCount;

            for (var offset = 2; offset <= 10; ++offset)
            {
                scroll.Offset = new Vector(offset, 0);
                Layout(target);
            }

            Assert.Equal(measureCountAfterRealizingTrailingCell, presenter.MeasureCount);
            AssertColumnIndexes(target, 0, 11);

            scroll.Offset = new Vector(11, 0);
            Layout(target);

            Assert.True(presenter.MeasureCount > measureCountAfterRealizingTrailingCell);
            AssertColumnIndexes(target, 1, 11);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Scrolls_Right_More_Than_A_Page()
        {
            var (target, scroll) = CreateTarget();

            scroll.Offset = new Vector(200, 0);
            Layout(target);

            AssertColumnIndexes(target, 20, 10);
            AssertRecyclable(target, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Scrolls_Left_More_Than_A_Page()
        {
            var (target, scroll) = CreateTarget();

            scroll.Offset = new Vector(200, 0);
            Layout(target);

            scroll.Offset = new Vector(0, 0);
            Layout(target);

            AssertColumnIndexes(target, 0, 10);
            AssertRecyclable(target, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Reuses_Realized_Cell_Controls_When_Row_Is_Immediately_Rebound()
        {
            var (target, _) = CreateTarget(rowCount: 2);
            var cells = target.RealizedElements.Cast<TreeDataGridCell>().ToList();
            var models = cells.Select(x => x.Model).ToList();

            target.Unrealize();

            Assert.Equal(cells, target.RealizedElements);
            Assert.All(cells, cell => Assert.Equal(0, cell.RowIndex));

            target.Realize(1);
            Layout(target);

            Assert.Equal(cells, target.RealizedElements);
            Assert.All(cells, cell => Assert.Equal(1, cell.RowIndex));
            Assert.All(cells.Zip(models), pair => Assert.NotSame(pair.Second, pair.First.Model));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Deferred_Row_Cells_Are_Recycled_Without_Tree_Reattachment_When_Finalized()
        {
            var (target, _) = CreateTarget(rowCount: 2);
            var cells = target.RealizedElements.Cast<TreeDataGridCell>().ToHashSet();
            var logicalAttaches = 0;
            var logicalDetaches = 0;
            var visualAttaches = 0;
            var visualDetaches = 0;

            foreach (var cell in cells)
            {
                cell.AttachedToLogicalTree += (_, _) => ++logicalAttaches;
                cell.DetachedFromLogicalTree += (_, _) => ++logicalDetaches;
                cell.AttachedToVisualTree += (_, _) => ++visualAttaches;
                cell.DetachedFromVisualTree += (_, _) => ++visualDetaches;
            }

            target.Unrealize();
            target.FinalizeUnrealize();

            Assert.Empty(target.RealizedElements);
            Assert.All(cells, cell =>
            {
                Assert.Equal(-1, cell.ColumnIndex);
                Assert.Equal(-1, cell.RowIndex);
                Assert.Null(cell.Model);
                Assert.False(cell.IsVisible);
                Assert.Contains(cell, target.GetLogicalChildren());
                Assert.Contains(cell, target.GetVisualChildren());
            });

            target.Realize(1);
            Layout(target);

            Assert.Equal(cells, target.RealizedElements.Cast<TreeDataGridCell>().ToHashSet());
            Assert.Equal(0, logicalAttaches);
            Assert.Equal(0, logicalDetaches);
            Assert.Equal(0, visualAttaches);
            Assert.Equal(0, visualDetaches);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Deferred_Row_Cells_Are_Recycled_When_Presenter_Detaches()
        {
            var (target, scroll) = CreateTarget(rowCount: 2);
            var cells = target.RealizedElements.Cast<TreeDataGridCell>().ToList();

            target.Unrealize();
            scroll.Content = null;
            scroll.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(target.RealizedElements);
            Assert.All(cells, cell =>
            {
                Assert.Equal(-1, cell.ColumnIndex);
                Assert.Equal(-1, cell.RowIndex);
                Assert.Null(cell.Model);
            });
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Deferred_Row_Cells_Are_Recycled_Before_Rows_Source_Changes()
        {
            var (target, _) = CreateTarget(rowCount: 2);
            var cells = target.RealizedElements.Cast<TreeDataGridCell>().ToList();

            target.Unrealize();
            target.Rows = null;

            Assert.Empty(target.RealizedElements);
            Assert.All(cells, cell =>
            {
                Assert.Equal(-1, cell.ColumnIndex);
                Assert.Equal(-1, cell.RowIndex);
                Assert.Null(cell.Model);
            });
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Moving_Columns_Preserves_Realized_Cell_Identity()
        {
            var columns = new MovableColumnList<Model>();
            for (var i = 0; i < 100; ++i)
                columns.Add(new LayoutTestColumn<Model>("Column " + i));

            var (target, _) = CreateTarget(columns);
            var cells = target.RealizedElements.ToList();

            columns.Move(2, 7);
            Layout(target);

            Assert.Same(cells[0], target.TryGetElement(0));
            Assert.Same(cells[1], target.TryGetElement(1));
            for (var oldIndex = 3; oldIndex <= 7; ++oldIndex)
                Assert.Same(cells[oldIndex], target.TryGetElement(oldIndex - 1));
            Assert.Same(cells[2], target.TryGetElement(7));
            Assert.Same(cells[8], target.TryGetElement(8));
            Assert.Same(cells[9], target.TryGetElement(9));
            AssertColumnIndexes(target, 0, 10);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void DesiredSize_Takes_Min_Star_Column_Width_Into_Account()
        {
            var minWidth = new ColumnOptions<Model>
            {
                MinWidth = new GridLength(100),
            };

            var columns = new ColumnList<Model>
            {
                new LayoutTestColumn<Model>("Col0", GridLength.Star, minWidth),
                new LayoutTestColumn<Model>("Col1", GridLength.Star, minWidth),
            };

            var (target, scroll) = CreateTarget(columns);

            Assert.Equal(200, target.DesiredSize.Width);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Star_Cells_Are_Measured_With_Final_Column_Width()
        {
            // Issue #70
            var columns = new ColumnList<Model>
            {
                new LayoutTestColumn<Model>("Col0", GridLength.Star),
                new LayoutTestColumn<Model>("Col1", GridLength.Star),
            };

            var (target, _) = CreateTarget(columns);

            for (var i = 0; i < target.RealizedElements.Count; ++i)
            {
                var cell = (LayoutTestCellControl)target.RealizedElements[i]!;

                Assert.Equal(
                    new[]
                    {
                        Size.Infinity,
                        new Size(0, double.PositiveInfinity),
                        new Size(50, double.PositiveInfinity),
                    },
                    cell!.MeasureConstraints);
            }
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Fixed_Cells_Are_Measured_Only_With_Final_Column_Width()
        {
            var columns = new ColumnList<Model>
            {
                new LayoutTestColumn<Model>("Col0", new GridLength(40)),
                new LayoutTestColumn<Model>("Col1", new GridLength(60)),
            };

            var (target, _) = CreateTarget(columns);

            Assert.Equal(
                new[] { new Size(40, double.PositiveInfinity) },
                ((LayoutTestCellControl)target.RealizedElements[0]!).MeasureConstraints);
            Assert.Equal(
                new[] { new Size(60, double.PositiveInfinity) },
                ((LayoutTestCellControl)target.RealizedElements[1]!).MeasureConstraints);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Auto_Cell_Natural_Measure_Cache_Is_Invalidated_With_The_Cell()
        {
            var columns = new ColumnList<Model>
            {
                new LayoutTestColumn<Model>("Col0", GridLength.Auto),
            };
            var (target, _) = CreateTarget(columns);
            var cell = (LayoutTestCellControl)target.RealizedElements[0]!;
            var initialConstraints = cell.MeasureConstraints.ToArray();

            Assert.Contains(Size.Infinity, initialConstraints);
            Assert.Contains(new Size(10, double.PositiveInfinity), initialConstraints);

            target.InvalidateMeasure();
            Layout(target);

            Assert.Equal(initialConstraints, cell.MeasureConstraints);

            cell.InvalidateMeasure();
            target.InvalidateMeasure();
            Layout(target);

            Assert.Equal(
                initialConstraints.Count(x => x == Size.Infinity) + 1,
                cell.MeasureConstraints.Count(x => x == Size.Infinity));
            Assert.Equal(new Size(10, double.PositiveInfinity), cell.MeasureConstraints[^1]);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Rebinding_Auto_Cell_Discards_Natural_Measure_Cache()
        {
            var columns = new ColumnList<Model>
            {
                new LayoutTestColumn<Model>("Col0", GridLength.Auto),
            };
            var (target, _) = CreateTarget(columns, rowCount: 2);
            var cell = (LayoutTestCellControl)target.RealizedElements[0]!;
            var initialNaturalMeasures = cell.MeasureConstraints.Count(x => x == Size.Infinity);

            target.Unrealize();
            target.Realize(1);
            Layout(target);

            Assert.Equal(
                initialNaturalMeasures + 1,
                cell.MeasureConstraints.Count(x => x == Size.Infinity));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Nth_Child_Handles_Deletion_And_Addition_Correctly()
        {
            var (target, scroll) = CreateTarget(additionalStyles:
                new List<IStyle>
                {
                    new Style(x => x.OfType<TreeDataGridCellsPresenter>().Descendant().Is<TreeDataGridCell>().NthChild(2,0))
                    {
                        Setters =
                        {
                            new Setter(TreeDataGridRow.BackgroundProperty,new SolidColorBrush(Colors.Red)),
                        }
                    }
                });

            Layout(target);

            int CountEvenRedRows(TreeDataGridCellsPresenter presenter)
            {
                return target.GetVisualChildren().Cast<TreeDataGridCell>().Select(x => x.Background)
                    .Where(x => x is SolidColorBrush brush && brush.Color == Colors.Red).Count();
            }

            Assert.Equal(5, CountEvenRedRows(target));
        }

        private static void AssertColumnIndexes(
            TreeDataGridCellsPresenter? target,
            int firstColumnIndex,
            int columnCount)
        {
            Assert.NotNull(target);

            var rowIndexes = target!.GetVisualChildren()
                .Cast<TreeDataGridCell>()
                .Where(x => x.IsVisible)
                .Select(x => x.ColumnIndex)
                .OrderBy(x => x)
                .ToList();

            Assert.Equal(
                Enumerable.Range(firstColumnIndex, columnCount),
                rowIndexes);
        }

        private static void AssertRecyclable(TreeDataGridCellsPresenter? target, int count)
        {
            Assert.NotNull(target);

            var recyclableCells = target!.GetVisualChildren()
                .Cast<TreeDataGridCell>()
                .Where(x => !x.IsVisible)
                .ToList();
            Assert.Equal(count, recyclableCells.Count);
        }

        private static (TreeDataGridCellsPresenter, ScrollViewer) CreateTarget(
            ColumnList<Model>? columns = null,
            List<IStyle>? additionalStyles = null,
            int rowCount = 1,
            TreeDataGridCellsPresenter? presenter = null)
        {
            if (columns is null)
            {
                columns = new ColumnList<Model>();

                for (var i = 0; i < 100; ++i)
                    columns.Add(new LayoutTestColumn<Model>("Column " + i));
            }

            var items = new Model[rowCount];
            var rows = new AnonymousSortableRows<Model>(new TreeDataGridItemsSourceView<Model>(items), null);

            var target = presenter ?? new TreeDataGridCellsPresenter();
            target.ElementFactory = new TestElementFactory();
            target.Items = columns;
            target.Rows = rows;

            // The column list's effective viewport would usually be updated by the rows presenter
            // but in this case we don't have one, so do it manually.
            target.EffectiveViewportChanged += (s, e) =>
            {
                columns.ViewportChanged(e.EffectiveViewport);
            };

            target.Realize(0);

            var scrollViewer = new ScrollViewer
            {
                Template = TestTemplates.ScrollViewerTemplate(),
                Content = target,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            };

            var root = new TestWindow(scrollViewer);

            if (additionalStyles != null)
            {
                foreach (var item in additionalStyles)
                {
                    root.Styles.Add(item);
                }
            }

            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            return (target, scrollViewer);
        }

        private static void Layout(TreeDataGridCellsPresenter target)
        {
            target.UpdateLayout();
        }

        private class Model : NotifyingBase
        {
            public int Id { get; set; }
            public string? Title { get; set; }
        }

        private sealed class MovableColumnList<T> : ColumnList<T>
        {
            public void Move(int oldIndex, int newIndex) => MoveItem(oldIndex, newIndex);
        }

        private sealed class CountingCellsPresenter : TreeDataGridCellsPresenter
        {
            public int MeasureCount { get; private set; }

            protected override Size MeasureOverride(Size availableSize)
            {
                ++MeasureCount;
                return base.MeasureOverride(availableSize);
            }
        }
    }
}
