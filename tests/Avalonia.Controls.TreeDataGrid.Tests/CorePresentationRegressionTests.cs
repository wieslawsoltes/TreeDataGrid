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
    private sealed class BooleanObserver : IObserver<bool>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(bool value) { }
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
