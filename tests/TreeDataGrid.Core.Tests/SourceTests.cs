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
        public void Flat_move_rejects_duplicate_source_indexes_before_mutation()
        {
            var first = new Node("First");
            var second = new Node("Second");
            var items = new ObservableCollection<Node> { first, second };
            using var source = new FlatTreeDataGridSource<Node>(items);

            Assert.Throws<ArgumentException>(() => source.MoveRows(
                source,
                new[] { new IndexPath(0), new IndexPath(0) },
                new IndexPath(1),
                RowDropPosition.After,
                RowMoveEffects.Move));

            Assert.Equal(new[] { first, second }, items);
        }

        [Fact]
        public void Flat_move_rejects_an_invalid_target_before_mutation()
        {
            var first = new Node("First");
            var second = new Node("Second");
            var items = new ObservableCollection<Node> { first, second };
            using var source = new FlatTreeDataGridSource<Node>(items);

            Assert.Throws<ArgumentOutOfRangeException>(() => source.MoveRows(
                source, new[] { new IndexPath(0) }, new IndexPath(5),
                RowDropPosition.Before, RowMoveEffects.Move));

            Assert.Equal(new[] { first, second }, items);
        }

        [Fact]
        public void Flat_move_rejects_an_invalid_source_before_mutation()
        {
            var first = new Node("First");
            var second = new Node("Second");
            var items = new ObservableCollection<Node> { first, second };
            using var source = new FlatTreeDataGridSource<Node>(items);

            Assert.Throws<ArgumentOutOfRangeException>(() => source.MoveRows(
                source, new[] { new IndexPath(5) }, new IndexPath(1),
                RowDropPosition.Before, RowMoveEffects.Move));

            Assert.Equal(new[] { first, second }, items);
        }

        [Fact]
        public void Flat_move_rejects_a_read_only_list_before_mutation()
        {
            var items = new[] { new Node("First"), new Node("Second") };
            using var source = new FlatTreeDataGridSource<Node>(items);

            Assert.Throws<InvalidOperationException>(() => source.MoveRows(
                source, new[] { new IndexPath(0) }, new IndexPath(1),
                RowDropPosition.After, RowMoveEffects.Move));

            Assert.Equal(new[] { "First", "Second" }, items.Select(x => x.Name));
        }

        [Fact]
        public void Flat_move_none_is_a_no_op_for_a_read_only_list()
        {
            var items = new[] { new Node("First"), new Node("Second") };
            using var source = new FlatTreeDataGridSource<Node>(items);

            source.MoveRows(source, new[] { new IndexPath(0) }, new IndexPath(1),
                RowDropPosition.None, RowMoveEffects.Move);

            Assert.Equal(new[] { "First", "Second" }, items.Select(x => x.Name));
        }

        [Fact]
        public void Flat_move_refreshes_materialized_rows_for_a_plain_list()
        {
            var first = new Node("First");
            var second = new Node("Second");
            var items = new List<Node> { first, second };
            using var source = new FlatTreeDataGridSource<Node>(items);
            Assert.Equal(new[] { first, second }, source.Rows.Select(x => x.Model));

            source.MoveRows(source, new[] { new IndexPath(0) }, new IndexPath(1),
                RowDropPosition.After, RowMoveEffects.Move);

            Assert.Equal(new[] { second, first }, items);
            Assert.Equal(new[] { second, first }, source.Rows.Select(x => x.Model));
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
        public void Hierarchical_items_replacement_raises_property_changed()
        {
            using var source = new HierarchicalTreeDataGridSource<Node>(new[] { new Node("Old") });
            string? propertyName = null;
            source.PropertyChanged += (_, e) => propertyName = e.PropertyName;

            source.Items = new[] { new Node("New") };

            Assert.Equal(nameof(source.Items), propertyName);
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
        public void Hierarchical_move_uses_the_original_after_target_offset()
        {
            var first = new Node("First");
            var middle = new Node("Middle");
            var target = new Node("Target");
            var items = new ObservableCollection<Node> { first, middle, target };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);

            source.MoveRows(source, new[] { new IndexPath(0), new IndexPath(2) },
                new IndexPath(2), RowDropPosition.After, RowMoveEffects.Move);

            Assert.Equal(new[] { middle, first, target }, items);
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
        public void Hierarchical_move_preserves_the_primary_selection()
        {
            var first = new Node("First");
            var primary = new Node("Primary");
            var target = new Node("Target");
            var items = new ObservableCollection<Node> { first, primary, target };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
            source.RowSelection!.SingleSelect = false;
            source.RowSelection.Select(new IndexPath(1));
            source.RowSelection.Select(new IndexPath(0));

            source.MoveRows(source, new[] { new IndexPath(0), new IndexPath(1) },
                new IndexPath(2), RowDropPosition.After, RowMoveEffects.Move);

            Assert.Equal(new[] { target, first, primary }, items);
            Assert.Equal(new IndexPath(2), source.RowSelection.SelectedIndex);
            Assert.Same(primary, source.RowSelection.SelectedItem);
            Assert.Equal(new[] { new IndexPath(1), new IndexPath(2) },
                source.RowSelection.SelectedIndexes);
        }

        [Fact]
        public void Hierarchical_move_on_plain_list_preserves_moved_primary_and_retained_selection()
        {
            var primary = new Node("Primary");
            var retained = new Node("Retained");
            var target = new Node("Target");
            var items = new List<Node> { primary, retained, target };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
            source.RowSelection!.SingleSelect = false;
            source.RowSelection.Select(new IndexPath(0));
            source.RowSelection.Select(new IndexPath(1));

            source.MoveRows(source, new[] { new IndexPath(0) }, new IndexPath(2),
                RowDropPosition.After, RowMoveEffects.Move);

            Assert.Equal(new[] { retained, target, primary }, items);
            Assert.Equal(new IndexPath(2), source.RowSelection.SelectedIndex);
            Assert.Same(primary, source.RowSelection.SelectedItem);
            Assert.Equal(new[] { new IndexPath(0), new IndexPath(2) },
                source.RowSelection.SelectedIndexes);
        }

        [Fact]
        public void Hierarchical_move_on_plain_list_remaps_an_unmoved_selection()
        {
            var moved = new Node("Moved");
            var selected = new Node("Selected");
            var target = new Node("Target");
            var items = new List<Node> { moved, selected, target };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.RowSelection!.SelectedIndex = new IndexPath(1);

            source.MoveRows(source, new[] { new IndexPath(0) }, new IndexPath(2),
                RowDropPosition.After, RowMoveEffects.Move);

            Assert.Equal(new[] { selected, target, moved }, items);
            Assert.Equal(new IndexPath(0), source.RowSelection.SelectedIndex);
            Assert.Same(selected, source.RowSelection.SelectedItem);
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

        [Fact]
        public void Hierarchical_move_maps_selections_through_the_deepest_moved_ancestor()
        {
            var grandchild = new Node("Grandchild");
            var extracted = new Node("Extracted") { Children = { grandchild } };
            var retained = new Node("Retained");
            var parent = new Node("Parent") { Children = { extracted, retained } };
            var target = new Node("Target");
            var items = new ObservableCollection<Node> { parent, target };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
            source.RowSelection!.SingleSelect = false;
            source.RowSelection.Select(new IndexPath(0, 0, 0));
            source.RowSelection.Select(new IndexPath(0, 1));

            source.MoveRows(source, new[] { new IndexPath(0), new IndexPath(0, 0) },
                new IndexPath(1), RowDropPosition.Inside, RowMoveEffects.Move);

            Assert.Equal(new[] { parent, extracted }, target.Children);
            Assert.Same(retained, Assert.Single(parent.Children));
            Assert.Equal(new[] { new IndexPath(0, 0, 0), new IndexPath(0, 1, 0) },
                source.RowSelection.SelectedIndexes);
            Assert.Equal(new[] { retained, grandchild }, source.RowSelection.SelectedItems);
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
        public void Hierarchical_move_rejects_duplicate_source_paths_before_mutation()
        {
            var first = new Node("First");
            var second = new Node("Second");
            var items = new ObservableCollection<Node> { first, second };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);

            Assert.Throws<ArgumentException>(() => source.MoveRows(
                source,
                new[] { new IndexPath(0), new IndexPath(0) },
                new IndexPath(1),
                RowDropPosition.After,
                RowMoveEffects.Move));

            Assert.Equal(new[] { first, second }, items);
        }

        [Fact]
        public void Hierarchical_move_rejects_an_invalid_target_before_mutation()
        {
            var first = new Node("First");
            var second = new Node("Second");
            var items = new ObservableCollection<Node> { first, second };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);

            Assert.Throws<ArgumentOutOfRangeException>(() => source.MoveRows(
                source, new[] { new IndexPath(0) }, new IndexPath(5),
                RowDropPosition.Before, RowMoveEffects.Move));

            Assert.Equal(new[] { first, second }, items);
        }

        [Fact]
        public void Hierarchical_move_rejects_an_invalid_source_before_mutation()
        {
            var first = new Node("First");
            var second = new Node("Second");
            var items = new ObservableCollection<Node> { first, second };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);

            Assert.Throws<ArgumentOutOfRangeException>(() => source.MoveRows(
                source, new[] { new IndexPath(5) }, new IndexPath(1),
                RowDropPosition.Before, RowMoveEffects.Move));

            Assert.Equal(new[] { first, second }, items);
        }

        [Fact]
        public void Hierarchical_move_uses_the_requested_path_for_a_shared_target_collection()
        {
            var sharedChildren = new ObservableCollection<SharedNode>();
            var moved = new SharedNode("Moved");
            var firstParent = new SharedNode("First", sharedChildren);
            var secondParent = new SharedNode("Second", sharedChildren);
            var items = new ObservableCollection<SharedNode> { moved, firstParent, secondParent };
            using var source = new HierarchicalTreeDataGridSource<SharedNode>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<SharedNode>(
                new TextColumn<SharedNode, string>("Name", x => x.Name), x => x.Children));
            source.RowSelection!.SelectedIndex = new IndexPath(0);

            source.MoveRows(source, new[] { new IndexPath(0) }, new IndexPath(2),
                RowDropPosition.Inside, RowMoveEffects.Move);

            Assert.Same(moved, Assert.Single(sharedChildren));
            Assert.Equal(new IndexPath(1, 0), source.RowSelection.SelectedIndex);
        }

        [Fact]
        public void Hierarchical_move_groups_aliased_source_collections_by_identity()
        {
            var first = new SharedNode("A");
            var second = new SharedNode("B");
            var third = new SharedNode("C");
            var sharedChildren = new ObservableCollection<SharedNode> { first, second, third };
            var firstParent = new SharedNode("First", sharedChildren);
            var secondParent = new SharedNode("Second", sharedChildren);
            var target = new SharedNode("Target");
            var items = new ObservableCollection<SharedNode> { firstParent, secondParent, target };
            using var source = new HierarchicalTreeDataGridSource<SharedNode>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<SharedNode>(
                new TextColumn<SharedNode, string>("Name", x => x.Name), x => x.Children));

            source.MoveRows(source, new[] { new IndexPath(0, 0), new IndexPath(1, 1) },
                new IndexPath(2), RowDropPosition.Inside, RowMoveEffects.Move);

            Assert.Equal(new[] { third }, sharedChildren);
            Assert.Equal(new[] { first, second }, target.Children);
        }

        [Fact]
        public void Hierarchical_move_rejects_duplicate_physical_source_offsets()
        {
            var shared = new SharedNode("Shared");
            var sharedChildren = new ObservableCollection<SharedNode> { shared };
            var firstParent = new SharedNode("First", sharedChildren);
            var secondParent = new SharedNode("Second", sharedChildren);
            var target = new SharedNode("Target");
            var items = new ObservableCollection<SharedNode> { firstParent, secondParent, target };
            using var source = new HierarchicalTreeDataGridSource<SharedNode>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<SharedNode>(
                new TextColumn<SharedNode, string>("Name", x => x.Name), x => x.Children));

            Assert.Throws<ArgumentException>(() => source.MoveRows(source,
                new[] { new IndexPath(0, 0), new IndexPath(1, 0) }, new IndexPath(2),
                RowDropPosition.Inside, RowMoveEffects.Move));

            Assert.Same(shared, Assert.Single(sharedChildren));
            Assert.Empty(target.Children);
        }

        [Fact]
        public void Hierarchical_move_rejects_an_aliased_self_drop()
        {
            var shared = new SharedNode("Shared");
            var sharedChildren = new ObservableCollection<SharedNode> { shared };
            var firstParent = new SharedNode("First", sharedChildren);
            var secondParent = new SharedNode("Second", sharedChildren);
            var items = new ObservableCollection<SharedNode> { firstParent, secondParent };
            using var source = new HierarchicalTreeDataGridSource<SharedNode>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<SharedNode>(
                new TextColumn<SharedNode, string>("Name", x => x.Name), x => x.Children));

            Assert.Throws<InvalidOperationException>(() => source.MoveRows(source,
                new[] { new IndexPath(0, 0) }, new IndexPath(1, 0),
                RowDropPosition.Inside, RowMoveEffects.Move));

            Assert.Same(shared, Assert.Single(sharedChildren));
            Assert.Empty(shared.Children);
        }

        [Fact]
        public void Hierarchical_move_rejects_a_target_collection_aliased_into_the_moved_subtree()
        {
            var sharedChildren = new ObservableCollection<SharedNode>();
            var first = new SharedNode("First", sharedChildren);
            var second = new SharedNode("Second", sharedChildren);
            var items = new ObservableCollection<SharedNode> { first, second };
            using var source = new HierarchicalTreeDataGridSource<SharedNode>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<SharedNode>(
                new TextColumn<SharedNode, string>("Name", x => x.Name), x => x.Children));

            Assert.Throws<InvalidOperationException>(() => source.MoveRows(source,
                new[] { new IndexPath(0) }, new IndexPath(1),
                RowDropPosition.Inside, RowMoveEffects.Move));

            Assert.Equal(new[] { first, second }, items);
            Assert.Empty(sharedChildren);
        }

        [Fact]
        public void Hierarchical_move_does_not_scan_subtrees_for_a_same_collection_reorder()
        {
            var first = new Node("First") { Children = { new("Child") } };
            var second = new Node("Second");
            var items = new ObservableCollection<Node> { first, second };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            var childSelectorCalls = 0;
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name),
                x =>
                {
                    ++childSelectorCalls;
                    return x.Children;
                }));

            source.MoveRows(source, new[] { new IndexPath(0) }, new IndexPath(1),
                RowDropPosition.After, RowMoveEffects.Move);

            Assert.Equal(0, childSelectorCalls);
            Assert.Equal(new[] { second, first }, items);
        }

        [Fact]
        public void Hierarchical_move_refreshes_materialized_rows_for_a_plain_list()
        {
            var first = new Node("First");
            var second = new Node("Second");
            var items = new List<Node> { first, second };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
            Assert.Equal(new[] { first, second }, source.Rows.Select(x => x.Model));

            source.MoveRows(source, new[] { new IndexPath(0) }, new IndexPath(1),
                RowDropPosition.After, RowMoveEffects.Move);

            Assert.Equal(new[] { second, first }, items);
            Assert.Equal(new[] { second, first }, source.Rows.Select(x => x.Model));
        }

        [Fact]
        public void Hierarchical_move_refreshes_an_expanded_plain_child_list()
        {
            var moved = new SharedNode("Moved");
            var existing = new SharedNode("Existing");
            var children = new List<SharedNode> { existing };
            var target = new SharedNode("Target", children);
            var items = new ObservableCollection<SharedNode> { moved, target };
            using var source = new HierarchicalTreeDataGridSource<SharedNode>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<SharedNode>(
                new TextColumn<SharedNode, string>("Name", x => x.Name), x => x.Children));
            source.Expand(new IndexPath(1));
            Assert.Equal(new[] { moved, target, existing }, source.Rows.Select(x => x.Model));

            source.MoveRows(source, new[] { new IndexPath(0) }, new IndexPath(1),
                RowDropPosition.Inside, RowMoveEffects.Move);

            Assert.Equal(new[] { existing, moved }, children);
            Assert.Equal(new[] { target, existing, moved }, source.Rows.Select(x => x.Model));
        }

        [Fact]
        public void Hierarchical_move_preserves_source_owned_expansion()
        {
            var expanded = new Node("Expanded") { Children = { new("Child") } };
            var sibling = new Node("Sibling");
            var items = new ObservableCollection<Node> { expanded, sibling };
            using var source = new HierarchicalTreeDataGridSource<Node>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
            source.Expand(new IndexPath(0));
            Assert.Equal(3, source.Rows.Count);

            source.MoveRows(source, new[] { new IndexPath(0) }, new IndexPath(1),
                RowDropPosition.After, RowMoveEffects.Move);

            Assert.Equal(new[] { sibling, expanded, expanded.Children[0] },
                source.Rows.Select(x => x.Model));
            Assert.True(((IExpanderRow<Node>)source.Rows[1]).IsExpanded);
        }

        [Fact]
        public void Hierarchical_move_restores_a_selection_reached_through_an_alias()
        {
            var moved = new SharedNode("Moved");
            var remaining = new SharedNode("Remaining");
            var sharedChildren = new ObservableCollection<SharedNode> { moved, remaining };
            var firstParent = new SharedNode("First", sharedChildren);
            var secondParent = new SharedNode("Second", sharedChildren);
            var target = new SharedNode("Target");
            var items = new ObservableCollection<SharedNode> { firstParent, secondParent, target };
            using var source = new HierarchicalTreeDataGridSource<SharedNode>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<SharedNode>(
                new TextColumn<SharedNode, string>("Name", x => x.Name), x => x.Children));
            source.RowSelection!.SelectedIndex = new IndexPath(1, 0);

            source.MoveRows(source, new[] { new IndexPath(0, 0) }, new IndexPath(2),
                RowDropPosition.Inside, RowMoveEffects.Move);

            Assert.Same(remaining, Assert.Single(sharedChildren));
            Assert.Same(moved, Assert.Single(target.Children));
            Assert.Equal(new IndexPath(2, 0), source.RowSelection.SelectedIndex);
        }

        [Fact]
        public void Hierarchical_move_remaps_retained_selection_through_an_aliased_target()
        {
            var moved = new SharedNode("Moved");
            var first = new SharedNode("A");
            var selected = new SharedNode("B");
            var sharedChildren = new ObservableCollection<SharedNode> { first, selected };
            var firstParent = new SharedNode("First", sharedChildren);
            var secondParent = new SharedNode("Second", sharedChildren);
            var items = new ObservableCollection<SharedNode> { moved, firstParent, secondParent };
            using var source = new HierarchicalTreeDataGridSource<SharedNode>(items);
            source.Columns.Add(new HierarchicalExpanderColumn<SharedNode>(
                new TextColumn<SharedNode, string>("Name", x => x.Name), x => x.Children));
            source.RowSelection!.SelectedIndex = new IndexPath(1, 1);

            source.MoveRows(source, new[] { new IndexPath(0) }, new IndexPath(2, 1),
                RowDropPosition.Before, RowMoveEffects.Move);

            Assert.Equal(new[] { first, moved, selected }, sharedChildren);
            Assert.Equal(new IndexPath(0, 2), source.RowSelection.SelectedIndex);
        }

        [Fact]
        public void Hierarchical_move_rejects_a_read_only_target_before_mutation()
        {
            var moved = new SharedNode("Moved");
            var readOnlyChildren = new System.Collections.ObjectModel.ReadOnlyCollection<SharedNode>(
                new List<SharedNode>());
            var target = new SharedNode("Target", readOnlyChildren);
            var items = new ObservableCollection<SharedNode> { moved, target };
            using var source = new HierarchicalTreeDataGridSource<SharedNode>(items);

            Assert.Throws<InvalidOperationException>(() => source.MoveRows(
                source, new[] { new IndexPath(0) }, new IndexPath(1),
                RowDropPosition.Inside, RowMoveEffects.Move));

            Assert.Equal(new[] { moved, target }, items);
            Assert.Empty(readOnlyChildren);
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

        private sealed class SharedNode
        {
            public SharedNode(string name, IList<SharedNode>? children = null)
            {
                Name = name;
                Children = children ?? new ObservableCollection<SharedNode>();
            }

            public string Name { get; }
            public IList<SharedNode> Children { get; }
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
