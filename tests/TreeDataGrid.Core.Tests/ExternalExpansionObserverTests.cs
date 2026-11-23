using System;
using System.Collections.ObjectModel;
using System.Linq;
using TreeDataGridCore.Models;
using Xunit;

namespace TreeDataGridCore.Tests;

public class ExternalExpansionObserverTests
{
    [Fact]
    public void Binding_observer_updates_expansion_without_root_INPC_or_a_view()
    {
        var root = new Node();
        root.Children.Add(new());
        using var source = Create(root);
        Assert.Single(source.Rows);
        Assert.Equal(1, root.ExpansionObservers);
        root.SetExpanded(true);
        Assert.Equal(2, source.Rows.Count);
        root.SetExpanded(false);
        Assert.Single(source.Rows);
        source.Dispose();
        Assert.Equal(0, root.ExpansionObservers);
        Assert.Equal(0, root.ChildrenObservers);
    }

    [Fact]
    public void Child_reference_observation_is_lazy_and_replaces_expanded_children()
    {
        var root = new Node();
        root.Children.Add(new());
        using var source = Create(root);
        Assert.Single(source.Rows);
        Assert.Equal(0, root.ChildrenReads);
        Assert.Equal(0, root.ChildrenObservers);
        source.Expand(0);
        Assert.Equal(1, root.ChildrenObservers);
        root.ReplaceChildren(new() { new(), new() });
        Assert.Equal(3, source.Rows.Count);
        source.Collapse(0);
        Assert.Equal(0, root.ChildrenObservers);
        var reads = root.ChildrenReads;
        root.ReplaceChildren(new() { new() });
        Assert.Equal(reads, root.ChildrenReads);
        source.Expand(0);
        Assert.Equal(2, source.Rows.Count);
    }

    [Fact]
    public void Removing_an_expanded_row_disposes_both_external_subscriptions()
    {
        var root = new Node();
        root.Children.Add(new());
        var items = new ObservableCollection<Node> { root };
        using var source = Create(root, items);
        source.Expand(0);
        Assert.Equal(1, root.ExpansionObservers);
        Assert.Equal(1, root.ChildrenObservers);
        items.Clear();
        Assert.Equal(0, root.ExpansionObservers);
        Assert.Equal(0, root.ChildrenObservers);
        Assert.Empty(source.Rows);
    }

    [Fact]
    public void Removing_an_expanded_subtree_releases_descendants_and_preserves_siblings()
    {
        var before = new Node();
        var root = new Node();
        var child = new Node();
        var grandchild = new Node();
        var after = new Node();
        root.Children.Add(child);
        child.Children.Add(grandchild);
        var items = new ObservableCollection<Node> { before, root, after };
        using var source = Create(root, items);
        source.Expand(1);
        source.Expand(new IndexPath(1).Append(0));
        Assert.Equal(new[] { before, root, child, grandchild, after }, source.Rows.Select(row => row.Model));

        items.RemoveAt(1);

        Assert.Equal(new[] { before, after }, source.Rows.Select(row => row.Model));
        AssertRetired(root);
        AssertRetired(child);
        AssertRetired(grandchild);
        var reads = root.ChildrenReads;
        root.SetExpanded(false);
        root.SetExpanded(true);
        root.ReplaceChildren(new() { new() });
        Assert.Equal(reads, root.ChildrenReads);
        Assert.Equal(new[] { before, after }, source.Rows.Select(row => row.Model));
    }

    [Fact]
    public void Replacing_an_expanded_root_removes_old_descendants_and_disposal_is_idempotent()
    {
        var root = new Node();
        var child = new Node();
        root.Children.Add(child);
        var items = new ObservableCollection<Node> { root };
        using var source = Create(root, items);
        source.Expand(0);
        var retiredRow = Assert.IsType<HierarchicalRow<Node>>(source.Rows[0]);
        var replacement = new Node();

        items[0] = replacement;
        retiredRow.Dispose();
        retiredRow.Dispose();

        Assert.Same(replacement, Assert.Single(source.Rows).Model);
        AssertRetired(root);
        AssertRetired(child);
        Assert.Equal(1, replacement.ExpansionObservers);
    }

    [Fact]
    public void Replacing_child_reference_releases_the_old_subtree_but_keeps_owner_observation()
    {
        var root = new Node();
        var child = new Node();
        var grandchild = new Node();
        root.Children.Add(child);
        child.Children.Add(grandchild);
        using var source = Create(root);
        source.Expand(0);
        source.Expand(new IndexPath(0).Append(0));
        var replacement = new Node();

        root.ReplaceChildren(new() { replacement });

        Assert.Equal(new[] { root, replacement }, source.Rows.Select(row => row.Model));
        AssertRetired(child);
        AssertRetired(grandchild);
        Assert.Equal(1, root.ExpansionObservers);
        Assert.Equal(1, root.ChildrenObservers);
        Assert.Equal(1, replacement.ExpansionObservers);
    }

    [Fact]
    public void Disposed_rows_ignore_callbacks_raised_during_external_unsubscribe()
    {
        var root = new Node { NotifyOnUnsubscribe = true };
        root.Children.Add(new());
        using var source = Create(root);
        source.Expand(0);
        var reads = root.ChildrenReads;

        source.Dispose();
        source.Dispose();

        AssertRetired(root);
        Assert.Equal(reads, root.ChildrenReads);
    }

    private static void AssertRetired(Node node)
    {
        Assert.Equal(0, node.ExpansionObservers);
        Assert.Equal(0, node.ChildrenObservers);
    }

    private static HierarchicalTreeDataGridSource<Node> Create(Node root, ObservableCollection<Node>? items = null)
    {
        var source = new HierarchicalTreeDataGridSource<Node>(items ?? new() { root });
        source.Columns.Add(new ObserverColumn());
        return source;
    }
    private sealed class ObserverColumn : HierarchicalExpanderColumn<Node>, IModelExpansionObserver<Node>, IModelChildrenObserver<Node>
    {
        internal ObserverColumn() : base(new TextColumn<Node, string>("Name", _ => "row"),
            node => { ++node.ChildrenReads; return node.Children; }) { }
        public override bool HasChildren(Node model) => true;
        public override bool? GetModelIsExpanded(Node model) => model.Expanded;
        public override void SetModelIsExpanded(IExpanderRow<Node> row) => row.Model.SetExpanded(row.IsExpanded);
        public IDisposable? SubscribeToExpansion(Node model, Action changed)
        {
            ++model.ExpansionObservers;
            model.ExpansionChanged += changed;
            return new Subscription(() =>
            {
                --model.ExpansionObservers;
                model.ExpansionChanged -= changed;
                if (model.NotifyOnUnsubscribe) changed();
            });
        }
        public IDisposable? SubscribeToChildren(Node model, Action changed)
        {
            ++model.ChildrenObservers;
            model.ChildrenChanged += changed;
            return new Subscription(() =>
            {
                --model.ChildrenObservers;
                model.ChildrenChanged -= changed;
                if (model.NotifyOnUnsubscribe) changed();
            });
        }
    }
    private sealed class Node
    {
        internal ObservableCollection<Node> Children { get; private set; } = new();
        internal bool Expanded;
        internal bool NotifyOnUnsubscribe;
        internal int ChildrenReads, ExpansionObservers, ChildrenObservers;
        internal event Action? ExpansionChanged;
        internal event Action? ChildrenChanged;
        internal void SetExpanded(bool value) { if (Expanded == value) return; Expanded = value; ExpansionChanged?.Invoke(); }
        internal void ReplaceChildren(ObservableCollection<Node> children) { Children = children; ChildrenChanged?.Invoke(); }
    }
    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() { var action = _dispose; _dispose = null; action?.Invoke(); }
    }
}
