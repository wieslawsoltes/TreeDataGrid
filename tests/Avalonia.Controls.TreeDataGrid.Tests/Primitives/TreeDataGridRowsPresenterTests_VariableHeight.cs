using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia.Collections;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Primitives
{
    public class TreeDataGridRowsPresenterTests_VariableHeight
    {
        [AvaloniaFact]
        public void Estimates_Unrealized_Row_Positions_From_Realized_Range()
        {
            var target = new RealizedStackElements(traceEnabled: false);
            var estimatedSize = 25d;

            target.Add(10, new Border(), 1000, 20);
            target.Add(11, new Border(), 1020, 40);

            Assert.Equal(970, target.GetOrEstimateElementU(9, ref estimatedSize));
            Assert.Equal(1060, target.GetOrEstimateElementU(12, ref estimatedSize));
            Assert.Equal(1120, target.GetOrEstimateElementU(14, ref estimatedSize));
        }

        [AvaloniaFact]
        public void Does_Not_Use_Stale_Row_Position_After_Insert_Before_Range()
        {
            var target = new RealizedStackElements(traceEnabled: false);
            var estimatedSize = 25d;

            target.Add(10, new Border(), 1000, 20);
            target.ItemsInserted(0, 1, (_, _, _) => { }, _ => { });

            Assert.True(double.IsNaN(target.GetElementU(11)));
            Assert.Equal(220, target.GetOrEstimateElementU(11, ref estimatedSize));
        }

        [AvaloniaFact]
        public void ValidateStartU_Uses_Current_Measured_Row_Heights()
        {
            var first = new Border { Height = 20 };
            var second = new Border { Height = 20 };
            first.Measure(Size.Infinity);
            second.Measure(Size.Infinity);

            var target = new RealizedStackElements(traceEnabled: false);
            target.Add(10, first, 200, 20);
            target.Add(11, second, 220, 20);

            first.Height = 40;
            first.Measure(Size.Infinity);
            target.ValidateStartU(Orientation.Vertical);

            Assert.True(double.IsNaN(target.GetElementU(10)));
            Assert.Equal(30, target.EstimateElementSizeU(Orientation.Vertical));
        }

        [AvaloniaTheory(Timeout = 10000)]
        [InlineData(10)]
        [InlineData(20)]
        [InlineData(50)]
        public void Scroll_Down_To_Bottom(double step)
        {
            var (target, scroll, _) = CreateTarget();

            Layout(target);

            var index = GetFirstRowIndex(target);
            Assert.Equal(0, index);

            while (scroll.Offset.Y < scroll.Extent.Height - scroll.Viewport.Height)
            {
                scroll.Offset = new Vector(0, scroll.Offset.Y + step);
                System.Diagnostics.Debug.WriteLine(scroll.Offset.Y);
                Layout(target);

                var newIndex = GetFirstRowIndex(target);
                Assert.True(newIndex >= index, $"{newIndex} > {index} failed");
                index = newIndex;
            }
        }

        [AvaloniaFact]
        public void Scroll_To_Bottom()
        {
            var (target, scroll, items) = CreateTarget();

            scroll.GetObservable(ScrollViewer.OffsetProperty).Subscribe(x => { });

            Layout(target);

            var index = GetFirstRowIndex(target);
            Assert.Equal(0, index);

            scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
            Layout(target);

            var lastIndex = GetLastRowIndex(target);
            Assert.Equal(items.Count - 1, lastIndex);
        }

        [AvaloniaFact(Timeout = 30000)]
        public void BringIntoView_Does_Not_Leave_Unrealized_Row_Visible()
        {
            var (target, _, items) = CreateTarget(itemCount: 227, rootSize: new Size(100, 600));
            var targetIndexes = new[] { 206, 5, 185, 25, 220, 100, 200, 0 };

            foreach (var targetIndex in targetIndexes)
            {
                var brought = Assert.IsType<TreeDataGridRow>(target.BringIntoView(targetIndex));
                Layout(target);

                Assert.Equal(targetIndex, brought.RowIndex);
                Assert.Same(items[targetIndex], brought.DataContext);

                var visibleRows = target.GetVisualChildren()
                    .Cast<TreeDataGridRow>()
                    .Where(x => x.IsVisible)
                    .OrderBy(x => x.Bounds.Top)
                    .ToList();
                var realizedRows = target.RealizedElements
                    .Cast<TreeDataGridRow>()
                    .OrderBy(x => x.Bounds.Top)
                    .ToList();

                Assert.Contains(brought, realizedRows);
                Assert.Equal(realizedRows, visibleRows);

                for (var i = 1; i < visibleRows.Count; ++i)
                {
                    Assert.Equal(visibleRows[i - 1].RowIndex + 1, visibleRows[i].RowIndex);
                    Assert.True(
                        Math.Abs(visibleRows[i - 1].Bounds.Bottom - visibleRows[i].Bounds.Top) < 0.001,
                        $"Rows {visibleRows[i - 1].RowIndex} and {visibleRows[i].RowIndex} overlap or have a gap.");
                }
            }
        }

        [AvaloniaFact(Timeout = 10000)]
        public void BringIntoView_Correctly_Scrolls_Down_To_A_Page_Of_Smaller_Rows()
        {
            var heights = Enumerable.Range(0, 20)
                .Select(x => ((29 - x) / 10) * 10d)
                .ToArray();
            var (target, scroll, _) = CreateTarget(
                rootSize: new Size(100, 100),
                rowHeights: heights);

            var brought = Assert.IsType<TreeDataGridRow>(target.BringIntoView(19));

            Assert.Equal(19, brought.RowIndex);
            Assert.Equal(new Rect(0, 380, 100, 10), brought.Bounds);
            Assert.Equal(new Size(100, 100), scroll.Viewport);
            Assert.Equal(new Size(100, 390), scroll.Extent);
            Assert.Equal(new Vector(0, 290), scroll.Offset);
            Assert.Equal(Enumerable.Range(10, 10), target.RealizedElements.Select(x => ((TreeDataGridRow)x!).RowIndex));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void BringIntoView_Correctly_Scrolls_Down_To_A_Page_Of_Larger_Rows()
        {
            var heights = Enumerable.Range(0, 20)
                .Select(x => ((x / 10) + 1) * 10d)
                .ToArray();
            var (target, scroll, _) = CreateTarget(
                rootSize: new Size(100, 100),
                rowHeights: heights);

            var brought = Assert.IsType<TreeDataGridRow>(target.BringIntoView(19));

            Assert.Equal(19, brought.RowIndex);
            Assert.Equal(new Rect(0, 190, 100, 20), brought.Bounds);
            Assert.Equal(new Size(100, 100), scroll.Viewport);
            Assert.Equal(new Size(100, 210), scroll.Extent);
            Assert.Equal(new Vector(0, 110), scroll.Offset);
            Assert.Equal(Enumerable.Range(15, 5), target.RealizedElements.Select(x => ((TreeDataGridRow)x!).RowIndex));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Extent_And_Offset_Are_Updated_When_Realized_Row_Heights_Change()
        {
            var (target, scroll, items) = CreateTarget(
                rootSize: new Size(100, 100),
                rowHeights: Enumerable.Repeat(50d, 20).ToArray());

            target.BringIntoView(5);

            Assert.Equal(Enumerable.Range(4, 2), target.RealizedElements.Select(x => ((TreeDataGridRow)x!).RowIndex));
            Assert.Equal(new Size(100, 1000), scroll.Extent);
            Assert.Equal(new Vector(0, 200), scroll.Offset);

            foreach (var item in items)
                item.Height = 25;
            Layout(target);

            Assert.Equal(new Size(100, 500), scroll.Extent);
            Assert.Equal(new Vector(0, 200), scroll.Offset);
            Assert.Equal(Enumerable.Range(8, 4), target.RealizedElements.Select(x => ((TreeDataGridRow)x!).RowIndex));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Focused_Row_Is_Kept_Outside_Viewport_When_Row_Heights_Change()
        {
            var (target, scroll, items) = CreateTarget(
                rootSize: new Size(100, 100),
                rowHeights: Enumerable.Repeat(50d, 20).ToArray());

            target.BringIntoView(5);
            var focused = Assert.IsType<TreeDataGridRow>(target.TryGetElement(5));
            focused.Focusable = true;
            focused.Focus();

            foreach (var item in items)
                item.Height = 25;
            Layout(target);

            var viewport = new Rect(
                scroll.Offset.X,
                scroll.Offset.Y,
                scroll.Viewport.Width,
                scroll.Viewport.Height);
            Assert.True(focused.IsKeyboardFocusWithin);
            Assert.Equal(new Rect(0, 125, 100, 25), focused.Bounds);
            Assert.False(focused.Bounds.Intersects(viewport));
            Assert.DoesNotContain(focused, target.RealizedElements);
        }

        [AvaloniaFact(Timeout = 30000)]
        public void Pixel_Scrolls_In_Both_Directions_Preserve_Variable_Height_Virtualization()
        {
            var (target, scroll, items) = CreateTarget(itemCount: 40, rootSize: new Size(100, 200));
            var encounteredRows = new HashSet<TreeDataGridRow>();
            var maximumRealizedCount = 0;

            void AssertState()
            {
                var rows = target.RealizedElements.Cast<TreeDataGridRow>().ToList();
                var viewportStart = scroll.Offset.Y;
                var viewportEnd = Math.Min(scroll.Extent.Height, viewportStart + scroll.Viewport.Height);

                Assert.NotEmpty(rows);
                Assert.Equal(rows.Count, rows.Distinct().Count());
                Assert.True(rows[0].Bounds.Top <= viewportStart + 0.001);
                Assert.True(rows[^1].Bounds.Bottom >= viewportEnd - 0.001);

                for (var i = 0; i < rows.Count; ++i)
                {
                    var row = rows[i];
                    Assert.Same(items[row.RowIndex], row.DataContext);
                    Assert.Contains(row, target.GetVisualChildren());
                    Assert.Contains(row, target.GetLogicalChildren());

                    if (i > 0)
                    {
                        Assert.Equal(rows[i - 1].RowIndex + 1, row.RowIndex);
                        Assert.True(Math.Abs(rows[i - 1].Bounds.Bottom - row.Bounds.Top) < 0.001);
                    }

                    encounteredRows.Add(row);
                }

                maximumRealizedCount = Math.Max(maximumRealizedCount, rows.Count);
            }

            var maximumOffset = (int)(scroll.Extent.Height - scroll.Viewport.Height);

            for (var offset = 0; offset <= maximumOffset; ++offset)
            {
                scroll.Offset = new Vector(0, offset);
                Layout(target);
                AssertState();
            }

            var mutationIndex = Math.Min(
                ((TreeDataGridRow)target.RealizedElements[0]!).RowIndex + 1,
                items.Count);
            var inserted = new Model { Id = -1, Height = 37 };
            items.Insert(mutationIndex, inserted);
            Layout(target);
            AssertState();
            items.RemoveAt(mutationIndex);
            Layout(target);
            AssertState();

            for (var offset = (int)scroll.Offset.Y; offset >= 0; --offset)
            {
                scroll.Offset = new Vector(0, offset);
                Layout(target);
                AssertState();
            }

            Assert.True(encounteredRows.Count <= maximumRealizedCount + 2,
                $"Encountered {encounteredRows.Count} controls for a maximum realized count of {maximumRealizedCount}.");
        }

        [AvaloniaFact]
        public void Insert_And_Remove_Preserve_Variable_Height_Row_Identity_And_Positions()
        {
            var (target, _, items) = CreateTarget(rootSize: new Size(100, 400));
            var originalRows = target.RealizedElements.Cast<TreeDataGridRow>().ToList();
            var inserted = new Model { Id = -1, Height = 73 };

            items.Insert(2, inserted);
            Layout(target);

            Assert.Same(inserted, target.TryGetElement(2)!.DataContext);
            var preservedCount = 0;
            for (var oldIndex = 2; oldIndex < originalRows.Count - 1; ++oldIndex)
            {
                if (target.TryGetElement(oldIndex + 1) is not { } shiftedRow)
                    break;

                Assert.Same(originalRows[oldIndex], shiftedRow);
                ++preservedCount;
            }
            Assert.True(preservedCount > 0);
            AssertContiguousRows(target, items);

            items.RemoveAt(2);
            Layout(target);

            for (var index = 0; index < 2 + preservedCount; ++index)
                Assert.Same(originalRows[index], target.TryGetElement(index));
            AssertContiguousRows(target, items);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Move_Preserves_Variable_Height_Row_Identity_And_Positions()
        {
            var (target, _, items) = CreateTarget(rootSize: new Size(100, 400));
            var rowsByItem = target.RealizedElements
                .Cast<TreeDataGridRow>()
                .ToDictionary(x => (Model)x.DataContext!);

            items.Move(2, 7);
            Layout(target);

            foreach (var pair in rowsByItem)
            {
                var newIndex = items.IndexOf(pair.Key);
                if (target.TryGetElement(newIndex) is { } row)
                    Assert.Same(pair.Value, row);
            }

            AssertContiguousRows(target, items);
        }

        private static void AssertContiguousRows(
            TreeDataGridRowsPresenter target,
            IReadOnlyList<Model> items)
        {
            var rows = target.RealizedElements.Cast<TreeDataGridRow>().ToList();

            for (var i = 0; i < rows.Count; ++i)
            {
                Assert.Equal(i, rows[i].RowIndex);
                Assert.Same(items[i], rows[i].DataContext);

                if (i > 0)
                    Assert.True(Math.Abs(rows[i - 1].Bounds.Bottom - rows[i].Bounds.Top) < 0.001);
            }
        }

        private static int GetFirstRowIndex(TreeDataGridRowsPresenter target)
        {
            return target!.GetVisualChildren()
                .Cast<TreeDataGridRow>()
                .Where(x => x.IsVisible)
                .Select(x => x.RowIndex)
                .OrderBy(x => x)
                .First();
        }

        private static int GetLastRowIndex(TreeDataGridRowsPresenter target)
        {
            return target!.GetVisualChildren()
                .Cast<TreeDataGridRow>()
                .Where(x => x.IsVisible)
                .Select(x => x.RowIndex)
                .OrderByDescending(x => x)
                .First();
        }

        private static (TreeDataGridRowsPresenter, ScrollViewer, AvaloniaList<Model>) CreateTarget(
            IColumns? columns = null,
            List<IStyle>? additionalStyles = null,
            int itemCount = 100,
            Size? rootSize = null,
            IReadOnlyList<double>? rowHeights = null)
        {
            var heights = rowHeights ?? CreateRandomHeights(itemCount);
            var items = new AvaloniaList<Model>(heights.Select((height, index) =>
                new Model
                {
                    Id = index,
                    Height = height,
                }));

            var itemsView = new TreeDataGridItemsSourceView<Model>(items);
            var rows = new AnonymousSortableRows<Model>(itemsView, null);

            var target = new TreeDataGridRowsPresenter
            {
                ElementFactory = new TreeDataGridElementFactory(),
                Items = rows,
                Columns = columns,
            };

            var scrollViewer = new ScrollViewer
            {
                Template = TestTemplates.ScrollViewerTemplate(),
                Content = target,
            };

            var root = new TestWindow(scrollViewer, rootSize ?? new Size(100, 1000))
            {
                Styles =
                {
                    new Style(x => x.OfType<TreeDataGridRow>())
                    {
                        Setters =
                        {
                            new Setter(TreeDataGridRow.HeightProperty, new Binding(nameof(Model.Height))),
                        }
                    }
                },
            };

            if (additionalStyles != null)
            {
                foreach (var item in additionalStyles)
                {
                    root.Styles.Add(item);
                }
            }

            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            return (target, scrollViewer, items);

            static IReadOnlyList<double> CreateRandomHeights(int count)
            {
                var random = new Random(0);
                return Enumerable.Range(0, count)
                    .Select(_ => (double)(random.Next(90) + 10))
                    .ToArray();
            }
        }

        private static void Layout(TreeDataGridRowsPresenter target)
        {
            target.UpdateLayout();
        }

        private class Model : NotifyingBase
        {
            private double _height;

            public int Id { get; set; }

            public double Height
            {
                get => _height;
                set => RaiseAndSetIfChanged(ref _height, value);
            }
        }
    }
}
