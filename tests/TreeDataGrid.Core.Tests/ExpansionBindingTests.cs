using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using TreeDataGridCore.Models;
using Xunit;

namespace TreeDataGridCore.Tests;
public class ExpansionBindingTests
{
    [Fact]
    public void Nested_expansion_updates_without_a_view_and_rebinds_replaced_owners()
    {
        var root = new Node { Children = { new() } };
        using var source = Create(root);
        Assert.Single(source.Rows);
        var original = root.State;
        original.IsExpanded = true;
        Assert.Equal(2, source.Rows.Count);
        root.State = new State();
        Assert.Single(source.Rows);
        Assert.Equal(0, original.SubscriberCount);
        original.IsExpanded = false;
        original.IsExpanded = true;
        Assert.Single(source.Rows);
        root.State.IsExpanded = true;
        Assert.Equal(2, source.Rows.Count);
        source.Collapse(0);
        Assert.False(root.State.IsExpanded);
        source.Expand(0);
        Assert.True(root.State.IsExpanded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Source_disposal_or_row_removal_releases_entire_property_path(bool remove)
    {
        var root = new Node { Children = { new() } };
        var items = new ObservableCollection<Node> { root };
        using var source = Create(root, items);
        Assert.Single(source.Rows);
        Assert.True(root.SubscriberCount > 0);
        Assert.True(root.State.SubscriberCount > 0);
        if (remove) items.Clear(); else source.Dispose();
        Assert.Equal(0, root.SubscriberCount);
        Assert.Equal(0, root.State.SubscriberCount);
    }

    [Fact]
    public void Null_safe_selector_reconnects_a_missing_intermediate_object()
    {
        var root = new Node { Children = { new() } };
        using var source = new HierarchicalTreeDataGridSource<Node>(new[] { root });
        source.Columns.Add(new HierarchicalExpanderColumn<Node>(
            new TextColumn<Node, string>("Name", x => "Node"), x => x.Children,
            isExpandedSelector: x => x.State != null && x.State.IsExpanded,
            setIsExpanded: (x, value) => { if (x.State != null) x.State.IsExpanded = value; }));
        Assert.Single(source.Rows);
        var original = root.State;
        root.State = null!;
        Assert.Equal(0, original.SubscriberCount);
        root.State = new State { IsExpanded = true };
        Assert.Equal(2, source.Rows.Count);
    }

    private static HierarchicalTreeDataGridSource<Node> Create(Node root, ObservableCollection<Node>? items = null)
    {
        var source = new HierarchicalTreeDataGridSource<Node>(items ?? new() { root });
        source.Columns.Add(new HierarchicalExpanderColumn<Node>(
            new TextColumn<Node, string>("Name", x => "Node"), x => x.Children,
            isExpandedSelector: x => x.State.IsExpanded));
        return source;
    }
    public abstract class Observable : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
        protected void Notify(string name) => _changed?.Invoke(this, new(name));
    }
    public sealed class Node : Observable
    {
        private State _state = new();
        public State State { get => _state; set { _state = value; Notify(nameof(State)); } }
        public ObservableCollection<Node> Children { get; } = new();
    }
    public sealed class State : Observable
    {
        private bool _expanded;
        public bool IsExpanded { get => _expanded; set { if (_expanded == value) return; _expanded = value; Notify(nameof(IsExpanded)); } }
    }
}
