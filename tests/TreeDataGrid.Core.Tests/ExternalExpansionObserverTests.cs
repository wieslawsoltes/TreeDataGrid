using System;
using System.Collections.ObjectModel;
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
            return new Subscription(() => { --model.ExpansionObservers; model.ExpansionChanged -= changed; });
        }
        public IDisposable? SubscribeToChildren(Node model, Action changed)
        {
            ++model.ChildrenObservers;
            model.ChildrenChanged += changed;
            return new Subscription(() => { --model.ChildrenObservers; model.ChildrenChanged -= changed; });
        }
    }
    private sealed class Node
    {
        internal ObservableCollection<Node> Children { get; private set; } = new();
        internal bool Expanded;
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
