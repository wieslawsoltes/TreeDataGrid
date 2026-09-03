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
        public void Flat_clear_sort_restores_source_order_and_column_state()
        {
            var items = new ObservableCollection<Node> { new("B"), new("A"), new("C") };
            using var source = new FlatTreeDataGridSource<Node>(items);
            var column = new TextColumn<Node, string>("Name", x => x.Name);
            source.Columns.Add(column);
            source.SortBy(column, ListSortDirection.Ascending);

            ((ITreeDataGridSource)source).ClearSort();

            Assert.False(source.IsSorted);
            Assert.Null(column.SortDirection);
            Assert.Equal(new[] { "B", "A", "C" }, source.Rows.Select(x => ((Node)x.Model!).Name));
        }

        [Fact]
        public void Flat_move_preserves_selected_rows()
        {
            var first = new Node("First");
            var second = new Node("Second");
            var third = new Node("Third");
            var items = new ObservableCollection<Node> { first, second, third };
            using var source = new FlatTreeDataGridSource<Node>(items);
            source.RowSelection!.SingleSelect = false;
            source.RowSelection.Select(new IndexPath(0));
            source.RowSelection.Select(new IndexPath(1));

            source.MoveRows(source, new[] { new IndexPath(0), new IndexPath(1) },
                new IndexPath(2), RowDropPosition.After, RowMoveEffects.Move);

            Assert.Equal(new[] { third, first, second }, items);
            Assert.Equal(new[] { first, second }, source.RowSelection.SelectedItems);
            Assert.Equal(new[] { new IndexPath(1), new IndexPath(2) }, source.RowSelection.SelectedIndexes);
        }

        [Fact]
        public void Flat_move_tracks_selected_duplicate_occurrences()
        {
            var shared = new Node("Shared");
            var other = new Node("Other");
            var items = new ObservableCollection<Node> { shared, shared, other };
            using var source = new FlatTreeDataGridSource<Node>(items);
            source.RowSelection!.SelectedIndex = new IndexPath(1);

            source.MoveRows(source, new[] { new IndexPath(1) }, new IndexPath(2),
                RowDropPosition.After, RowMoveEffects.Move);

            Assert.Equal(new[] { shared, other, shared }, items);
            Assert.Equal(new IndexPath(2), source.RowSelection.SelectedIndex);
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
        public void Replacing_the_expander_column_is_rejected_before_mutation()
        {
            using var source = new HierarchicalTreeDataGridSource<Node>(new[] { new Node("Root") });
            var expander = new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children);
            source.Columns.Add(expander);
            var rows = source.Rows;

            Assert.Throws<InvalidOperationException>(() =>
                source.Columns[0] = new TextColumn<Node, string>("Other", x => x.Name));

            Assert.Same(expander, source.Columns[0]);
            Assert.Same(rows, source.Rows);
        }

        [Fact]
        public void Adding_or_removing_an_expander_is_rejected_before_mutation()
        {
            using var source = new HierarchicalTreeDataGridSource<Node>(new[] { new Node("Root") });
            var expander = new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children);
            source.Columns.Add(expander);

            Assert.Throws<InvalidOperationException>(() => source.Columns.Add(
                new HierarchicalExpanderColumn<Node>(
                    new TextColumn<Node, string>("Other", x => x.Name), x => x.Children)));
            Assert.Single(source.Columns);
            Assert.Throws<InvalidOperationException>(() => source.Columns.Remove(expander));
            Assert.Same(expander, Assert.Single(source.Columns));
        }

        [Fact]
        public void Column_reset_is_staged_and_retains_the_expander()
        {
            using var source = new HierarchicalTreeDataGridSource<Node>(new[] { new Node("Root") });
            var expander = new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children);
            var original = new TextColumn<Node, string>("Original", x => x.Name);
            var replacement = new TextColumn<Node, string>("Replacement", x => x.Name);
            source.Columns.Add(expander);
            source.Columns.Add(original);

            source.Columns.Reset(columns =>
            {
                columns.Remove(original);
                columns.Add(replacement);
            });

            Assert.Equal(new IColumn<Node>[] { expander, replacement }, source.Columns);
            Assert.Throws<InvalidOperationException>(() =>
                source.Columns.Reset(columns => columns.Remove(expander)));
            Assert.Equal(new IColumn<Node>[] { expander, replacement }, source.Columns);
        }

        [Fact]
        public void Batch_column_changes_validate_the_expander_before_mutation()
        {
            using var source = new HierarchicalTreeDataGridSource<Node>(new[] { new Node("Root") });
            var expander = new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children);
            source.Columns.Add(expander);

            Assert.Throws<InvalidOperationException>(() => source.Columns.InsertRange(1, add =>
            {
                add(new TextColumn<Node, string>("Ordinary", x => x.Name));
                add(new HierarchicalExpanderColumn<Node>(
                    new TextColumn<Node, string>("Other", x => x.Name), x => x.Children));
            }));
            Assert.Same(expander, Assert.Single(source.Columns));
            Assert.Throws<InvalidOperationException>(() => source.Columns.RemoveRange(0, 1));
            Assert.Same(expander, Assert.Single(source.Columns));
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
        public void Hierarchical_move_preserves_selected_rows()
        {
            var first = new Node("First") { Children = { new("Child") } };
            var second = new Node("Second");
            var items = new ObservableCollection<Node> { first, second };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
            source.RowSelection!.SingleSelect = false;
            source.RowSelection.Select(new IndexPath(0));
            source.RowSelection.Select(new IndexPath(0, 0));

            source.MoveRows(source, new[] { new IndexPath(0) }, new IndexPath(1),
                RowDropPosition.After, RowMoveEffects.Move);

            Assert.Equal(new[] { second, first }, items);
            Assert.Equal(new[] { new IndexPath(1), new IndexPath(1, 0) },
                source.RowSelection.SelectedIndexes);
        }

        [Fact]
        public void Hierarchical_move_prefers_exact_selected_source_paths()
        {
            var child = new Node("Child");
            var parent = new Node("Parent") { Children = { child } };
            var target = new Node("Target");
            var items = new ObservableCollection<Node> { parent, target };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
            source.RowSelection!.SingleSelect = false;
            source.RowSelection.Select(new IndexPath(0));
            source.RowSelection.Select(new IndexPath(0, 0));

            source.MoveRows(source, new[] { new IndexPath(0), new IndexPath(0, 0) },
                new IndexPath(1), RowDropPosition.Inside, RowMoveEffects.Move);

            Assert.Equal(new[] { parent, child }, target.Children);
            Assert.Equal(new[] { new IndexPath(0, 0), new IndexPath(0, 1) },
                source.RowSelection.SelectedIndexes);
        }

        [Theory]
        [InlineData(RowDropPosition.Inside, 0)]
        [InlineData(RowDropPosition.Before, 1)]
        [InlineData(RowDropPosition.After, 1)]
        public void Hierarchical_move_rejects_targets_in_the_moved_subtree(
            RowDropPosition position,
            int targetDepth)
        {
            var child = new Node("Child");
            var parent = new Node("Parent") { Children = { child } };
            var items = new ObservableCollection<Node> { parent };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
            var target = targetDepth == 0 ? new IndexPath(0) : new IndexPath(0, 0);

            Assert.Throws<InvalidOperationException>(() => source.MoveRows(
                source,
                new[] { new IndexPath(0) },
                target,
                position,
                RowMoveEffects.Move));

            Assert.Same(parent, Assert.Single(items));
            Assert.Same(child, Assert.Single(parent.Children));
        }

        [Fact]
        public void Moving_an_expanded_root_recomputes_the_flattened_destination()
        {
            var first = new Node("First") { Children = { new("Child") } };
            var second = new Node("Second");
            var items = new ObservableCollection<Node> { first, second };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
            source.Expand(new IndexPath(0));

            items.Move(0, 1);

            Assert.Equal(new[] { second, first }, items);
            Assert.Equal(new[] { "Second", "First" },
                source.Rows.Select(x => ((Node)x.Model!).Name));
        }

        [Fact]
        public void Sorted_row_removal_reports_the_removed_row_not_its_model()
        {
            var first = new Node("B");
            var second = new Node("A");
            var items = new ObservableCollection<Node> { first, second };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            var column = new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children);
            source.Columns.Add(column);
            source.SortBy(column, ListSortDirection.Ascending);
            var removedRow = source.Rows.Single(x => ReferenceEquals(x.Model, first));
            System.Collections.Specialized.NotifyCollectionChangedEventArgs? change = null;
            source.Rows.CollectionChanged += (_, e) => change = e;

            items.Remove(first);

            Assert.Same(removedRow, change!.OldItems![0]);
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
        public void ShowExpander_reflects_child_collection_changes_without_a_presentation()
        {
            var parent = new Node("Parent");
            using var source = new HierarchicalTreeDataGridSource<Node>(new[] { parent });
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
            var row = Assert.IsType<HierarchicalRow<Node>>(source.Rows[0]);

            Assert.False(row.ShowExpander);
            parent.Children.Add(new Node("Child"));
            Assert.True(row.ShowExpander);
            parent.Children.Clear();
            Assert.False(row.ShowExpander);
        }

        [Fact]
        public void Core_has_no_Avalonia_assembly_references()
        {
            Assert.DoesNotContain(typeof(FlatTreeDataGridSource<>).Assembly.GetReferencedAssemblies(), a => a.Name!.Contains("Avalonia", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Equal_single_index_paths_have_equal_hash_codes()
        {
            var encoded = new IndexPath(0);
            var array = new IndexPath(new[] { 0 });
            var paths = new HashSet<IndexPath> { encoded };

            Assert.Equal(encoded, array);
            Assert.Equal(encoded.GetHashCode(), array.GetHashCode());
            Assert.Contains(array, paths);
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
