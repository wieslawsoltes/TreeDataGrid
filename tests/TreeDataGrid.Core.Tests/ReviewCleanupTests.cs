using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore.Models;
using Xunit;

namespace TreeDataGridCore.Tests;

public sealed class ReviewCleanupTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    [InlineData(12)] [InlineData(13)] [InlineData(14)] [InlineData(15)]
    public void Row_disposal_attempts_all_independent_owners_in_order(int mask)
    {
        var first = new Node(); var second = new Node();
        var root = new Node { Expanded = true, Children = new() { first, second } };
        var row = new HierarchicalRow<Node>(new Controller(), new Column(), new IndexPath(0), root, null);
        Assert.Equal(2, row.Children!.ToArray().Length);
        var expected = new List<Exception>();
        if ((mask & 1) != 0) expected.Add(root.ModelFailure = new InvalidOperationException("model remove"));
        if ((mask & 2) != 0) expected.Add(root.ExpansionFailure = new InvalidOperationException("expansion remove"));
        if ((mask & 4) != 0) expected.Add(root.ChildrenFailure = new InvalidOperationException("children remove"));
        if ((mask & 8) != 0) expected.Add(first.ExpansionFailure = new InvalidOperationException("descendant remove"));
        root.OnModelRemove = row.Dispose;
        var error = Record.Exception(row.Dispose);
        if (expected.Count == 0) Assert.Null(error);
        else if (expected.Count == 1) Assert.Same(expected[0], error);
        else Assert.Equal(expected, Assert.IsType<AggregateException>(error).InnerExceptions);
        Assert.Equal(0, root.ModelObservers + root.ExpansionObservers + root.ChildrenObservers);
        Assert.Equal(0, first.ExpansionObservers + second.ExpansionObservers);
        Assert.Equal(1, root.ModelRemovals);
        Assert.Equal(1, first.ExpansionRemovals); Assert.Equal(1, second.ExpansionRemovals);
        row.Dispose();
        Assert.Equal(1, root.ModelRemovals);
        Assert.Equal(2, root.Children.Count); // Caller owns the model collection.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Collapse_cleanup_preserves_observation_created_by_reentrant_expansion(bool childrenLease)
    {
        var root = new Node { Expanded = true, Children = new() { new() } };
        using var row = new HierarchicalRow<Node>(new Controller(), new Column(), new IndexPath(0), root, null);
        if (childrenLease) root.OnChildrenRemove = () => row.IsExpanded = true;
        else root.OnModelRemove = () => row.IsExpanded = true;
        row.IsExpanded = false;
        Assert.True(row.IsExpanded);
        Assert.Equal(1, root.ModelObservers);
        Assert.Equal(1, root.ChildrenObservers);
        row.Dispose();
        Assert.Equal(0, root.ModelObservers + root.ExpansionObservers + root.ChildrenObservers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sorted_and_unsorted_resets_retire_siblings_even_when_cleanup_and_notification_fail(bool sorted)
    {
        var a = new Node(); var b = new Node();
        using var view = new TreeDataGridItemsSourceView<Node>(new[] { a, b });
        using var rows = new Rows(view, sorted ? static (_, _) => 0 : null);
        var created = rows.ToArray();
        var first = new InvalidOperationException("first"); var second = new InvalidOperationException("second");
        var notify = new InvalidOperationException("notification");
        created[0].Failure = first; created[1].Failure = second;
        var notifications = 0;
        NotifyCollectionChangedEventHandler handler = (_, e) =>
        { ++notifications; Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action); Assert.Equal(0, rows.Count); throw notify; };
        rows.CollectionChanged += handler;
        var failure = Assert.Throws<AggregateException>(rows.Dispose);
        Assert.Equal(new Exception[] { first, second, notify }, failure.InnerExceptions);
        Assert.All(created, item => Assert.Equal(1, item.Disposals));
        Assert.Equal(1, notifications);
        rows.CollectionChanged -= handler;
        rows.Dispose();
        Assert.All(created, item => Assert.Equal(1, item.Disposals));
        Assert.Equal(2, view.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reentrant_reset_keeps_newly_materialized_rows_and_releases_old_rows_once(bool sorted)
    {
        using var original = new TreeDataGridItemsSourceView<Node>(new[] { new Node(), new Node() });
        using var replacement = new TreeDataGridItemsSourceView<Node>(new[] { new Node() });
        using var rows = new Rows(original, sorted ? static (_, _) => 0 : null);
        var old = rows.ToArray();
        ProbeRow? current = null;
        old[0].OnDispose = () => { rows.SetItems(replacement); current = rows[0]; };
        rows.Dispose();
        Assert.All(old, item => Assert.Equal(1, item.Disposals));
        Assert.NotNull(current); Assert.Same(current, Assert.Single(rows));
        Assert.Equal(0, current!.Disposals);
        rows.Dispose(); Assert.Equal(1, current!.Disposals);
    }

    private sealed class Rows(TreeDataGridItemsSourceView<Node> items, Comparison<Node>? comparison)
        : SortableRowsBase<Node, ProbeRow>(items, comparison)
    {
        protected override ProbeRow CreateRow(int modelIndex, Node model) => new(modelIndex, model);
    }
    private sealed class ProbeRow(int modelIndex, Node model) : IRow<Node>, IModelIndexableRow, IDisposable
    {
        public int ModelIndex { get; private set; } = modelIndex;
        public IndexPath ModelIndexPath => new(ModelIndex);
        public Node Model => model;
        object? IRow.Model => Model;
        public object? Header => null;
        public GridLength Height { get; set; } = GridLength.Auto;
        public void UpdateModelIndex(int delta) => ModelIndex += delta;
        internal int Disposals;
        internal Exception? Failure;
        internal Action? OnDispose;
        public void Dispose()
        {
            ++Disposals;
            var callback = OnDispose; OnDispose = null;
            callback?.Invoke();
            if (Failure is { } error) throw error;
        }
    }
    private sealed class Controller : IExpanderRowController<Node>
    {
        public void OnBeginExpandCollapse(IExpanderRow<Node> row) { }
        public void OnEndExpandCollapse(IExpanderRow<Node> row) { }
        public void OnChildCollectionChanged(IExpanderRow<Node> row, NotifyCollectionChangedEventArgs e) { }
    }
    private sealed class Column : HierarchicalExpanderColumn<Node>, IModelExpansionObserver<Node>, IModelChildrenObserver<Node>
    {
        public Column() : base(new TextColumn<Node, string>("Name", _ => "Row"), x => x.Children) { }
        public override bool HasChildren(Node model) => model.Children.Count > 0;
        public override bool? GetModelIsExpanded(Node model) => model.Expanded;
        public override void SetModelIsExpanded(IExpanderRow<Node> row) => row.Model.Expanded = row.IsExpanded;
        public IDisposable? SubscribeToExpansion(Node model, Action changed)
        {
            ++model.ExpansionObservers;
            return new Lease(() =>
            { --model.ExpansionObservers; ++model.ExpansionRemovals; if (model.ExpansionFailure is { } error) throw error; });
        }
        public IDisposable? SubscribeToChildren(Node model, Action changed)
        {
            ++model.ChildrenObservers;
            return new Lease(() =>
            {
                --model.ChildrenObservers;
                var callback = model.OnChildrenRemove; model.OnChildrenRemove = null; callback?.Invoke();
                if (model.ChildrenFailure is { } error) throw error;
            });
        }
    }
    private sealed class Node : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        internal bool Expanded;
        internal ObservableCollection<Node> Children = new();
        internal int ModelObservers, ModelRemovals, ExpansionObservers, ExpansionRemovals, ChildrenObservers;
        internal Exception? ModelFailure, ExpansionFailure, ChildrenFailure;
        internal Action? OnModelRemove, OnChildrenRemove;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { ++ModelObservers; _changed += value; }
            remove
            {
                --ModelObservers; ++ModelRemovals; _changed -= value;
                var callback = OnModelRemove; OnModelRemove = null; callback?.Invoke();
                if (ModelFailure is { } error) throw error;
            }
        }
    }
    private sealed class Lease(Action action) : IDisposable
    {
        private Action? _action = action;
        public void Dispose() { var callback = _action; _action = null; callback?.Invoke(); }
    }
}
