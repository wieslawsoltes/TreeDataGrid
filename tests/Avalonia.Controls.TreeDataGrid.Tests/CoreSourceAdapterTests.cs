using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls.Adapters;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Core = global::TreeDataGridCore;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests
{
    public class CoreSourceAdapterTests
    {
        [AvaloniaFact]
        public void Presentation_uses_neutral_sort_selection_and_column_state()
        {
            var items = new ObservableCollection<Node> { new("B"), new("A") };
            using var source = Flat(items);
            source.RowSelection!.SelectedIndex = 0;
            using var adapter = new TreeDataGridSourceAdapter<Node>(source);
            Assert.Same(source.RowSelection.SelectedItem, adapter.RowSelection!.SelectedItem);
            adapter.SortBy(adapter.Columns[0], ListSortDirection.Ascending);
            Assert.Equal("A", ((Node)adapter.Rows[0].Model!).Name);
            Assert.Equal(1, adapter.Rows.ModelIndexToRowIndex(adapter.RowSelection.SelectedIndex));
            adapter.RowSelection.SelectedIndex = 1;
            Assert.Equal(new Core.IndexPath(1), source.RowSelection.SelectedIndex);
            source.RowSelection.SelectedIndex = 0;
            Assert.Equal(new IndexPath(0), adapter.RowSelection.SelectedIndex);
            adapter.Columns.SetColumnWidth(0, new GridLength(142));
            Assert.Equal(new Core.GridLength(142), source.Columns[0].Width);
            source.Columns[0].Width = Core.GridLength.Star;
            Assert.Equal(GridLength.Star, adapter.Columns[0].Width);
            source.Columns[0].IsVisible = false;
            Assert.Empty(adapter.Columns);
            source.Columns[0].IsVisible = true;
            Assert.Single(adapter.Columns);
        }
        [AvaloniaFact]
        public void Hierarchy_cells_expand_the_neutral_projection_and_update_models()
        {
            var parent = new Node("Root") { Children = { new("Child") } };
            using var source = new Core.HierarchicalTreeDataGridSource<Node>(new[] { parent });
            source.Columns.Add(new Core.Models.HierarchicalExpanderColumn<Node>(
                new Core.Models.TextColumn<Node, string>("Name", x => x.Name), x => x.Children,
                setIsExpanded: (x, expanded) => x.Expanded = expanded));
            using var adapter = new TreeDataGridSourceAdapter<Node>(source);
            var cell = (IExpanderCell)adapter.Rows.RealizeCell(adapter.Columns[0], 0, 0);
            cell.IsExpanded = true;
            Assert.True(parent.Expanded);
            Assert.Equal(2, source.Rows.Count);
            Assert.Equal(2, adapter.Rows.Count);
            parent.Children.Add(new("Second"));
            Assert.Equal(3, adapter.Rows.Count);
            source.Collapse(0);
            Assert.False(cell.IsExpanded);
            Assert.Single(adapter.Rows);
            adapter.Rows.UnrealizeCell(cell, 0, 0);
        }
        [AvaloniaFact]
        public void Disposal_detaches_presentation_and_allows_reattachment()
        {
            var items = new ObservableCollection<Node> { new("A") };
            using var source = Flat(items);
            var adapter = new TreeDataGridSourceAdapter<Node>(source);
            var selectionEvents = 0;
            ((ITreeDataGridSelectionInteraction)adapter.Selection!).SelectionChanged += (_, _) => ++selectionEvents;
            source.RowSelection!.SelectedIndex = 0;
            Assert.Equal(1, selectionEvents);
            adapter.Dispose();
            adapter.Dispose();
            source.RowSelection.Clear();
            items.Add(new("B"));
            Assert.Equal(1, selectionEvents);
            using var replacement = new TreeDataGridSourceAdapter<Node>(source);
            Assert.Equal(2, replacement.Rows.Count);
            source.RowSelection.SelectedIndex = 1;
            Assert.Same(items[1], replacement.RowSelection!.SelectedItem);
        }
        [AvaloniaFact]
        public void Control_renders_edits_scrolls_and_navigates_a_neutral_source()
        {
            var items = new ObservableCollection<Node>(Enumerable.Range(0, 100).Select(x => new Node($"Row {x}")));
            using var source = Flat(items);
            using var adapter = new TreeDataGridSourceAdapter<Node>(source);
            var grid = new TreeDataGrid { Source = adapter, Template = TestTemplates.TreeDataGridTemplate() };
            var window = new TestWindow(grid)
            {
                Styles = { TestTemplates.TreeDataGridRowStyle, new Style(x => x.OfType<TreeDataGridCell>()) { Setters = { new Setter(TreeDataGridCell.HeightProperty, 10.0) } } }
            };
            try
            {
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.NotEmpty(grid.RowsPresenter!.GetRealizedElements());
                source.RowSelection!.SelectedIndex = 0;
                ((ITreeDataGridSelectionInteraction)adapter.Selection!).OnKeyDown(grid, new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
                Assert.Equal(new Core.IndexPath(1), source.RowSelection.SelectedIndex);
                var cell = adapter.Rows.RealizeCell(adapter.Columns[0], 0, 1);
                ((ITextCell)cell).Text = "Edited";
                Assert.Equal("Edited", items[1].Name);
                adapter.Rows.UnrealizeCell(cell, 0, 1);
                grid.Scroll!.Offset = new Vector(0, 400);
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.NotEmpty(grid.RowsPresenter.GetRealizedElements());
                items.Insert(0, new("Inserted"));
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.Equal(new Core.IndexPath(2), source.RowSelection.SelectedIndex);
            }
            finally { window.Close(); }
        }
        [AvaloniaFact]
        public void Model_binding_recreates_only_presentation_after_detach()
        {
            using var source = Flat(new() { new("A") });
            var grid = new TreeDataGrid { Model = source, Template = TestTemplates.TreeDataGridTemplate() };
            var window = new TestWindow(grid) { Styles = { TestTemplates.TreeDataGridRowStyle } };
            try
            {
                window.UpdateLayout();
                var first = grid.Source;
                Assert.NotNull(first);
                window.Content = null;
                Assert.Null(grid.Source);
                Assert.Same(source, grid.Model);
                source.RowSelection!.SelectedIndex = 0;
                window.Content = grid;
                window.UpdateLayout();
                Assert.NotNull(grid.Source);
                Assert.NotSame(first, grid.Source);
                Assert.Same(source.RowSelection.SelectedItem, grid.RowSelection!.SelectedItem);
                // The explicit legacy Source API still takes precedence when assigned.
                using var legacy = new FlatTreeDataGridSource<Node>(Array.Empty<Node>());
                grid.Source = legacy;
                Assert.Null(grid.Model);
                Assert.Same(legacy, grid.Source);
            }
            finally { window.Close(); }
        }

        [AvaloniaFact]
        public void V12_selection_mode_and_events_use_the_neutral_selection()
        {
            using var source = Flat(new() { new("A"), new("B") });
            source.Selection = null;
            var grid = new TreeDataGrid { SelectionMode = TreeDataGridSelectionMode.Row | TreeDataGridSelectionMode.Multiple, Model = source };
            Assert.NotNull(source.RowSelection);
            Assert.False(source.RowSelection!.SingleSelect);
            var eventCount = 0;
            grid.SelectionChanged += (_, e) =>
            {
                ++eventCount;
                Assert.Single(e.SelectedIndexes);
                Assert.Equal("B", ((Node)e.SelectedItems[0]!).Name);
            };
            var window = new TestWindow(grid);
            try
            {
                source.RowSelection.SelectedIndex = 1;
                Assert.Equal(1, eventCount);
                grid.SelectionMode = TreeDataGridSelectionMode.Row;
                Assert.True(source.RowSelection.SingleSelect);
                grid.Model = null;
            }
            finally { window.Close(); }
        }

        private static Core.FlatTreeDataGridSource<Node> Flat(ObservableCollection<Node> items)
        {
            var source = new Core.FlatTreeDataGridSource<Node>(items);
            source.Columns.Add(new Core.Models.TextColumn<Node, string>("Name", x => x.Name, (x, value) => x.Name = value));
            return source;
        }
        public sealed class Node
        {
            public Node(string name) => Name = name;
            public string Name { get; set; }
            public bool Expanded { get; set; }
            public ObservableCollection<Node> Children { get; } = new();
        }
    }
}
