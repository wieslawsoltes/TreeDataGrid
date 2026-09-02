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
    }
}
