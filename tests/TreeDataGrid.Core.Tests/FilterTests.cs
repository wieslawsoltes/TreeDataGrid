using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore.Models;
using Xunit;
using Node = TreeDataGridCore.Tests.SourceTests.Node;

namespace TreeDataGridCore.Tests
{
    public class FilterTests
    {
        [Fact]
        public void Flat_filter_displays_matching_items_and_can_be_cleared()
        {
            var items = new ObservableCollection<Node> { new("Alpha"), new("Beta"), new("Alpine") };
            using var source = new FlatTreeDataGridSource<Node>(items);
            source.Columns.Add(new TextColumn<Node, string>("Name", x => x.Name));

            source.Filter(x => x.Name.StartsWith("Al", StringComparison.Ordinal));

            Assert.True(source.IsFiltered);
            Assert.Equal(new[] { "Alpha", "Alpine" }, source.Rows.Select(x => ((Node)x.Model!).Name));
            Assert.Equal(2, ((ITreeDataGridSource)source).Items.Count());

            source.Filter(null);

            Assert.False(source.IsFiltered);
            Assert.Equal(new[] { "Alpha", "Beta", "Alpine" }, source.Rows.Select(x => ((Node)x.Model!).Name));
        }

        [Fact]
        public void Flat_refresh_filter_reevaluates_the_predicate_and_keeps_the_sort()
        {
            var items = new ObservableCollection<Node> { new("C"), new("x"), new("A") };
            using var source = new FlatTreeDataGridSource<Node>(items);
            var column = new TextColumn<Node, string>("Name", x => x.Name);
            source.Columns.Add(column);
            source.SortBy(column, ListSortDirection.Ascending);
            source.Filter(x => char.IsUpper(x.Name[0]));
            Assert.Equal(new[] { "A", "C" }, source.Rows.Select(x => ((Node)x.Model!).Name));

            items[1].Name = "B";
            source.RefreshFilter();

            Assert.Equal(new[] { "A", "B", "C" }, source.Rows.Select(x => ((Node)x.Model!).Name));
        }

        [Fact]
        public void Flat_selection_follows_the_filtered_view()
        {
            var items = new ObservableCollection<Node> { new("A"), new("B"), new("C") };
            using var source = new FlatTreeDataGridSource<Node>(items);
            source.Columns.Add(new TextColumn<Node, string>("Name", x => x.Name));
            source.Filter(x => x.Name != "A");

            source.RowSelection!.SelectedIndex = new IndexPath(0);

            Assert.Same(items[1], source.RowSelection.SelectedItem);
        }

        [Fact]
        public void Flat_move_rows_is_rejected_while_filtered()
        {
            var items = new ObservableCollection<Node> { new("A"), new("B") };
            using var source = new FlatTreeDataGridSource<Node>(items);
            source.Columns.Add(new TextColumn<Node, string>("Name", x => x.Name));
            source.Filter(x => true);

            Assert.Throws<NotSupportedException>(() => source.MoveRows(source, new[] { new IndexPath(0) },
                new IndexPath(1), RowDropPosition.After, RowMoveEffects.Move));
        }

        [Fact]
        public void Hierarchical_filter_applies_to_every_level()
        {
            var root = new Node("Root");
            var keep = new Node("Keep");
            keep.Children.Add(new Node("Keep child"));
            keep.Children.Add(new Node("Drop child"));
            root.Children.Add(keep);
            root.Children.Add(new Node("Drop"));
            var items = new ObservableCollection<Node> { root, new("Drop root") };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));

            source.Filter(x => !x.Name.StartsWith("Drop", StringComparison.Ordinal));
            source.ExpandAll();

            Assert.Equal(new[] { "Root", "Keep", "Keep child" }, source.Rows.Select(x => ((Node)x.Model!).Name));
            Assert.True(source.TryGetModelAt(new IndexPath(0, 0, 0), out var model));
            Assert.Equal("Keep child", model!.Name);

            source.Filter(null);
            source.ExpandAll();

            Assert.Equal(6, source.Rows.Count);
        }

        [Fact]
        public void Hierarchical_filter_hides_expanders_without_matching_children()
        {
            var parent = new Node("Parent");
            parent.Children.Add(new Node("Hidden"));
            using var source = new HierarchicalTreeDataGridSource<Node>(new[] { parent });
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));

            source.Filter(x => x.Name != "Hidden");

            var row = Assert.IsAssignableFrom<IExpander>(source.Rows[0]);
            Assert.False(row.ShowExpander);
        }
    }
}
