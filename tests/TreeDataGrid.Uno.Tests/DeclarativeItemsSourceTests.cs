using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class DeclarativeItemsSourceTests
{
    [Fact]
    public void Every_mutation_preserves_projection_sender_and_original_event_payload()
    {
        var items = new ObservableCollection<object>();
        var projection = new DeclarativeItemsSource(items);
        var underlying = new List<NotifyCollectionChangedEventArgs>();
        var forwarded = new List<NotifyCollectionChangedEventArgs>();
        items.CollectionChanged += (_, args) => underlying.Add(args);
        projection.CollectionChanged += (sender, args) =>
        {
            Assert.Same(projection, sender);
            Assert.Equal(items.Count, projection.Count);
            forwarded.Add(args);
        };
        var first = new object();
        var second = new object();
        items.Add(first);
        projection.Add(second);
        items.Move(1, 0);
        projection[0] = first;
        projection.RemoveAt(0);
        projection.Clear();
        Assert.Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add,
            NotifyCollectionChangedAction.Move, NotifyCollectionChangedAction.Replace,
            NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Reset },
            forwarded.Select(args => args.Action));
        for (var i = 0; i < underlying.Count; ++i) Assert.Same(underlying[i], forwarded[i]);
    }

    [Fact]
    public void Core_weak_event_routing_receives_projected_collection_mutations()
    {
        var items = new ObservableCollection<object>();
        var projection = new DeclarativeItemsSource(items);
        using var view = new TreeDataGridItemsSourceView<object>(projection);
        var changes = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (sender, args) => { Assert.Same(view, sender); changes.Add(args); };
        var item = new object();
        items.Add(item);
        Assert.Same(item, view[0]);
        Assert.Equal(NotifyCollectionChangedAction.Add, Assert.Single(changes).Action);
        changes.Clear();
        items.Clear();
        Assert.Equal(0, view.Count);
        Assert.Equal(NotifyCollectionChangedAction.Reset, Assert.Single(changes).Action);
    }

    [Fact]
    public void Empty_materialized_hierarchy_recovers_when_projected_children_are_added()
    {
        var root = new Node();
        using var source = new HierarchicalTreeDataGridSource<object>([root]);
        source.Columns.Add(new HierarchicalExpanderColumn<object>(
            new TextColumn<object, string>("Name", item => ((Node)item).Name),
            item => ((Node)item).ProjectedChildren));
        var row = (IExpander)source.Rows[0];
        row.IsExpanded = true;
        Assert.False(row.IsExpanded);
        Assert.False(row.ShowExpander);
        var child = new Node();
        root.Children.Add(child);
        Assert.True(row.ShowExpander);
        row.IsExpanded = true;
        Assert.True(row.IsExpanded);
        Assert.Equal(2, source.Rows.Count);
        Assert.Same(child, source.Rows[1].Model);
        root.Children.Clear();
        Assert.Equal(1, source.Rows.Count);
        var replacement = new Node();
        root.Children.Add(replacement);
        Assert.Equal(2, source.Rows.Count);
        Assert.Same(replacement, source.Rows[1].Model);
    }

    [Fact]
    public void Last_listener_removal_detaches_and_later_subscription_reattaches_once()
    {
        var items = new TrackingCollection();
        var projection = new DeclarativeItemsSource(items);
        var calls = 0;
        NotifyCollectionChangedEventHandler handler = (sender, _) =>
        { Assert.Same(projection, sender); ++calls; };
        Assert.Equal(0, items.Subscribers);
        projection.CollectionChanged += handler;
        projection.CollectionChanged += handler;
        Assert.Equal(1, items.Subscribers);
        items.Add(new object());
        Assert.Equal(2, calls);
        projection.CollectionChanged -= handler;
        Assert.Equal(1, items.Subscribers);
        projection.CollectionChanged -= handler;
        Assert.Equal(0, items.Subscribers);
        items.Add(new object());
        Assert.Equal(2, calls);
        projection.CollectionChanged += handler;
        Assert.Equal(1, items.Subscribers);
        items.Add(new object());
        Assert.Equal(3, calls);
        projection.CollectionChanged -= handler;
        Assert.Equal(0, items.Subscribers);
    }

    [Fact]
    public void A_listener_can_replace_its_subscription_inside_notification()
    {
        var items = new TrackingCollection();
        var projection = new DeclarativeItemsSource(items);
        var calls = new List<string>();
        NotifyCollectionChangedEventHandler next = (_, _) => calls.Add("next");
        NotifyCollectionChangedEventHandler first = null!;
        first = (_, _) =>
        {
            calls.Add("first");
            projection.CollectionChanged -= first;
            projection.CollectionChanged += next;
        };
        projection.CollectionChanged += first;
        items.Add(new object());
        items.Add(new object());
        Assert.Equal(new[] { "first", "next" }, calls);
        Assert.Equal(1, items.Subscribers);
        projection.CollectionChanged -= next;
        Assert.Equal(0, items.Subscribers);
    }

    private sealed class Node
    {
        public string Name => "Node";
        public ObservableCollection<object> Children { get; } = new();
        public DeclarativeItemsSource ProjectedChildren { get; }
        public Node() => ProjectedChildren = new(Children);
    }

    private sealed class TrackingCollection : ObservableCollection<object>
    {
        public int Subscribers { get; private set; }
        public override event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { if (value is not null) { ++Subscribers; base.CollectionChanged += value; } }
            remove { if (value is not null) { --Subscribers; base.CollectionChanged -= value; } }
        }
    }
}
