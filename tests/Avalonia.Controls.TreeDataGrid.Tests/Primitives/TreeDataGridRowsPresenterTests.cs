﻿using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Primitives
{
    public class TreeDataGridRowsPresenterTests
    {
        [AvaloniaFact(Timeout = 10000)]
        public void Nth_Child_Handles_Deletion_And_Addition_Correctly()
        {
            var (target, scroll, items) = CreateTarget(additionalStyles:
                new List<IStyle>
                {
                    new Style(x => x.OfType<TreeDataGridRowsPresenter>().Descendant().OfType<TreeDataGridRow>().NthChild(2,0))
                    {
                        Setters =
                        {
                            new Setter(TreeDataGridRow.BackgroundProperty,new SolidColorBrush(Colors.Red)),
                        }
                    }
                });

            Layout(target);

            int CountEvenRedRows(TreeDataGridRowsPresenter presenter)
            {
                return target.GetVisualChildren().Cast<TreeDataGridRow>().Select(x => x.Background)
                    .Where(x => x is SolidColorBrush brush && brush.Color == Colors.Red).Count();
            }

            Assert.True(CountEvenRedRows(target) == 5);

            Assert.True(items.Count == 100);

            items.RemoveAt(0);
            items.RemoveAt(0);

            Assert.True(items.Count == 98);

            Layout(target);

            Assert.True(CountEvenRedRows(target) == 5);

            items.Add(new Model() { Id = 101, Title = "Item 101" });

            Assert.True(items.Count == 99);

            Layout(target);

            Assert.True(CountEvenRedRows(target) == 5);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Creates_Initial_Rows()
        {
            var (target, scroll, _) = CreateTarget();

            Assert.Equal(new Size(100, 1000), scroll.Extent);
            AssertRowIndexes(target, 0, 10);
            AssertRecyclable(target, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Scrolls_Down_One_Row()
        {
            var (target, scroll, _) = CreateTarget();

            scroll.Offset = new Vector(0, 10);
            Layout(target);

            AssertRowIndexes(target, 1, 10);
            AssertRecyclable(target, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void CacheLength_Buffers_Rows_Across_Small_Scrolls()
        {
            var (target, scroll, _) = CreateTarget(cacheLength: 0.5);
            var initialRows = target.RealizedElements.ToList();

            AssertRowIndexes(target, 0, 20);

            scroll.Offset = new Vector(0, 40);
            Layout(target);

            Assert.Equal(initialRows, target.RealizedElements);

            scroll.Offset = new Vector(0, 110);
            Layout(target);

            AssertRowIndexes(target, 6, 20);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Small_Scrolls_Within_Realized_Rows_Do_Not_Repeat_Measure()
        {
            var presenter = new CountingRowsPresenter();
            var (target, scroll, items) = CreateTarget(presenter: presenter);

            scroll.Offset = new Vector(0, 1);
            Layout(target);
            var measureCountAfterRealizingTrailingRow = presenter.MeasureCount;

            for (var offset = 2; offset <= 10; ++offset)
            {
                scroll.Offset = new Vector(0, offset);
                Layout(target);
            }

            Assert.Equal(measureCountAfterRealizingTrailingRow, presenter.MeasureCount);
            AssertRealizedRowsAreConsistent(target, items);

            scroll.Offset = new Vector(0, 11);
            Layout(target);

            Assert.True(presenter.MeasureCount > measureCountAfterRealizingTrailingRow);
            AssertRealizedRowsAreConsistent(target, items);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Scrolls_Down_More_Than_A_Page()
        {
            var (target, scroll, _) = CreateTarget();

            scroll.Offset = new Vector(0, 200);
            Layout(target);

            AssertRowIndexes(target, 20, 10);
            AssertRecyclable(target, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Scrolls_Up_More_Than_A_Page()
        {
            var (target, scroll, _) = CreateTarget();

            scroll.Offset = new Vector(0, 200);
            Layout(target);

            scroll.Offset = new Vector(0, 0);
            Layout(target);

            AssertRowIndexes(target, 0, 10);
            AssertRecyclable(target, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Inserted_Row()
        {
            var (target, _, items) = CreateTarget();

            Assert.Equal(10, target.RealizedElements.Count);

            var preservedPrefix = target.RealizedElements.Take(2).ToList();

            items.Insert(2, new Model { Id = 100, Title = "New" });

            var indexes = GetRealizedRowIndexes(target);

            // The inserted slot is unrealized, while existing rows retain their items at their
            // shifted indexes until the next measure fills the slot and trims the tail.
            Assert.Equal(new[] { 0, 1, -1 }.Concat(Enumerable.Range(3, 8)), indexes);
            Layout(target);

            indexes = GetRealizedRowIndexes(target);

            Assert.Equal(Enumerable.Range(0, 10), indexes);
            Assert.Equal(preservedPrefix, target.RealizedElements.Take(2));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Removed_Row()
        {
            var (target, _, items) = CreateTarget();

            Assert.Equal(10, target.RealizedElements.Count);

            items.RemoveAt(2);

            var indexes = GetRealizedRowIndexes(target);

            Assert.Equal(Enumerable.Range(0, 9), indexes);
            Layout(target);

            indexes = GetRealizedRowIndexes(target);

            Assert.Equal(Enumerable.Range(0, 10), indexes);
            Assert.Equal(
                items.Take(10),
                target.RealizedElements.Cast<TreeDataGridRow>().Select(x => x.DataContext));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Insert_Preserves_Realized_Rows_Whose_Items_Only_Changed_Index()
        {
            var (target, _, items) = CreateTarget();
            var rows = target.RealizedElements.Cast<TreeDataGridRow>().ToList();
            var shiftedItem = rows[2].DataContext;

            items.Insert(2, new Model { Id = 100, Title = "New" });

            Assert.Same(rows[2], target.TryGetElement(3));
            Assert.Same(shiftedItem, rows[2].DataContext);
            Assert.Equal(3, rows[2].RowIndex);

            Layout(target);

            Assert.Same(rows[0], target.TryGetElement(0));
            Assert.Same(rows[1], target.TryGetElement(1));
            for (var oldIndex = 2; oldIndex < 9; ++oldIndex)
                Assert.Same(rows[oldIndex], target.TryGetElement(oldIndex + 1));
            AssertRealizedRowsAreConsistent(target, items);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Remove_Preserves_Realized_Rows_Whose_Items_Only_Changed_Index()
        {
            var (target, _, items) = CreateTarget();
            var rows = target.RealizedElements.Cast<TreeDataGridRow>().ToList();
            var shiftedItem = rows[3].DataContext;

            items.RemoveAt(2);

            Assert.Same(rows[3], target.TryGetElement(2));
            Assert.Same(shiftedItem, rows[3].DataContext);
            Assert.Equal(2, rows[3].RowIndex);

            Layout(target);

            Assert.Same(rows[0], target.TryGetElement(0));
            Assert.Same(rows[1], target.TryGetElement(1));
            for (var oldIndex = 3; oldIndex < 10; ++oldIndex)
                Assert.Same(rows[oldIndex], target.TryGetElement(oldIndex - 1));
            AssertRealizedRowsAreConsistent(target, items);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Collection_Edit_Remains_Consistent_Across_Recycling_Scrolls()
        {
            var (target, scroll, items) = CreateTarget();

            items.Insert(2, new Model { Id = -1, Title = "Inserted" });
            Layout(target);
            AssertRealizedRowsAreConsistent(target, items);

            scroll.Offset = new Vector(0, 100);
            Layout(target);
            AssertRealizedRowsAreConsistent(target, items);

            items.RemoveAt(2);
            Layout(target);
            AssertRealizedRowsAreConsistent(target, items);

            scroll.Offset = default;
            Layout(target);
            AssertRealizedRowsAreConsistent(target, items);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Collection_Edit_Preserves_Focus_On_A_Shifted_Visible_Row()
        {
            var (target, _, items) = CreateTarget();
            var focusedRow = target.RealizedElements[5]!;
            var focusedItem = focusedRow.DataContext;

            focusedRow.Focusable = true;
            focusedRow.Focus();
            Assert.True(focusedRow.IsKeyboardFocusWithin);

            items.Insert(2, new Model { Id = -1, Title = "Inserted" });
            Layout(target);

            Assert.Same(focusedRow, target.TryGetElement(6));
            Assert.Same(focusedItem, focusedRow.DataContext);
            Assert.True(focusedRow.IsKeyboardFocusWithin);

            items.RemoveAt(2);
            Layout(target);

            Assert.Same(focusedRow, target.TryGetElement(5));
            Assert.Same(focusedItem, focusedRow.DataContext);
            Assert.True(focusedRow.IsKeyboardFocusWithin);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Move_Preserves_Every_Realized_Row_That_Remains_In_The_Viewport()
        {
            var (target, _, items) = CreateTarget();
            var rowsByItem = target.RealizedElements
                .Cast<TreeDataGridRow>()
                .ToDictionary(x => (Model)x.DataContext!);

            items.Move(2, 7);
            Layout(target);

            AssertRealizedRowsAreConsistent(target, items);
            foreach (var item in items.Take(10))
                Assert.Same(rowsByItem[item], target.TryGetElement(items.IndexOf(item)));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Move_Crossing_Viewport_Boundary_Preserves_Rows_That_Stay_Visible()
        {
            var (target, scroll, items) = CreateTarget();
            scroll.Offset = new Vector(0, 100);
            Layout(target);

            var rowsByItem = target.RealizedElements
                .Cast<TreeDataGridRow>()
                .ToDictionary(x => (Model)x.DataContext!);

            items.Move(2, 15);
            Layout(target);

            AssertRealizedRowsAreConsistent(target, items);
            foreach (var pair in rowsByItem)
            {
                var newIndex = items.IndexOf(pair.Key);
                if (target.TryGetElement(newIndex) is { } row)
                    Assert.Same(pair.Value, row);
            }

            scroll.Offset = default;
            Layout(target);
            AssertRealizedRowsAreConsistent(target, items);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Move_Preserves_Focus_And_Row_Identity_For_Visible_Item()
        {
            var (target, _, items) = CreateTarget();
            var focusedRow = target.RealizedElements[5]!;
            var focusedItem = focusedRow.DataContext;

            focusedRow.Focusable = true;
            focusedRow.Focus();
            Assert.True(focusedRow.IsKeyboardFocusWithin);

            items.Move(5, 8);
            Layout(target);

            Assert.Same(focusedItem, items[8]);
            Assert.Same(focusedRow, target.TryGetElement(8));
            Assert.True(focusedRow.IsKeyboardFocusWithin);
            AssertRealizedRowsAreConsistent(target, items);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Unrealized_Rows_Being_Removed_From_End()
        {
            var (target, scroll, items) = CreateTarget();

            Assert.Equal(new Size(100, 1000), scroll.Extent);
            AssertRowIndexes(target, 0, 10);
            AssertRecyclable(target, 0);

            items.RemoveRange(90, 10);

            AssertRowIndexes(target, 0, 10);
            AssertRecyclable(target, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Unrealized_Rows_Being_Removed_From_Start()
        {
            var (target, scroll, items) = CreateTarget();

            Assert.Equal(new Size(100, 1000), scroll.Extent);
            scroll.Offset = new Vector(0, 900);
            Layout(target);

            AssertRowIndexes(target, 90, 10);
            AssertRecyclable(target, 0);

            items.RemoveRange(0, 10);

            AssertRowIndexes(target, 80, 10);
            AssertRecyclable(target, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Realized_Children_Should_Not_Be_Removed()
        {
            var (target, _, items) = CreateTarget();

            Assert.Equal(100, target!.Items!.Count);
            Assert.Equal(10, target.RealizedElements.Count);

            items.RemoveRange(7, 93);
            Layout(target);
            var children = target.GetVisualChildren();

            for (var i = 0; i < children.Count(); i++)
            {
                Assert.Equal(children.ElementAt(i), target.RealizedElements[i]);
            }
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Realized_Children_Are_Added_To_The_Logical_Tree()
        {
            var (target, _, _) = CreateTarget();
            var logicalChildren = target.GetLogicalChildren().ToList();

            Assert.NotEmpty(target.RealizedElements);
            Assert.All(target.RealizedElements, child => Assert.Contains(child, logicalChildren));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Recycled_Rows_Are_Removed_From_Visual_And_Logical_Trees()
        {
            var (target, _, _) = CreateTarget();

            Assert.Equal(10, target.RealizedElements.Count);

            target.RecycleAllElements();

            Assert.Empty(target.RealizedElements);
            Assert.Empty(target.GetVisualChildren());
            Assert.Empty(target.GetLogicalChildren());
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Detached_Collection_Shrink_Does_Not_Leave_Ghost_Rows()
        {
            var (target, scroll, items) = CreateTarget(itemCount: 10);
            var window = Assert.IsType<TestWindow>(scroll.Parent);

            Assert.Equal(10, target.GetVisualChildren().Count());

            window.Content = null;
            window.UpdateLayout();
            items.RemoveRange(5, 5);
            window.Content = scroll;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var visualRows = target.GetVisualChildren().Cast<TreeDataGridRow>().ToList();
            var logicalRows = target.GetLogicalChildren().Cast<TreeDataGridRow>().ToList();

            Assert.Equal(5, visualRows.Count);
            Assert.Equal(5, logicalRows.Count);
            Assert.All(visualRows, x => Assert.True(x.IsVisible));
            Assert.Equal(items, visualRows.Select(x => x.DataContext));
            Assert.Equal(visualRows, logicalRows);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Should_Remove_Children_On_Empty_Collection_Assignment_To_Items()
        {
            var (target, _, items) = CreateTarget();
            Layout(target);
            Assert.Equal(100, items.Count);
            items.RemoveRange(1, 99);
            Layout(target);
            Assert.NotNull(target.Items);
            Assert.Single(target.Items!);
            Assert.Single(target.GetVisualChildren());

            target.Items = new AnonymousSortableRows<Model>(TreeDataGridItemsSourceView<Model>.Empty, null);
            Layout(target);
            Assert.Empty(target.Items);

            Assert.Empty(target.GetVisualChildren());
            Assert.Empty(target.GetLogicalChildren());

            target.Items = new AnonymousSortableRows<Model>(new TreeDataGridItemsSourceView<Model>(Enumerable.Range(0, 5)
                .Select(x => new Model { Id = x, Title = "Item " + x, })), null);
            Layout(target);
            Assert.Equal(5, target.Items.Count);

            Assert.Equal(5, target.GetVisualChildren().Count());
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Removed_And_Reinserted_Row()
        {
            var (target, _, items) = CreateTarget();

            Assert.Equal(10, target.RealizedElements.Count);

            var item = items[0];
            var preservedRows = target.RealizedElements.Skip(1).ToList();
            items.RemoveAt(0);

            var indexes = GetRealizedRowIndexes(target);

            Assert.Equal(Enumerable.Range(0, 9), indexes);
            Assert.Equal(preservedRows, target.RealizedElements);

            items.Insert(0, item);

            indexes = GetRealizedRowIndexes(target);
            Assert.Equal(new[] { -1 }.Concat(Enumerable.Range(1, 9)), indexes);
            Layout(target);

            indexes = GetRealizedRowIndexes(target);

            Assert.Equal(Enumerable.Range(0, 10), indexes);
            Assert.Same(item, target.RealizedElements[0]!.DataContext);
            Assert.Equal(preservedRows, target.RealizedElements.Skip(1));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Removing_Row_Range_That_Spans_Realized_And_Unrealized_Elements()
        {
            var (target, scroll, items) = CreateTarget();

            // Scroll down one item.
            scroll.Offset = new Vector(0, 10);
            Layout(target);

            Assert.Equal(10, target.RealizedElements.Count);

            var toRecycle = target.RealizedElements.Skip(4).Take(6).ToList();
            items.RemoveRange(5, 10);

            var indexes = GetRealizedRowIndexes(target);

            // Item removed from realized elements and subsequent row indexes updated.
            Assert.Equal(Enumerable.Range(1, 4), indexes);

            var elements = target.RealizedElements.ToList();
            Layout(target);

            indexes = GetRealizedRowIndexes(target);

            // After layout an element for the newly visible last row is created and indexes updated.
            Assert.Equal(Enumerable.Range(1, 10), indexes);

            // And the removed row should now have been recycled as the last row.
            elements.AddRange(toRecycle);
            Assert.Equal(elements, target.RealizedElements);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Removing_All_Rows_When_Scrolled()
        {
            var (target, scroll, items) = CreateTarget();

            // Scroll down one item.
            scroll.Offset = new Vector(0, 10);
            Layout(target);

            Assert.Equal(10, target.RealizedElements.Count);

            // Remove all items using RemoveRange.
            items.RemoveRange(0, items.Count);

            // All items removed
            Assert.Empty(target.RealizedElements);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Removing_Row_Range_That_Invalidates_Current_Viewport()
        {
            var (target, scroll, items) = CreateTarget();

            // Scroll down ten items.
            scroll.Offset = new Vector(0, 100);
            Layout(target);

            Assert.Equal(10, target.RealizedElements.Count);

            // Remove all but the first five items.
            items.RemoveRange(5, 95);

            Layout(target);

            // The target bounds should be updated, which will cause the scrollviewer to scroll back up.
            Assert.Equal(new Size(100, 100), target.Bounds.Size);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Removing_Focused_Row_While_Outside_Viewport()
        {
            var (target, scroll, items) = CreateTarget();
            var element = target.RealizedElements.ElementAt(0)!;

            element.Focusable = true;
            element.Focus();

            // Scroll down one item.
            scroll.Offset = new Vector(0, 10);
            Layout(target);

            // Remove the focused element.
            items.RemoveAt(0);

            // Scroll back to the beginning.
            scroll.Offset = new Vector(0, 0);
            Layout(target);

            // The correct element should be shown.
            Assert.Same(items[0], target.RealizedElements.ElementAt(0)!.DataContext);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Replacing_Focused_Row_While_Outside_Viewport()
        {
            var (target, scroll, items) = CreateTarget();
            var element = target.RealizedElements.ElementAt(0)!;

            element.Focusable = true;
            element.Focus();

            // Scroll down one item.
            scroll.Offset = new Vector(0, 10);
            Layout(target);

            // Replace the focused element.
            items[0] = new Model { Id = 100, Title = "New Item" };

            // Scroll back to the beginning.
            scroll.Offset = new Vector(0, 0);
            Layout(target);

            // The correct element should be shown.
            Assert.Same(items[0], target.RealizedElements.ElementAt(0)!.DataContext);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Moving_Focused_Row_While_Outside_Viewport()
        {
            var (target, scroll, items) = CreateTarget();
            var element = target.RealizedElements.ElementAt(0)!;

            element.Focusable = true;
            element.Focus();

            // Scroll down one item.
            scroll.Offset = new Vector(0, 10);
            Layout(target);

            // Move the focused element.
            items.Move(0, items.Count - 1);

            // Scroll back to the beginning.
            scroll.Offset = new Vector(0, 0);
            Layout(target);

            // The correct element should be shown.
            Assert.Same(items[0], target.RealizedElements.ElementAt(0)!.DataContext);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Keeps_Offscreen_Focused_Row_With_Its_Item_After_Insert()
        {
            var (target, scroll, items) = CreateTarget();
            var element = target.RealizedElements[5]!;
            var item = items[5];

            element.Focusable = true;
            element.Focus();
            scroll.Offset = new Vector(0, 100);
            Layout(target);

            items.Insert(0, new Model { Id = -1, Title = "Inserted" });
            scroll.Offset = default;
            Layout(target);

            Assert.Same(item, items[6]);
            Assert.Same(element, target.RealizedElements[6]);
            Assert.Equal(items.Take(10), target.RealizedElements.Select(x => x!.DataContext));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Keeps_Offscreen_Focused_Row_With_Its_Item_After_Remove()
        {
            var (target, scroll, items) = CreateTarget();
            var element = target.RealizedElements[5]!;
            var item = items[5];

            element.Focusable = true;
            element.Focus();
            scroll.Offset = new Vector(0, 100);
            Layout(target);

            items.RemoveAt(0);
            scroll.Offset = default;
            Layout(target);

            Assert.Same(item, items[4]);
            Assert.Same(element, target.RealizedElements[4]);
            Assert.Equal(items.Take(10), target.RealizedElements.Select(x => x!.DataContext));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Keeps_Offscreen_Focused_Row_With_Its_Item_After_Move()
        {
            var (target, scroll, items) = CreateTarget();
            var element = target.RealizedElements[5]!;
            var item = items[5];

            element.Focusable = true;
            element.Focus();
            scroll.Offset = new Vector(0, 100);
            Layout(target);

            items.Move(0, 8);
            scroll.Offset = default;
            Layout(target);

            Assert.Same(item, items[4]);
            Assert.Same(element, target.RealizedElements[4]);
            Assert.Equal(items.Take(10), target.RealizedElements.Select(x => x!.DataContext));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Move_After_Insert_Without_Duplicates()
        {
            // This tests the specific scenario: Add item at index 0, then move item from index 2 to position 1
            // The bug was that after this sequence, indices 2 and 3 would become duplicates of indices 0 and 1
            var (target, scroll, items) = CreateTarget(itemCount: 4);

            Layout(target);

            // Initial state: [Item0, Item1, Item2, Item3] at indices 0-3
            Assert.Equal(4, items.Count);
            Assert.Equal("Item 0", ((Model)target.RealizedElements.ElementAt(0)!.DataContext!).Title);
            Assert.Equal("Item 1", ((Model)target.RealizedElements.ElementAt(1)!.DataContext!).Title);
            Assert.Equal("Item 2", ((Model)target.RealizedElements.ElementAt(2)!.DataContext!).Title);
            Assert.Equal("Item 3", ((Model)target.RealizedElements.ElementAt(3)!.DataContext!).Title);

            // Step 1: Add new item at index 0
            items.Insert(0, new Model { Id = 100, Title = "Item 100" });
            Layout(target);

            // After insert: [Item100, Item0, Item1, Item2, Item3] at indices 0-4
            Assert.Equal(5, items.Count);
            Assert.Equal("Item 100", ((Model)target.RealizedElements.ElementAt(0)!.DataContext!).Title);
            Assert.Equal("Item 0", ((Model)target.RealizedElements.ElementAt(1)!.DataContext!).Title);
            Assert.Equal("Item 1", ((Model)target.RealizedElements.ElementAt(2)!.DataContext!).Title);
            Assert.Equal("Item 2", ((Model)target.RealizedElements.ElementAt(3)!.DataContext!).Title);
            Assert.Equal("Item 3", ((Model)target.RealizedElements.ElementAt(4)!.DataContext!).Title);

            // Step 2: Move item at index 2 (Item1) to position 1
            items.Move(2, 1);
            Layout(target);

            // After move: [Item100, Item1, Item0, Item2, Item3] at indices 0-4
            Assert.Equal(5, items.Count);

            // Verify no duplicates - each element should have the correct DataContext
            var realizedModels = target.RealizedElements
                .Cast<TreeDataGridRow>()
                .Select(x => (Model)x.DataContext!)
                .ToList();

            Assert.Equal("Item 100", realizedModels[0].Title);
            Assert.Equal("Item 1", realizedModels[1].Title);
            Assert.Equal("Item 0", realizedModels[2].Title);
            Assert.Equal("Item 2", realizedModels[3].Title);
            Assert.Equal("Item 3", realizedModels[4].Title);

            // Verify all IDs are unique (no duplicates)
            var uniqueIds = realizedModels.Select(m => m.Id).Distinct().ToList();
            Assert.Equal(5, uniqueIds.Count);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Remove_Updates_Subsequent_Element_Data()
        {
            // Test that when items are removed, elements after the removal get their data updated
            var (target, scroll, items) = CreateTarget(itemCount: 5);

            Layout(target);

            // Initial state: [Item0, Item1, Item2, Item3, Item4] at indices 0-4
            Assert.Equal(5, items.Count);
            Assert.Equal("Item 0", ((Model)target.RealizedElements.ElementAt(0)!.DataContext!).Title);
            Assert.Equal("Item 1", ((Model)target.RealizedElements.ElementAt(1)!.DataContext!).Title);
            Assert.Equal("Item 2", ((Model)target.RealizedElements.ElementAt(2)!.DataContext!).Title);
            Assert.Equal("Item 3", ((Model)target.RealizedElements.ElementAt(3)!.DataContext!).Title);
            Assert.Equal("Item 4", ((Model)target.RealizedElements.ElementAt(4)!.DataContext!).Title);

            // Remove Item1 at index 1
            items.RemoveAt(1);
            Layout(target);

            // After remove: [Item0, Item2, Item3, Item4] at indices 0-3
            Assert.Equal(4, items.Count);

            // Verify all elements have correct DataContext (not stale data)
            var realizedModels = target.RealizedElements
                .Cast<TreeDataGridRow>()
                .Select(x => (Model)x.DataContext!)
                .ToList();

            Assert.Equal(4, realizedModels.Count);
            Assert.Equal("Item 0", realizedModels[0].Title);
            Assert.Equal("Item 2", realizedModels[1].Title); // Not "Item 1"!
            Assert.Equal("Item 3", realizedModels[2].Title); // Not "Item 2"!
            Assert.Equal("Item 4", realizedModels[3].Title); // Not "Item 3"!

            // Verify IDs match the titles (no stale data)
            Assert.Equal(0, realizedModels[0].Id);
            Assert.Equal(2, realizedModels[1].Id);
            Assert.Equal(3, realizedModels[2].Id);
            Assert.Equal(4, realizedModels[3].Id);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Insert_Updates_Subsequent_Element_Data()
        {
            // Test that when items are inserted, elements after the insertion get their data updated
            var (target, scroll, items) = CreateTarget(itemCount: 4);

            Layout(target);

            // Initial state: [Item0, Item1, Item2, Item3] at indices 0-3
            Assert.Equal(4, items.Count);
            Assert.Equal("Item 0", ((Model)target.RealizedElements.ElementAt(0)!.DataContext!).Title);
            Assert.Equal("Item 1", ((Model)target.RealizedElements.ElementAt(1)!.DataContext!).Title);
            Assert.Equal("Item 2", ((Model)target.RealizedElements.ElementAt(2)!.DataContext!).Title);
            Assert.Equal("Item 3", ((Model)target.RealizedElements.ElementAt(3)!.DataContext!).Title);

            // Insert new item at index 1
            items.Insert(1, new Model { Id = 100, Title = "Item 100" });
            Layout(target);

            // After insert: [Item0, Item100, Item1, Item2, Item3] at indices 0-4
            Assert.Equal(5, items.Count);

            // Verify all elements have correct DataContext (not stale data)
            var realizedModels = target.RealizedElements
                .Cast<TreeDataGridRow>()
                .Select(x => (Model)x.DataContext!)
                .ToList();

            Assert.Equal(5, realizedModels.Count);
            Assert.Equal("Item 0", realizedModels[0].Title);
            Assert.Equal("Item 100", realizedModels[1].Title); // The inserted item
            Assert.Equal("Item 1", realizedModels[2].Title);   // Not "Item 2"!
            Assert.Equal("Item 2", realizedModels[3].Title);   // Not "Item 3"!
            Assert.Equal("Item 3", realizedModels[4].Title);   // Not stale!

            // Verify IDs match the titles (no stale data)
            Assert.Equal(0, realizedModels[0].Id);
            Assert.Equal(100, realizedModels[1].Id);
            Assert.Equal(1, realizedModels[2].Id);
            Assert.Equal(2, realizedModels[3].Id);
            Assert.Equal(3, realizedModels[4].Id);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Adding_Rows_While_Detached_From_VisualTree()
        {
            var (target, scroll, items) = CreateTarget(itemCount: 5);
            var testWindow = scroll.Parent as TestWindow;

            if (testWindow != null)
            {
                testWindow.Content = null;
                testWindow.UpdateLayout();
            }

            var tabItem = new TabItem { Content = scroll };
            var tabControl = new TabControl { Items = { tabItem, new TabItem() }, Template = TabControlTemplate() };

            ApplyTemplate(tabControl);

            if (testWindow != null)
            {
                testWindow.Content = tabControl;
                tabControl.ApplyTemplate();
                testWindow.UpdateLayout();
            }

            Dispatcher.UIThread.RunJobs();

            tabControl.SelectedIndex = 1;

            Layout(target);

            Enumerable.Range(5, 5).ToList().ForEach(x => items.Insert(1, new Model { Id = x, Title = "Item " + x }));

            tabControl.SelectedIndex = 0;
            Layout(target);

            var indexes = GetRealizedRowIndexes(target);
            var models = target!.RealizedElements
                .Cast<TreeDataGridRow?>().Select(x => x?.Model)
                .Cast<Model>().ToList();

            var distinctModelCount = models.DistinctBy(x => x.Id).Count();

            Assert.Equal(10, indexes.Count);
            Assert.Equal(10, models.Count);
            Assert.Equal(10, distinctModelCount);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Updates_Star_Column_ActualWidth()
        {
            var columns = new ColumnList<Model>
            {
                new TextColumn<Model, int>("ID", x => x.Id, new GridLength(1, GridUnitType.Star)),
                new TextColumn<Model, string?>("Title", x => x.Title, new GridLength(1, GridUnitType.Star))
            };

            var (target, _, _) = CreateTarget(columns: columns);

            foreach (var column in columns)
            {
                Assert.Equal(50, column.ActualWidth);
            }
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Brings_Next_Item_Into_View()
        {
            var (target, scroll, _) = CreateTarget();

            target.BringIntoView(10);
            Layout(target);

            AssertRowIndexes(target, 1, 10);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Handles_Bringing_Item_Into_View_Which_Will_Already_Be_In_View_When_Created()
        {
            var (target, scroll, _) = CreateTarget();

            // Clear the items and do a layout to simulate starting from an empty state.
            var items = target.Items;
            target.Items = null;
            Layout(target);

            // Assign the items.
            target.Items = items;

            // Now bring the first item into view before it's created. There was an issue here where
            // the presenter will wait for a viewport update which will never come because the item
            // will be placed in the existing viewport.
            target.BringIntoView(0);

            AssertRowIndexes(target, 0, 10);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Brings_Partially_Visible_New_Item_Into_View()
        {
            // Issue #77
            var (target, scroll, items) = CreateTarget(itemCount: 9, rootSize: new Size(100, 95));

            AssertRowIndexes(target, 0, 9);

            items.Add(new Model { Id = 100, Title = "New Item" });
            target.BringIntoView(9);
            Layout(target);

            AssertRowIndexes(target, 0, 10);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Assigns_Row_DataContexts()
        {
            var (target, scroll, items) = CreateTarget();
            var lastRow = (TreeDataGridRow)target.RealizedElements.Last()!;

            for (var i = 0; i < 10; ++i)
            {
                Assert.Same(items[i], target.RealizedElements[i]!.DataContext);
            }

            items.RemoveRange(0, 99);
            Layout(target);

            Assert.Equal(-1, lastRow.RowIndex);
            Assert.Null(lastRow.DataContext);
        }

        private static void AssertRowIndexes(TreeDataGridRowsPresenter? target, int firstRowIndex, int rowCount)
        {
            Assert.NotNull(target);

            var rowIndexes = target!.GetVisualChildren()
                .Cast<TreeDataGridRow>()
                .Where(x => x.IsVisible)
                .Select(x => x.RowIndex)
                .OrderBy(x => x)
                .ToList();

            Assert.Equal(
                Enumerable.Range(firstRowIndex, rowCount),
                rowIndexes);

            rowIndexes = target!.RealizedElements
                .Cast<TreeDataGridRow>()
                .Where(x => x.IsVisible)
                .Select(x => x.RowIndex)
                .OrderBy(x => x)
                .ToList();

            Assert.Equal(
                Enumerable.Range(firstRowIndex, rowCount),
                rowIndexes);
        }

        private static void AssertRecyclable(TreeDataGridRowsPresenter? target, int count)
        {
            Assert.NotNull(target);

            var recyclableRows = target!.GetLogicalChildren()
                .Cast<TreeDataGridRow>()
                .Where(x => !x.IsVisible)
                .ToList();
            Assert.Equal(count, recyclableRows.Count);
        }

        private static List<int> GetRealizedRowIndexes(TreeDataGridRowsPresenter? target)
        {
            Assert.NotNull(target);

            return target!.RealizedElements
                .Cast<TreeDataGridRow?>()
                .Select(x => x?.RowIndex ?? -1)
                .ToList();
        }

        private static void AssertRealizedRowsAreConsistent(
            TreeDataGridRowsPresenter target,
            IReadOnlyList<Model> items)
        {
            var rows = target.RealizedElements.Cast<TreeDataGridRow>().ToList();
            Assert.Equal(rows.Count, rows.Select(x => x.RowIndex).Distinct().Count());

            foreach (var row in rows)
            {
                Assert.InRange(row.RowIndex, 0, items.Count - 1);
                Assert.Same(items[row.RowIndex], row.DataContext);
                Assert.True(row.IsVisible);
                Assert.Contains(row, target.GetVisualChildren());
                Assert.Contains(row, target.GetLogicalChildren());
            }

            Assert.Equal(
                rows.OrderBy(x => x.RowIndex),
                target.GetVisualChildren().Cast<TreeDataGridRow>().Where(x => x.IsVisible).OrderBy(x => x.RowIndex));
        }

        private static (TreeDataGridRowsPresenter, ScrollViewer, AvaloniaList<Model>) CreateTarget(
            IColumns? columns = null,
            List<IStyle>? additionalStyles = null,
            int itemCount = 100,
            Size? rootSize = null,
            double cacheLength = 0,
            TreeDataGridRowsPresenter? presenter = null)
        {
            var items = new AvaloniaList<Model>(Enumerable.Range(0, itemCount).Select(x =>
                new Model
                {
                    Id = x,
                    Title = "Item " + x,
                }));

            var itemsView = new TreeDataGridItemsSourceView<Model>(items);
            var rows = new AnonymousSortableRows<Model>(itemsView, null);

            var target = presenter ?? new TreeDataGridRowsPresenter();
            target.CacheLength = cacheLength;
            target.ElementFactory = new TreeDataGridElementFactory();
            target.Items = rows;
            target.Columns = columns;

            var scrollViewer = new ScrollViewer
            {
                Template = TestTemplates.ScrollViewerTemplate(),
                Content = target,
            };

            var root = new TestWindow(scrollViewer, rootSize)
            {
                Styles =
                {
                    new Style(x => x.OfType<TreeDataGridRow>())
                    {
                        Setters =
                        {
                            new Setter(TreeDataGridRow.HeightProperty, 10.0),
                        }
                    }
                }
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
        }

        private static void Layout(TreeDataGridRowsPresenter target)
        {
            target.UpdateLayout();
        }

        private static void ApplyTemplate(TabControl target)
        {
            target.ApplyTemplate();

            target.Presenter?.ApplyTemplate();

            foreach (var tabItem in target.GetLogicalChildren().OfType<TabItem>())
            {
                tabItem.Template = TabItemTemplate();

                tabItem.ApplyTemplate();

                tabItem.Presenter?.UpdateChild();
            }
        }

        private static IControlTemplate TabItemTemplate()
        {
            return new FuncControlTemplate<TabItem>((parent, scope) =>
                new ContentPresenter
                {
                    Name = "PART_ContentPresenter",
                    [~ContentPresenter.ContentProperty] = new TemplateBinding(TabItem.HeaderProperty),
                    [~ContentPresenter.ContentTemplateProperty] = new TemplateBinding(TabItem.HeaderTemplateProperty),
                    RecognizesAccessKey = true,
                }.RegisterInNameScope(scope));
        }

        private static IControlTemplate TabControlTemplate()
        {
            return new FuncControlTemplate<TabControl>((parent, scope) =>
                new StackPanel
                {
                    Children =
                    {
                        new ItemsPresenter
                        {
                            Name = "PART_ItemsPresenter",
                        }.RegisterInNameScope(scope),
                        new ContentPresenter
                        {
                            Name = "PART_SelectedContentHost",
                            [~ContentPresenter.ContentProperty] = new TemplateBinding(TabControl.SelectedContentProperty),
                            [~ContentPresenter.ContentTemplateProperty] = new TemplateBinding(TabControl.SelectedContentTemplateProperty),
                        }.RegisterInNameScope(scope)
                    }
                });
        }

        private class Model
        {
            public int Id { get; set; }
            public string? Title { get; set; }
        }

        private sealed class CountingRowsPresenter : TreeDataGridRowsPresenter
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
