using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class ExpanderOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Disposal_attempts_all_owners_and_preserves_failure_order(bool multiple)
    {
        var model = new Model();
        var row = new Row(model);
        var inner = new Inner();
        var cell = new ExpanderCellValue<Model>(Column(), inner, row);
        var rowFailure = new InvalidOperationException("row removal");
        var modelFailure = new InvalidOperationException("model removal");
        var childrenFailure = new InvalidOperationException("children removal");
        var innerFailure = new InvalidOperationException("inner disposal");
        row.RemoveFailure = rowFailure;
        if (multiple)
        {
            model.RemoveFailure = modelFailure;
            model.Children.RemoveFailure = childrenFailure;
            inner.Failure = innerFailure;
        }
        var error = Record.Exception(cell.Dispose);
        if (multiple)
        {
            var aggregate = Assert.IsType<AggregateException>(error);
            Assert.Collection(aggregate.InnerExceptions,
                e => Assert.Same(rowFailure, e), e => Assert.Same(modelFailure, e),
                e => Assert.Same(childrenFailure, e), e => Assert.Same(innerFailure, e));
        }
        else Assert.Same(rowFailure, error);
        Assert.Equal(0, row.Subscribers);
        Assert.Equal(0, model.Subscribers);
        Assert.Equal(0, model.Children.Subscribers);
        Assert.Equal(1, inner.Disposals);
        cell.Dispose();
        Assert.Equal(1, inner.Disposals);
        Assert.False(cell.CanWrite);
        Assert.False(cell.ShowExpander);
        Assert.Throws<ObjectDisposedException>(() => cell.Write("retired"));
        Assert.Throws<ObjectDisposedException>(() => cell.IsExpanded = true);
        Assert.False(row.IsExpanded);
    }

    [Fact]
    public void Constructor_failure_preserves_the_primary_and_inner_cleanup_failures()
    {
        var model = new Model();
        var row = new Row(model);
        var primary = new InvalidOperationException("add failed after attachment");
        var cleanup = new InvalidOperationException("inner failed");
        row.AddFailure = primary;
        var inner = new Inner { Failure = cleanup };
        var error = Assert.Throws<AggregateException>(() => new ExpanderCellValue<Model>(Column(), inner, row));
        Assert.Collection(error.InnerExceptions, e => Assert.Same(primary, e), e => Assert.Same(cleanup, e));
        Assert.Equal(0, row.Subscribers);
        Assert.Equal(0, model.Subscribers);
        Assert.Equal(0, model.Children.Subscribers);
        Assert.Equal(1, inner.Disposals);
    }

    [Fact]
    public void Disposing_before_an_add_accessor_finishes_does_not_leak_its_handler()
    {
        var model = new Model();
        var row = new Row(model);
        row.BeforeAdd = handler => ((IDisposable)handler.Target!).Dispose();
        var inner = new Inner();
        using var cell = new ExpanderCellValue<Model>(Column(), inner, row);
        Assert.Equal(0, row.Subscribers);
        Assert.Equal(0, model.Subscribers);
        Assert.Equal(0, model.Children.Subscribers);
        Assert.Equal(1, inner.Disposals);
        Assert.False(cell.CanEdit);
    }

    [Fact]
    public void Retired_child_getter_result_is_not_subscribed_or_published()
    {
        var model = new Model();
        var row = new Row(model);
        var inner = new Inner();
        using var cell = new ExpanderCellValue<Model>(Column(), inner, row);
        var old = model.Children;
        var next = new Children();
        model.Children = next;
        var notifications = 0;
        cell.PropertyChanged += (_, _) => ++notifications;
        model.OnChildrenRead = cell.Dispose;
        model.Notify();
        Assert.Equal(0, old.Subscribers);
        Assert.Equal(0, next.Subscribers);
        Assert.Equal(0, model.Subscribers);
        Assert.Equal(0, row.Subscribers);
        Assert.Equal(0, notifications);
        Assert.Equal(1, inner.Disposals);
    }

    [Fact]
    public void Newer_nested_child_getter_change_wins_without_recursive_subscription()
    {
        var model = new Model();
        using var cell = new ExpanderCellValue<Model>(Column(), new Inner(), new Row(model));
        var old = model.Children;
        var obsolete = new Children();
        var current = new Children();
        model.Children = obsolete;
        model.OnChildrenRead = () => { model.Children = current; model.Notify(); };
        model.Notify();
        Assert.Equal(0, old.Subscribers);
        Assert.Equal(0, obsolete.Subscribers);
        Assert.Equal(1, current.Subscribers);
        cell.Dispose();
        Assert.Equal(0, current.Subscribers);
    }

    [Fact]
    public void Detachment_callback_cannot_overwrite_a_newer_child_source()
    {
        var model = new Model();
        using var cell = new ExpanderCellValue<Model>(Column(), new Inner(), new Row(model));
        var old = model.Children;
        var obsolete = new Children();
        var current = new Children();
        old.OnRemove = () => { model.Children = current; model.Notify(); };
        model.Children = obsolete;
        model.Notify();
        Assert.Equal(0, old.Subscribers);
        Assert.Equal(0, obsolete.Subscribers);
        Assert.Equal(1, current.Subscribers);
    }

    [Fact]
    public void Disposing_inside_child_attachment_waits_for_the_accessor_to_finish()
    {
        var model = new Model();
        var inner = new Inner();
        using var cell = new ExpanderCellValue<Model>(Column(), inner, new Row(model));
        var old = model.Children;
        var next = new Children { BeforeAdd = _ => cell.Dispose() };
        model.Children = next;
        model.Notify();
        Assert.Equal(0, old.Subscribers);
        Assert.Equal(0, next.Subscribers);
        Assert.Equal(0, model.Subscribers);
        Assert.Equal(1, inner.Disposals);
    }

    [Fact]
    public void Captured_notifications_do_not_publish_after_disposal()
    {
        var model = new Model();
        var row = new Row(model);
        var inner = new Inner();
        ExpanderCellValue<Model>? cell = null;
        model.PropertyChanged += (_, _) => cell!.Dispose();
        using (cell = new ExpanderCellValue<Model>(Column(), inner, row))
        {
            var changes = 0;
            cell.PropertyChanged += (_, _) => ++changes;
            model.Notify();
            row.Notify();
            inner.Notify();
            model.Children.Notify();
            Assert.Equal(0, changes);
            Assert.Equal(1, inner.Disposals);
        }
    }

    [Fact]
    public void Disposal_detaches_the_observed_model_without_reinvoking_row_getters()
    {
        var first = new Model();
        var second = new Model();
        var row = new Row(first);
        using var cell = new ExpanderCellValue<Model>(Column(), new Inner(), row);
        row.Current = second;
        row.FailModelRead = true;
        cell.Dispose();
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(0, first.Children.Subscribers);
        Assert.Equal(0, second.Subscribers);
    }

    [Fact]
    public void Explicit_has_children_binding_stays_lazy_and_independent_per_row()
    {
        var column = Column(explicitHasChildren: true);
        var first = new Model { HasChildren = true };
        var second = new Model { HasChildren = false };
        first.OnChildrenRead = second.OnChildrenRead = () => throw new InvalidOperationException("Must stay lazy");
        using var a = new ExpanderCellValue<Model>(column, new Inner(), new Row(first));
        using var b = new ExpanderCellValue<Model>(column, new Inner(), new Row(second));
        Assert.True(a.ShowExpander);
        Assert.False(b.ShowExpander);
        first.HasChildren = false;
        first.Notify();
        second.HasChildren = true;
        second.Notify();
        Assert.False(a.ShowExpander);
        Assert.True(b.ShowExpander);
        a.Dispose(); b.Dispose();
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(0, second.Subscribers);
    }

    [Fact]
    public void Has_children_definition_is_shared_per_column_not_per_realized_cell()
    {
        var column = Column(explicitHasChildren: true);
        var definition = ExpanderCellValue<Model>.GetHasChildrenDefinition(column);
        Assert.NotNull(definition);
        Assert.Same(column.HasChildrenSelector, definition.GetterExpression);
        Assert.Same(definition, ExpanderCellValue<Model>.GetHasChildrenDefinition(column));
        Assert.NotSame(definition, ExpanderCellValue<Model>.GetHasChildrenDefinition(Column(true)));
        Assert.Null(ExpanderCellValue<Model>.GetHasChildrenDefinition(Column()));
        for (var i = 0; i < 1024; ++i) ExpanderCellValue<Model>.GetHasChildrenDefinition(column);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) ExpanderCellValue<Model>.GetHasChildrenDefinition(column);
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Weak_definition_cache_does_not_root_discarded_columns()
    {
        var (column, definition) = CreateWeakDefinition();
        for (var i = 0; i < 3 && (column.IsAlive || definition.IsAlive); ++i)
        { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(column.IsAlive);
        Assert.False(definition.IsAlive);
    }

    [Fact]
    public void Warm_child_notifications_allocate_no_new_event_arguments()
    {
        var model = new Model();
        using var cell = new ExpanderCellValue<Model>(Column(), new Inner(), new Row(model));
        var count = 0;
        cell.PropertyChanged += (_, _) => ++count;
        for (var i = 0; i < 1024; ++i) model.Children.Notify();
        count = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) model.Children.Notify();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(4096, count);
        Assert.Equal(0L, allocated);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Column, WeakReference Definition) CreateWeakDefinition()
    {
        var column = Column(true);
        var definition = ExpanderCellValue<Model>.GetHasChildrenDefinition(column)!;
        Assert.True(definition.GetValue(new Model { HasChildren = true }));
        return (new(column), new(definition));
    }
    private static HierarchicalExpanderColumn<Model> Column(bool explicitHasChildren = false) => new(
        ValueColumn<Model, string>.FromDelegate("Name", static _ => "Name"), static model => model.ReadChildren(),
        explicitHasChildren ? static model => model.HasChildren : null);

    private class Observable : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs Changed = new(null);
        private PropertyChangedEventHandler? _handlers;
        internal Action<PropertyChangedEventHandler>? BeforeAdd;
        internal Exception? AddFailure;
        internal Exception? RemoveFailure;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { BeforeAdd?.Invoke(value!); _handlers += value; if (AddFailure is { } error) throw error; }
            remove { _handlers -= value; if (RemoveFailure is { } error) throw error; }
        }
        internal void Notify() => _handlers?.Invoke(this, Changed);
    }
    private sealed class Model : Observable
    {
        public bool HasChildren { get; set; }
        internal Children Children = new();
        internal Action? OnChildrenRead;
        internal Children ReadChildren()
        {
            var result = Children;
            var callback = OnChildrenRead; OnChildrenRead = null;
            callback?.Invoke();
            return result;
        }
    }
    private sealed class Row(Model model) : Observable, IExpanderRow<Model>
    {
        internal Model Current = model;
        internal bool FailModelRead;
        public Model Model => FailModelRead ? throw new InvalidOperationException("Unexpected model getter") : Current;
        object? IRow.Model => Model;
        public object? Header => null;
        public GridLength Height { get; set; } = GridLength.Auto;
        public bool IsExpanded { get; set; }
        public bool ShowExpander { get; private set; } = true;
        public void UpdateShowExpander(bool value) { ShowExpander = value; Notify(); }
    }
    private sealed class Inner : CellValue
    {
        private static readonly PropertyChangedEventArgs Changed = new("Value");
        internal int Disposals;
        internal Exception? Failure;
        public override object? Value => "Value";
        public override bool CanEdit => true;
        public override void Write(object? value) { }
        public override void Dispose() { ++Disposals; if (Failure is { } error) throw error; }
        internal void Notify() => RaisePropertyChanged(Changed);
    }
    private sealed class Children : IEnumerable<Model>, INotifyCollectionChanged
    {
        private static readonly NotifyCollectionChangedEventArgs Changed = new(NotifyCollectionChangedAction.Reset);
        private NotifyCollectionChangedEventHandler? _handlers;
        internal Action<NotifyCollectionChangedEventHandler>? BeforeAdd;
        internal Action? OnRemove;
        internal Exception? RemoveFailure;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { BeforeAdd?.Invoke(value!); _handlers += value; }
            remove
            {
                _handlers -= value;
                var callback = OnRemove; OnRemove = null; callback?.Invoke();
                if (RemoveFailure is { } error) throw error;
            }
        }
        internal void Notify() => _handlers?.Invoke(this, Changed);
        public IEnumerator<Model> GetEnumerator() => ((IEnumerable<Model>)Array.Empty<Model>()).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
