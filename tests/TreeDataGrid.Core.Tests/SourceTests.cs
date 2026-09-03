using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore.Models;
using Xunit;
namespace TreeDataGridCore.Tests
{
    public class SourceTests
    {
        [Fact]
        public void Existing_delegate_is_used_directly_for_values_sorting_and_edits()
        {
            Func<Node, string> getter = x => x.Name;
            var column = ValueColumn<Node, string>.FromDelegate("Name", getter, nameof(Node.Name), (x, value) => x.Name = value);
            using var source = new FlatTreeDataGridSource<Node>(new[] { new Node("B"), new Node("A") });
            source.Columns.Add(column);
            Assert.Same(getter, column.Getter);
            Assert.Null(column.GetterExpression);
            source.SortBy(column, ListSortDirection.Ascending);
            var first = (Node)source.Rows[0].Model!;
            Assert.Equal("A", first.Name);
            column.SetValue(first, "Changed");
            Assert.Equal("Changed", column.GetValue(first));
        }

        [Fact]
        public void Flat_sort_edits_and_selection_work_without_a_UI_runtime()
        {
            var items = new ObservableCollection<Node> { new("B"), new("A"), new("C") };
            using var source = new FlatTreeDataGridSource<Node>(items);
            var name = new TextColumn<Node, string>("Name", x => x.Name, (x, value) => x.Name = value);
            source.Columns.Add(name);
            source.RowSelection!.SelectedIndex = 0;
            Assert.True(source.SortBy(name, ListSortDirection.Ascending));
            Assert.Equal(new[] { "A", "B", "C" }, source.Rows.Select(x => ((Node)x.Model!).Name));
            Assert.Same(items[0], source.RowSelection.SelectedItem);
            var changes = 0;
            source.Rows.CollectionChanged += (_, _) => ++changes;
            items.Insert(0, new("D"));
            Assert.Equal(new IndexPath(1), source.RowSelection.SelectedIndex);
            Assert.Equal(1, changes);
            name.SetValue(items[1], "Z");
            source.SortBy(name, ListSortDirection.Ascending);
            Assert.Equal("Z", ((Node)source.Rows[^1].Model!).Name);
        }
        [Fact]
        public void Hierarchy_expansion_sorting_and_incremental_updates_share_model_indexes()
        {
            var parent = new Node("Parent") { Children = { new("B"), new("A") } };
            using var source = new HierarchicalTreeDataGridSource<Node>(new[] { parent });
            var column = new HierarchicalExpanderColumn<Node>(new TextColumn<Node, string>("Name", x => x.Name), x => x.Children);
            source.Columns.Add(column);
            Assert.Single(source.Rows);
            source.Expand(0);
            Assert.Equal(3, source.Rows.Count);
            source.RowSelection!.SelectedIndex = new(0, 0);
            source.SortBy(column, ListSortDirection.Ascending);
            Assert.Equal(new IndexPath(0, 1), source.Rows.RowIndexToModelIndex(1));
            parent.Children.Insert(0, new("C"));
            Assert.Equal(new IndexPath(0, 1), source.RowSelection.SelectedIndex);
            Assert.Equal(4, source.Rows.Count);
            source.Collapse(0);
            Assert.Single(source.Rows);
            Assert.Equal("B", source.RowSelection.SelectedItem!.Name);
            source.Expand(0);
            Assert.Equal(4, source.Rows.Count);
        }
        [Fact]
        public void Bound_expansion_is_initialized_and_changes_without_a_view()
        {
            var parent = new BoundNode { IsExpanded = true, Children = { new() } };
            using var source = new HierarchicalTreeDataGridSource<BoundNode>(new[] { parent });
            source.Columns.Add(new HierarchicalExpanderColumn<BoundNode>(new TextColumn<BoundNode, bool>("Expanded", x => x.IsExpanded),
                x => x.Children, isExpandedSelector: x => x.IsExpanded));
            Assert.Equal(2, source.Rows.Count);
            parent.IsExpanded = false;
            Assert.Single(source.Rows);
            source.Expand(0);
            Assert.True(parent.IsExpanded);
            Assert.Equal(2, source.Rows.Count);
        }
        private sealed class BoundNode : INotifyPropertyChanged
        {
            private bool _expanded;
            public bool IsExpanded
            {
                get => _expanded;
                set { if (_expanded == value) return; _expanded = value; PropertyChanged?.Invoke(this, new(nameof(IsExpanded))); }
            }
            public ObservableCollection<BoundNode> Children { get; } = new();
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        [Fact]
        public void Moving_the_expander_column_preserves_rows_and_expansion()
        {
            using var source = new HierarchicalTreeDataGridSource<Node>(new[] { new Node("Root") { Children = { new("Child") } } });
            var expander = new HierarchicalExpanderColumn<Node>(new TextColumn<Node, string>("Name", x => x.Name), x => x.Children);
            source.Columns.Add(expander);
            source.Columns.Add(new TextColumn<Node, string>("Other", x => x.Name));
            source.Expand(0);
            var rows = source.Rows;
            source.Columns.Move(0, 1);
            Assert.Same(expander, source.Columns[1]);
            Assert.Same(rows, source.Rows);
            Assert.Equal(2, rows.Count);
        }

        [Fact]
        public void Items_source_view_can_be_disposed_without_a_registered_listener()
        {
            var items = new ObservableCollection<Node>();
            var view = new TreeDataGridItemsSourceView(items);
            System.Collections.Specialized.NotifyCollectionChangedEventHandler handler = (_, _) => { };

            view.CollectionChanged -= handler;
            view.Dispose();
            view.Dispose();
        }

        [Fact]
        public void Replacing_a_hierarchical_item_disposes_the_replaced_row()
        {
            var replaced = new BoundNode { Children = { new() } };
            var items = new ObservableCollection<BoundNode> { replaced };
            using var source = new HierarchicalTreeDataGridSource<BoundNode>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<BoundNode>(
                new TextColumn<BoundNode, bool>("Expanded", x => x.IsExpanded),
                x => x.Children,
                isExpandedSelector: x => x.IsExpanded));
            var replacedRow = Assert.IsType<HierarchicalRow<BoundNode>>(source.Rows[0]);

            items[0] = new BoundNode();
            replaced.IsExpanded = true;

            Assert.False(replacedRow.IsExpanded);
        }

        [Fact]
        public void Hierarchical_move_with_none_position_is_a_no_op()
        {
            var first = new Node("First");
            var second = new Node("Second");
            var items = new ObservableCollection<Node> { first, second };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));

            source.MoveRows(source, new[] { new IndexPath(0) }, new IndexPath(1),
                RowDropPosition.None, RowMoveEffects.Move);

            Assert.Same(first, items[0]);
            Assert.Same(second, items[1]);
        }

        [Fact]
        public void Column_replacement_reports_new_and_old_items_in_the_correct_fields()
        {
            var columns = new ColumnList<Node>();
            var original = new TextColumn<Node, string>("Original", x => x.Name);
            var replacement = new TextColumn<Node, string>("Replacement", x => x.Name);
            System.Collections.Specialized.NotifyCollectionChangedEventArgs? change = null;
            columns.Add(original);
            columns.CollectionChanged += (_, e) => change = e;

            columns[0] = replacement;

            Assert.Same(replacement, change!.NewItems![0]);
            Assert.Same(original, change.OldItems![0]);
        }

        [Fact]
        public void Hierarchical_move_preserves_order_across_parent_groups()
        {
            var first = new Node("First") { Children = { new("A") } };
            var second = new Node("Second") { Children = { new("B") } };
            var target = new Node("Target");
            var items = new ObservableCollection<Node> { first, second, target };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));

            source.MoveRows(source, new[] { new IndexPath(0, 0), new IndexPath(1, 0) },
                new IndexPath(2), RowDropPosition.Inside, RowMoveEffects.Move);

            Assert.Equal(new[] { "A", "B" }, target.Children.Select(x => x.Name));
        }

        [Fact]
        public void Hierarchical_move_resolves_all_source_paths_before_removal()
        {
            var first = new Node("First");
            var movedChild = new Node("Child");
            var second = new Node("Second") { Children = { movedChild } };
            var target = new Node("Target");
            var items = new ObservableCollection<Node> { first, second, target };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));

            source.MoveRows(source, new[] { new IndexPath(0), new IndexPath(1, 0) },
                new IndexPath(2), RowDropPosition.Inside, RowMoveEffects.Move);

            Assert.Equal(new[] { second, target }, items);
            Assert.Equal(new[] { first, movedChild }, target.Children);
        }

        [Fact]
        public void Nested_expansion_binding_tracks_leaf_and_intermediate_changes()
        {
            var child = new NestedBoundNode { Children = { new() } };
            var parent = new NestedBoundNode { Children = { child } };
            using var source = new HierarchicalTreeDataGridSource<NestedBoundNode>(new[] { parent });
            source.Columns.Add(new HierarchicalExpanderColumn<NestedBoundNode>(
                new TextColumn<NestedBoundNode, bool>("Expanded", x => x.Expansion.IsExpanded),
                x => x.Children,
                isExpandedSelector: x => x.Expansion.IsExpanded));

            Assert.Single(source.Rows);
            parent.Expansion.IsExpanded = true;
            Assert.Equal(2, source.Rows.Count);
            child.Expansion.IsExpanded = true;
            Assert.Equal(3, source.Rows.Count);
            child.Expansion = new ExpansionState();
            Assert.Equal(2, source.Rows.Count);
            parent.Expansion = new ExpansionState();
            Assert.Single(source.Rows);
            parent.Expansion = new ExpansionState { IsExpanded = true };
            Assert.Equal(2, source.Rows.Count);
            child.Expansion = new ExpansionState { IsExpanded = true };
            Assert.Equal(3, source.Rows.Count);
        }

        [Fact]
        public void Core_has_no_Avalonia_assembly_references()
        {
            Assert.DoesNotContain(typeof(FlatTreeDataGridSource<>).Assembly.GetReferencedAssemblies(), a => a.Name!.Contains("Avalonia", StringComparison.OrdinalIgnoreCase));
        }
        public sealed class Node
        {
            public Node(string name) => Name = name;
            public string Name { get; set; }
            public ObservableCollection<Node> Children { get; } = new();
        }

        private sealed class NestedBoundNode : INotifyPropertyChanged
        {
            private ExpansionState _expansion = new();
            public ObservableCollection<NestedBoundNode> Children { get; } = new();
            public ExpansionState Expansion
            {
                get => _expansion;
                set
                {
                    if (ReferenceEquals(_expansion, value))
                        return;
                    _expansion = value;
                    PropertyChanged?.Invoke(this, new(nameof(Expansion)));
                }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private sealed class ExpansionState : INotifyPropertyChanged
        {
            private bool _isExpanded;
            public bool IsExpanded
            {
                get => _isExpanded;
                set
                {
                    if (_isExpanded == value)
                        return;
                    _isExpanded = value;
                    PropertyChanged?.Invoke(this, new(nameof(IsExpanded)));
                }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }
    }
}
