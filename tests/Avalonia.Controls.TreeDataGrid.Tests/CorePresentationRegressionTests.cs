using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Presentation;
using Avalonia.Headless.XUnit;
using Core = global::TreeDataGridCore;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests;
public class CorePresentationRegressionTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nested_expansion_binding_observes_nested_property_changes(bool native)
    {
        var item = new Node(); item.Children.Add(new Node());
        if (native)
        {
            using var source = new Core.HierarchicalTreeDataGridSource<Node>(new[] { item });
            source.Columns.Add(new Core.Models.HierarchicalExpanderColumn<Node>(
                new Core.Models.TextColumn<Node, string>("Name", x => x.Name),
                x => x.Children, isExpandedSelector: x => x.State.IsExpanded));
            using var view = new TreeDataGridPresentation<Node>(source);
            var cell = view.Rows.RealizeCell(view.Columns[0], 0, 0);
            try { item.State.IsExpanded = true; Assert.Equal(2, source.Rows.Count); }
            finally { view.Rows.UnrealizeCell(cell, 0, 0); }
        }
        else
        {
            using var source = new HierarchicalTreeDataGridSource<Node>(new[] { item });
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name),
                x => x.Children, isExpandedSelector: x => x.State.IsExpanded));
            var cell = source.Rows.RealizeCell(source.Columns[0], 0, 0);
            try { item.State.IsExpanded = true; Assert.Equal(2, source.Rows.Count); }
            finally { source.Rows.UnrealizeCell(cell, 0, 0); }
        }
    }

    [AvaloniaTheory]
    [InlineData("dispose")]
    [InlineData("hide")]
    [InlineData("replace")]
    public void Hierarchical_presentation_disposes_owned_custom_column(string action)
    {
        using var source = new Core.HierarchicalTreeDataGridSource<Node>(new[] { new Node() });
        source.Columns.Add(new Core.Models.HierarchicalExpanderColumn<Node>(
            new Core.Models.TemplateColumn<Node>("Name", "custom"), x => x.Children));
        var cellColumn = new DisposableColumn();
        var options = new TreeDataGridPresentationOptions<Node>();
        options.Columns.Add("custom", _ => cellColumn);
        var view = new TreeDataGridPresentation<Node>(source, options);
        options.Columns.Add("replacement", _ => new TextColumn<Node, string>("Name", x => x.Name));
        if (action == "hide")
        {
            source.Columns[0].IsVisible = false;
            Assert.Equal(0, cellColumn.DisposeCount);
            view.Dispose();
        }
        else if (action == "replace") source.Columns[0].PresentationKey = "replacement";
        else view.Dispose();
        Assert.Equal(1, cellColumn.DisposeCount);
        view.Dispose();
        Assert.Equal(1, cellColumn.DisposeCount);
    }

    [AvaloniaFact]
    public void Failed_presentation_key_change_keeps_the_previous_column_recoverable()
    {
        using var source = new Core.FlatTreeDataGridSource<Node>(new[] { new Node() });
        var model = new Core.Models.TemplateColumn<Node>("Name", "custom");
        source.Columns.Add(model);
        var previous = new DisposableColumn();
        var options = new TreeDataGridPresentationOptions<Node>();
        options.Columns.Add("custom", _ => previous);
        options.Columns.Add("replacement", _ => new TextColumn<Node, string>("Name", x => x.Name));
        using var view = new TreeDataGridPresentation<Node>(source, options);
        var previousView = view.Columns[0];

        Assert.Throws<InvalidOperationException>(() => model.PresentationKey = "missing");
        Assert.Same(previousView, view.Columns[0]);
        Assert.Equal(0, previous.DisposeCount);

        model.PresentationKey = "custom";
        Assert.Same(previousView, view.Columns[0]);
        model.PresentationKey = "replacement";
        Assert.NotSame(previousView, view.Columns[0]);
        Assert.Equal(1, previous.DisposeCount);
    }

    [AvaloniaFact]
    public void Duplicate_core_column_is_rejected_before_the_presentation_changes()
    {
        using var source = new Core.FlatTreeDataGridSource<Node>(new[] { new Node() });
        var model = new Core.Models.TextColumn<Node, string>("Name", x => x.Name);
        source.Columns.Add(model);
        using var view = new TreeDataGridPresentation<Node>(source);
        var cell = view.Columns[0];

        Assert.Throws<InvalidOperationException>(() => source.Columns.Add(model));

        Assert.Same(model, Assert.Single(source.Columns));
        Assert.Same(cell, Assert.Single(view.Columns));
    }

    [AvaloniaFact]
    public void Distinct_value_equal_core_columns_have_distinct_presentations()
    {
        using var source = new Core.FlatTreeDataGridSource<Node>(new[] { new Node() });
        var first = new ValueEqualColumn("First");
        var second = new ValueEqualColumn("Second");
        source.Columns.Add(first);
        using var view = new TreeDataGridPresentation<Node>(source);

        source.Columns.Add(second);

        Assert.Equal(2, view.Columns.Count);
        source.Columns.Remove(first);
        Assert.Same(second, Assert.Single(source.Columns));
        Assert.Equal("Second", Assert.Single(view.Columns).Header);
    }

    [AvaloniaFact]
    public void Show_expander_observable_detaches_from_children_when_unsubscribed()
    {
        var children = new TrackingCollection<Node>();
        var observable = new ShowExpanderObservable<Node>(_ => children, null, new Node());
        var subscription = observable.Subscribe(new BooleanObserver());

        Assert.Equal(1, children.SubscriberCount);

        subscription.Dispose();

        Assert.Equal(0, children.SubscriberCount);
    }

    [AvaloniaFact]
    public void Show_expander_observable_rebinds_when_the_child_collection_is_replaced()
    {
        var oldChildren = new TrackingCollection<ReplaceableNode>();
        var newChildren = new TrackingCollection<ReplaceableNode> { new() };
        var model = new ReplaceableNode { Children = oldChildren };
        var observable = new ShowExpanderObservable<ReplaceableNode>(
            x => x.Children, null, model);
        var observer = new BooleanObserver();
        var subscription = observable.Subscribe(observer);

        Assert.False(observer.Value);
        Assert.Equal(1, oldChildren.SubscriberCount);

        model.Children = newChildren;

        Assert.True(observer.Value);
        Assert.Equal(0, oldChildren.SubscriberCount);
        Assert.Equal(1, newChildren.SubscriberCount);

        subscription.Dispose();

        Assert.Equal(0, newChildren.SubscriberCount);
    }

    [AvaloniaFact]
    public void Unrealized_core_expander_recomputes_show_expander_without_a_cached_override()
    {
        var item = new Node();
        item.Children.Add(new Node());
        using var source = new Core.HierarchicalTreeDataGridSource<Node>(new[] { item });
        source.Columns.Add(new Core.Models.HierarchicalExpanderColumn<Node>(
            new Core.Models.TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
        using var view = new TreeDataGridPresentation<Node>(source);
        var cell = view.Rows.RealizeCell(view.Columns[0], 0, 0);
        Assert.True(((Core.Models.IExpanderRow<Node>)source.Rows[0]).ShowExpander);

        view.Rows.UnrealizeCell(cell, 0, 0);
        item.Children.Clear();

        Assert.False(((Core.Models.IExpanderRow<Node>)source.Rows[0]).ShowExpander);
    }

    public sealed class DisposableColumn : TextColumn<Node, string>, IDisposable
    {
        public DisposableColumn() : base("Name", x => x.Name) { }
        public int DisposeCount { get; private set; }
        public void Dispose() => ++DisposeCount;
    }
    private sealed class ValueEqualColumn : Core.Models.TextColumn<Node, string>
    {
        public ValueEqualColumn(string header) : base(header, x => x.Name) { }
        public override bool Equals(object? obj) => obj is ValueEqualColumn;
        public override int GetHashCode() => 0;
    }
    public sealed class Node
    {
        public string Name => "Node";
        public State State { get; } = new();
        public ObservableCollection<Node> Children { get; } = new();
    }
    public sealed class State : INotifyPropertyChanged
    {
        private bool _expanded;
        public bool IsExpanded { get => _expanded; set { _expanded = value; PropertyChanged?.Invoke(this, new(nameof(IsExpanded))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class ReplaceableNode : INotifyPropertyChanged
    {
        private TrackingCollection<ReplaceableNode> _children = new();
        public TrackingCollection<ReplaceableNode> Children
        {
            get => _children;
            set
            {
                _children = value;
                PropertyChanged?.Invoke(this, new(nameof(Children)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class BooleanObserver : IObserver<bool>
    {
        public bool Value { get; private set; }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(bool value) => Value = value;
    }
    private sealed class TrackingCollection<T> : Collection<T>, INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _collectionChanged;
        public int SubscriberCount { get; private set; }
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { ++SubscriberCount; _collectionChanged += value; }
            remove { --SubscriberCount; _collectionChanged -= value; }
        }
    }
}
