using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using Xunit;

namespace TreeDataGrid.Core.Tests
{
    public class CellSelectionTests
    {
        [Fact]
        public void Range_selects_columns_and_rows_in_one_notification()
        {
            using var source = Flat();
            using var selection = new TreeDataGridCellSelectionModel<Node>(source) { SingleSelect = false };
            source.Selection = selection;
            var typed = 0;
            var untyped = 0;
            selection.SelectionChanged += (_, _) => { ++typed; Assert.Equal(4, selection.Count); };
            ((ITreeDataGridCellSelectionModel)selection).SelectionChanged += (_, _) => ++untyped;
            selection.SetSelectedRange(new(0, 0), 2, 2);
            Assert.Equal(1, typed);
            Assert.Equal(1, untyped);
            Assert.Equal(new[] { new CellIndex(0, 0), new(1, 0), new(0, 1), new(1, 1) }, selection.SelectedIndexes);
            Assert.Equal(new CellIndex(0, 0), selection.AnchorIndex);
            Assert.Equal(new CellIndex(1, 1), selection.RangeAnchorIndex);
        }

        [Fact]
        public void Sorted_range_uses_display_order_and_keeps_model_indexes()
        {
            using var source = Flat();
            source.SortBy(source.Columns[0], ListSortDirection.Ascending);
            using var selection = new TreeDataGridCellSelectionModel<Node>(source) { SingleSelect = false };
            selection.SetSelectedRange(new(1, source.Rows.RowIndexToModelIndex(0)), 1, 2);
            Assert.True(selection.IsSelected(1, new IndexPath(1))); // A
            Assert.True(selection.IsSelected(1, new IndexPath(2))); // B
            Assert.False(selection.IsSelected(1, new IndexPath(0))); // C
            Assert.Equal(new CellIndex(1, 1), selection.SelectedIndex);
            source.ClearSort();
            Assert.True(selection.IsSelected(1, new IndexPath(1)));
        }

        [Fact]
        public void Backwards_and_single_cell_ranges_have_valid_endpoints()
        {
            using var source = Flat();
            using var selection = new TreeDataGridCellSelectionModel<Node>(source) { SingleSelect = false };
            selection.SetSelectedRange(new(1, 2), -2, -3);
            Assert.Equal(6, selection.Count);
            Assert.Equal(new CellIndex(1, 2), selection.AnchorIndex);
            Assert.Equal(new CellIndex(0, 0), selection.RangeAnchorIndex);
            selection.SingleSelect = true;
            Assert.Single(selection.SelectedIndexes);
            selection.SetSelectedRange(new(0, 0), 2, 3);
            Assert.Equal(new CellIndex(0, 0), Assert.Single(selection.SelectedIndexes));
            selection.Clear();
            Assert.Empty(selection.SelectedIndexes);
        }

        [Fact]
        public void Insert_remove_move_and_reset_track_selected_models_and_notify()
        {
            var items = new ObservableCollection<Node> { new("A"), new("B"), new("C") };
            using var source = Flat(items);
            using var selection = new TreeDataGridCellSelectionModel<Node>(source);
            source.Selection = selection;
            selection.SelectedIndex = new(1, 1);
            var events = 0;
            selection.SelectionChanged += (_, _) => ++events;
            items.Insert(0, new("New"));
            Assert.Equal(new CellIndex(1, 2), selection.SelectedIndex);
            Assert.True(events > 0);
            items.Move(2, 0);
            Assert.Equal(new CellIndex(1, 0), selection.SelectedIndex);
            items.RemoveAt(0);
            Assert.Empty(selection.SelectedIndexes);
            selection.SelectedIndex = new(0, 0);
            source.Items = new[] { new Node("Replacement") };
            Assert.Empty(selection.SelectedIndexes);
        }

        [Fact]
        public void Column_insert_remove_and_hidden_state_preserve_source_identity()
        {
            using var source = Flat();
            using var selection = new TreeDataGridCellSelectionModel<Node>(source);
            selection.SelectedIndex = new(1, 0);
            source.Columns[0].IsVisible = false;
            Assert.Equal(new CellIndex(1, 0), selection.SelectedIndex);
            source.Columns.Insert(0, new TextColumn<Node, string>("New", x => x.Name));
            Assert.Equal(new CellIndex(2, 0), selection.SelectedIndex);
            source.Columns.RemoveAt(2);
            Assert.Empty(selection.SelectedIndexes);
        }

        [Fact]
        public void Hierarchical_range_uses_nested_model_paths()
        {
            using var source = new HierarchicalTreeDataGridSource<Node>(new[]
            {
                new Node("Root") { Children = { new("A"), new("B") } }
            });
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
            source.Expand(0);
            using var selection = new TreeDataGridCellSelectionModel<Node>(source) { SingleSelect = false };
            selection.SetSelectedRange(new(0, new IndexPath(0, 0)), 1, 2);
            Assert.Equal(new[] { new CellIndex(0, new IndexPath(0, 0)), new(0, new IndexPath(0, 1)) }, selection.SelectedIndexes);
            source.Collapse(0);
            Assert.Equal(2, selection.Count);
            source.Expand(0);
            Assert.True(selection.IsSelected(new(0, new IndexPath(0, 1))));
        }

        [Fact]
        public void Source_move_preserves_cells_on_the_moved_rows()
        {
            using var source = Flat();
            using var selection = new TreeDataGridCellSelectionModel<Node>(source) { SingleSelect = false };
            source.Selection = selection;
            selection.SetSelectedRange(new(1, 0), 1, 2);
            source.MoveRows(source, new[] { new IndexPath(0), new IndexPath(1) }, new IndexPath(2), RowDropPosition.After, RowMoveEffects.Move);
            Assert.Equal(new[] { new CellIndex(1, 1), new(1, 2) }, selection.SelectedIndexes);
        }

        [Fact]
        public void Hierarchical_source_move_preserves_selected_child_cells()
        {
            using var source = new HierarchicalTreeDataGridSource<Node>(new ObservableCollection<Node>
            {
                new("Root") { Children = { new("Child") } }, new("Target")
            });
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
            source.Expand(0);
            using var selection = new TreeDataGridCellSelectionModel<Node>(source);
            source.Selection = selection;
            selection.SelectedIndex = new(0, new IndexPath(0, 0));
            source.MoveRows(source, new[] { new IndexPath(0, 0) }, new IndexPath(1), RowDropPosition.Inside, RowMoveEffects.Move);
            Assert.Equal(new CellIndex(0, new IndexPath(1, 0)), selection.SelectedIndex);
        }

        [Theory]
        [InlineData(-1, 0, 1, 1)]
        [InlineData(0, 10, 1, 1)]
        [InlineData(0, 0, 0, 1)]
        [InlineData(0, 0, 1, 0)]
        public void Invalid_or_empty_range_clears_selection(int column, int row, int columns, int rows)
        {
            using var source = Flat();
            using var selection = new TreeDataGridCellSelectionModel<Node>(source);
            selection.SelectedIndex = new(0, 0);
            selection.SetSelectedRange(new(column, row), columns, rows);
            Assert.Empty(selection.SelectedIndexes);
        }

        [Fact]
        public void Disposed_selection_stops_observing_collections()
        {
            var items = new ObservableCollection<Node> { new("A") };
            using var source = Flat(items);
            var selection = new TreeDataGridCellSelectionModel<Node>(source);
            selection.SelectedIndex = new(0, 0);
            selection.Dispose();
            var events = 0;
            selection.SelectionChanged += (_, _) => ++events;
            items.Clear();
            source.Columns.Clear();
            Assert.Equal(0, events);
            Assert.Empty(selection.SelectedIndexes);
        }

        private static FlatTreeDataGridSource<Node> Flat(ObservableCollection<Node>? items = null) =>
            new(items ?? new() { new("C"), new("A"), new("B") })
            {
                Columns = { new TextColumn<Node, string>("Name", x => x.Name), new TextColumn<Node, string>("Other", x => x.Name) }
            };
        private sealed record Node(string Name)
        {
            public ObservableCollection<Node> Children { get; } = new();
        }
    }
}
