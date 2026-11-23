using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Xunit;
using UI = global::Uno.Controls.Models.TreeDataGrid;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class PublicObservableExpanderTests
{
    [Fact]
    public void Public_contract_keeps_exact_content_and_Core_row_identity()
    {
        var row = new Row();
        var inner = new ContentCell();
        using var show = new Values(true);
        using var cell = new UI.ExpanderCell<object>(inner, row, show, null);
        Assert.Same(inner, cell.Content);
        Assert.Same(inner, ((UI.IExpanderCell)cell).Content);
        Assert.Same(row, cell.Row);
        Assert.Same(row, ((UI.IExpanderCell)cell).Row);
        Assert.Equal(inner.Value, cell.Value);
        Assert.Equal(inner.CanEdit, cell.CanEdit);
        Assert.Equal(inner.EditGestures, cell.EditGestures);
        Assert.True(cell.ShowExpander);
        cell.IsExpanded = true;
        Assert.True(row.IsExpanded);
    }

    [Fact]
    public void Observable_expansion_updates_the_actual_Core_source_without_copying_rows()
    {
        var model = new Node();
        model.Children.Add(new());
        using var source = new HierarchicalTreeDataGridSource<Node>([model]);
        source.Columns.Add(new HierarchicalExpanderColumn<Node>(
            new TextColumn<Node, string>("Name", _ => "row"), item => item.Children));
        var row = Assert.IsAssignableFrom<IExpanderRow<Node>>(source.Rows[0]);
        using var show = new Values(true);
        using var expanded = new Values(false);
        using var cell = new UI.ExpanderCell<Node>(new ContentCell(), row, show, expanded);
        Assert.Single(source.Rows);
        expanded.Publish(true);
        Assert.Equal(2, source.Rows.Count);
        Assert.Same(row, source.Rows[0]);
        cell.IsExpanded = false;
        Assert.Single(source.Rows);
        Assert.Same(model, cell.Row.Model);
    }

    [Fact]
    public void Row_notifications_preserve_names_and_ignore_unrelated_properties()
    {
        var row = new Row();
        using var show = new Values(true);
        using var expanded = new Values(false);
        using var cell = new UI.ExpanderCell<object>(new ContentCell(), row, show, expanded);
        var names = new List<string?>();
        cell.PropertyChanged += (_, args) => names.Add(args.PropertyName);
        expanded.Publish(true);
        show.Publish(false);
        row.Notify("Model");
        row.Notify(null);
        Assert.Equal(new[] { "IsExpanded", "ShowExpander", null }, names);
        Assert.True(cell.IsExpanded);
        Assert.False(cell.ShowExpander);
    }

    [Fact]
    public void Reentrant_disposal_rejects_captured_and_unsubscribe_callbacks()
    {
        var row = new Row();
        var inner = new ContentCell();
        using var show = new Values(true) { EmitOnDispose = false };
        using var expanded = new Values(false) { EmitOnDispose = false };
        var cell = new UI.ExpanderCell<object>(inner, row, show, expanded);
        cell.PropertyChanged += (_, _) => cell.Dispose();
        expanded.Publish(true);
        Assert.True(row.IsExpanded);
        Assert.True(row.ShowExpander);
        Assert.Equal(0, row.Subscribers);
        Assert.Equal(0, show.Subscribers);
        Assert.Equal(0, expanded.Subscribers);
        show.Captured!.OnNext(false);
        expanded.Captured!.OnNext(false);
        expanded.Captured!.OnError(new InvalidOperationException("retired"));
        Assert.True(row.IsExpanded);
        Assert.True(row.ShowExpander);
        Assert.Null(cell.Error);
        cell.Dispose();
        Assert.Equal(1, inner.Disposals);
    }

    [Fact]
    public void Failed_observable_construction_releases_prior_ownership()
    {
        var row = new Row();
        var inner = new ContentCell();
        using var show = new Values(true);
        var failure = new InvalidOperationException("Subscribe failed.");
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
            new UI.ExpanderCell<object>(inner, row, show, new FailingObservable(failure))));
        Assert.Equal(0, row.Subscribers);
        Assert.Equal(0, show.Subscribers);
        Assert.Equal(1, inner.Disposals);
    }

    [Fact]
    public void Disposal_aggregates_failures_and_releases_every_owned_resource()
    {
        var row = new Row();
        var inner = new ContentCell { Failure = new InvalidOperationException("inner") };
        using var show = new Values(true) { DisposeFailure = new InvalidOperationException("show") };
        using var expanded = new Values(false) { DisposeFailure = new InvalidOperationException("expanded") };
        var cell = new UI.ExpanderCell<object>(inner, row, show, expanded);
        var error = Assert.Throws<AggregateException>(cell.Dispose);
        Assert.Equal(new[] { "show", "expanded", "inner" }, error.InnerExceptions.Select(e => e.Message));
        Assert.Equal(0, row.Subscribers);
        Assert.Equal(0, show.Subscribers);
        Assert.Equal(0, expanded.Subscribers);
        Assert.Equal(1, inner.Disposals);
        cell.Dispose();
    }

    [Fact]
    public void Observable_errors_preserve_the_actual_exception_without_changing_expansion()
    {
        using var show = new Values(true);
        using var expanded = new Values(true);
        using var cell = new UI.ExpanderCell<object>(new ContentCell(), new Row(), show, expanded);
        var failure = new InvalidOperationException("observable error");
        expanded.Fail(failure);
        Assert.Same(failure, cell.Error);
        Assert.True(cell.IsExpanded);
        Assert.True(cell.ShowExpander);
    }

    [Fact]
    public void Native_custom_adapter_renders_public_expanders_and_owns_content_once()
    {
        var row = new Row();
        using var show = new Values(true);
        using var expanded = new Values(false);
        var inner = new ContentCell();
        var cell = new UI.ExpanderCell<object>(inner, row, show, expanded);
        using var adapter = Assert.IsAssignableFrom<ExpanderCellValue>(CellColumnAdapter<object>.Adapt(cell, true));
        Assert.Same(cell, adapter.PresentationModel);
        Assert.Same(inner, adapter.Content);
        Assert.Same(row, adapter.Row);
        Assert.True(adapter.ShowExpander);
        var count = 0;
        adapter.PropertyChanged += (_, args) => { if (args.PropertyName == "IsExpanded") ++count; };
        expanded.Publish(true);
        Assert.True(adapter.IsExpanded);
        Assert.Equal(1, count);
        var failure = new InvalidOperationException("observable expansion failed");
        expanded.Fail(failure);
        Assert.Same(failure, adapter.Error);
        adapter.Dispose();
        cell.Dispose();
        Assert.Equal(1, inner.Disposals);
        Assert.Equal(0, row.Subscribers);
    }

    private sealed class Node { public ObservableCollection<Node> Children { get; } = new(); }
    private sealed class Row : IExpanderRow<object>
    {
        private PropertyChangedEventHandler? _changed;
        private bool _expanded;
        private bool _show;
        public object Model { get; } = new();
        public object? Header => null;
        public void UpdateModelIndex(int delta) { }
        public GridLength Height { get; set; }
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public bool IsExpanded { get => _expanded; set { if (_expanded == value) return; _expanded = value; Notify(nameof(IsExpanded)); } }
        public bool ShowExpander => _show;
        public void UpdateShowExpander(bool value) { if (_show == value) return; _show = value; Notify(nameof(ShowExpander)); }
        public void Notify(string? name) => _changed?.Invoke(this, new(name));
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
    private sealed class ContentCell : UI.ICell, IDisposable
    {
        public bool CanEdit => true;
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.F2;
        public object? Value => "Content";
        public int Disposals;
        public Exception? Failure;
        public void Dispose() { ++Disposals; if (Failure is not null) throw Failure; }
    }
    private sealed class Values(bool initial) : IObservable<bool>, IDisposable
    {
        private readonly List<IObserver<bool>> _observers = new();
        public IObserver<bool>? Captured;
        public bool? EmitOnDispose;
        public Exception? DisposeFailure;
        public int Subscribers => _observers.Count;
        public IDisposable Subscribe(IObserver<bool> observer)
        {
            Captured = observer;
            _observers.Add(observer);
            observer.OnNext(initial);
            return new Lease(() => { _observers.Remove(observer); if (EmitOnDispose is { } value) observer.OnNext(value); if (DisposeFailure is not null) throw DisposeFailure; });
        }
        public void Publish(bool value) { foreach (var observer in _observers.ToArray()) observer.OnNext(value); }
        public void Fail(Exception error) { foreach (var observer in _observers.ToArray()) observer.OnError(error); }
        public void Dispose() => _observers.Clear();
    }
    private sealed class Lease(Action action) : IDisposable
    {
        private Action? _action = action;
        public void Dispose() { var action = _action; _action = null; action?.Invoke(); }
    }
    private sealed class FailingObservable(Exception error) : IObservable<bool>
    {
        public IDisposable Subscribe(IObserver<bool> observer) => throw error;
    }
}
