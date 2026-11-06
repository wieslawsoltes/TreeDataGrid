using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Uno.Controls;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class DeclarativeOwnershipReviewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_event_attachment_rolls_back_even_when_the_publisher_registered_first(bool registered)
    {
        var source = new ReentrantList();
        var view = new DeclarativeItemsSource(source);
        var failure = new InvalidOperationException("add");
        source.BeforeAdd = registered ? null : () => throw failure;
        source.AfterAdd = registered ? () => throw failure : null;
        var calls = 0;
        NotifyCollectionChangedEventHandler handler = (_, _) => ++calls;
        Assert.Same(failure, Record.Exception(() => view.CollectionChanged += handler));
        Assert.Equal(0, source.Subscribers);
        Assert.Equal(1, source.RemoveAttempts);
        source.EmitCaptured();
        Assert.Equal(0, calls);
        source.BeforeAdd = source.AfterAdd = null;
        view.CollectionChanged += handler;
        source.Emit();
        Assert.Equal(1, calls);
        view.CollectionChanged -= handler;
        Assert.Equal(0, source.Subscribers);
    }

    [Fact]
    public void Failed_attachment_preserves_original_and_cleanup_error_order()
    {
        var source = new ReentrantList();
        var view = new DeclarativeItemsSource(source);
        var add = new InvalidOperationException("add");
        var remove = new InvalidOperationException("remove");
        source.AfterAdd = () => throw add;
        source.AfterRemove = () => throw remove;
        NotifyCollectionChangedEventHandler handler = (_, _) => throw new Exception("retired listener ran");
        var error = Assert.IsType<AggregateException>(Record.Exception(() => view.CollectionChanged += handler));
        Assert.Equal(new Exception[] { add, remove }, error.InnerExceptions);
        Assert.Equal(0, source.Subscribers);
        source.EmitCaptured();
        view.CollectionChanged -= handler;
        Assert.Equal(1, source.RemoveAttempts);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Removal_during_add_defers_exactly_one_detachment(bool after, bool throwing)
    {
        var source = new ReentrantList(); var view = new DeclarativeItemsSource(source);
        NotifyCollectionChangedEventHandler handler = (_, _) => { };
        var failure = new InvalidOperationException("after retirement");
        void Retire() { view.CollectionChanged -= handler; if (throwing) throw failure; }
        if (after) source.AfterAdd = Retire; else source.BeforeAdd = Retire;
        var error = Record.Exception(() => view.CollectionChanged += handler);
        if (throwing) Assert.Same(failure, error); else Assert.Null(error);
        Assert.Equal(0, source.Subscribers);
        Assert.Equal(1, source.RemoveAttempts);
    }

    [Fact]
    public void Deferred_removal_failure_does_not_replace_the_add_failure()
    {
        var source = new ReentrantList(); var view = new DeclarativeItemsSource(source);
        NotifyCollectionChangedEventHandler handler = (_, _) => { };
        var add = new InvalidOperationException("add"); var remove = new InvalidOperationException("remove");
        source.AfterAdd = () => { view.CollectionChanged -= handler; throw add; };
        source.AfterRemove = () => throw remove;
        var error = Assert.IsType<AggregateException>(Record.Exception(() => view.CollectionChanged += handler));
        Assert.Equal(new Exception[] { add, remove }, error.InnerExceptions);
        Assert.Equal(0, source.Subscribers);
        Assert.Equal(1, source.RemoveAttempts);
    }

    [Fact]
    public void Failed_old_attachment_cannot_remove_a_new_generation_of_the_same_delegate()
    {
        var source = new ReentrantList(); var view = new DeclarativeItemsSource(source);
        var calls = 0; NotifyCollectionChangedEventHandler handler = (_, _) => ++calls;
        var failure = new InvalidOperationException("retired add");
        source.AfterAdd = () =>
        {
            source.AfterAdd = null;
            view.CollectionChanged -= handler;
            view.CollectionChanged += handler;
            throw failure;
        };
        Assert.Same(failure, Record.Exception(() => view.CollectionChanged += handler));
        Assert.Equal(1, source.Subscribers);
        source.Emit();
        Assert.Equal(1, calls);
        view.CollectionChanged -= handler;
        Assert.Equal(0, source.Subscribers);
    }

    [Fact]
    public void Duplicate_listener_removal_keeps_one_underlying_subscription()
    {
        var source = new ReentrantList(); var view = new DeclarativeItemsSource(source);
        var calls = 0; NotifyCollectionChangedEventHandler handler = (sender, _) => { Assert.Same(view, sender); ++calls; };
        view.CollectionChanged += handler; view.CollectionChanged += handler;
        source.Emit(); Assert.Equal(2, calls); Assert.Equal(1, source.Subscribers);
        view.CollectionChanged -= handler; source.Emit(); Assert.Equal(3, calls);
        view.CollectionChanged -= handler; Assert.Equal(0, source.Subscribers);
    }

    [Fact]
    public void Context_detaches_its_resource_list_before_recursive_disposal()
    {
        var context = new DeclarativeSourceContext(null, null); var order = new List<string>();
        var reentered = false;
        var first = context.Own(new Lease(() =>
        {
            order.Add("first");
            if (!reentered) { reentered = true; context.Dispose(); }
        }));
        var second = context.Own(new Lease(() => order.Add("second")));
        context.Dispose(); context.Dispose();
        Assert.Equal(new[] { "first", "second" }, order);
        Assert.Equal(1, first.Disposals); Assert.Equal(1, second.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Context_releases_every_resource_and_preserves_error_identity(bool multiple)
    {
        var context = new DeclarativeSourceContext(null, null);
        var first = new InvalidOperationException("first"); var second = new InvalidOperationException("second");
        var a = context.Own(new Lease(() => throw first));
        var b = context.Own(new Lease(() => { if (multiple) throw second; }));
        var c = context.Own(new Lease(() => { }));
        var error = Record.Exception(context.Dispose);
        if (multiple) Assert.Equal(new Exception[] { first, second }, Assert.IsType<AggregateException>(error).InnerExceptions);
        else Assert.Same(first, error);
        context.Dispose();
        Assert.Equal(1, a.Disposals); Assert.Equal(1, b.Disposals); Assert.Equal(1, c.Disposals);
    }

    [Fact]
    public void Resources_offered_during_retirement_are_released_and_rejected()
    {
        var context = new DeclarativeSourceContext(null, null);
        var incoming = new Lease(() => { });
        Exception? rejected = null;
        context.Own(new Lease(() => rejected = Record.Exception(() => context.Own(incoming))));
        var trailing = context.Own(new Lease(() => { }));
        context.Dispose();
        Assert.IsType<ObjectDisposedException>(rejected);
        Assert.Equal(1, incoming.Disposals); Assert.Equal(1, trailing.Disposals);
    }

    [Fact]
    public void Late_resource_cleanup_error_preserves_retirement_error_as_primary()
    {
        var context = new DeclarativeSourceContext(null, null); context.Dispose();
        var cleanup = new InvalidOperationException("late cleanup");
        var incoming = new Lease(() => throw cleanup);
        var error = Assert.Throws<AggregateException>(() => context.Own(incoming));
        Assert.IsType<ObjectDisposedException>(error.InnerExceptions[0]);
        Assert.Same(cleanup, error.InnerExceptions[1]); Assert.Equal(1, incoming.Disposals);
    }

    [Fact]
    public void Creation_failure_is_not_masked_by_owned_accessor_cleanup()
    {
        var primary = new InvalidOperationException("column factory");
        var cleanup = new InvalidOperationException("accessor cleanup");
        var lease = new Lease(() => throw cleanup);
        var error = Assert.Throws<AggregateException>(() => DeclarativeSource.Create(new object[] { new() },
            new TreeDataGridColumn[] { new FailingDefinition(lease, primary) }));
        Assert.Same(primary, error.InnerExceptions[0]);
        Assert.Same(cleanup, error.InnerExceptions[1]);
        Assert.Equal(1, lease.Disposals);
    }

    [Fact]
    public void Generated_source_retirement_detaches_the_projection_even_with_core_selection()
    {
        var items = new ReentrantList { new object() };
        using var owner = DeclarativeSource.Create(items, new TreeDataGridColumn[] { new TreeDataGridRowHeaderColumn() });
        _ = owner.Source.Rows;
        _ = owner.Source.Selection;
        Assert.Equal(1, items.Subscribers);
        owner.Dispose();
        Assert.Equal(0, items.Subscribers);
        items.EmitCaptured();
        items.Add(new object());
        Assert.Equal(2, items.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Projection_retirement_during_add_is_terminal_and_releases_late_registration(bool throwing)
    {
        var items = new ReentrantList();
        var view = new DeclarativeItemsSource(items);
        var owner = Assert.IsAssignableFrom<IDisposable>((object)view);
        var failure = new InvalidOperationException("late add");
        var calls = 0;
        NotifyCollectionChangedEventHandler handler = (_, _) => ++calls;
        items.AfterAdd = () => { owner.Dispose(); if (throwing) throw failure; };
        var error = Record.Exception(() => view.CollectionChanged += handler);
        if (throwing) Assert.Same(failure, error); else Assert.Null(error);
        Assert.Equal(0, items.Subscribers);
        Assert.Equal(1, items.RemoveAttempts);
        items.EmitCaptured(); Assert.Equal(0, calls);
        Assert.Throws<ObjectDisposedException>(() => view.CollectionChanged += handler);
        owner.Dispose(); Assert.Equal(1, items.RemoveAttempts);
    }

    private sealed class FailingDefinition(Lease lease, Exception error) : TreeDataGridColumn
    {
        internal override TreeDataGridCore.Models.IColumn<TModel> CreateCoreColumn<TModel>(object? header = null,
            ColumnCreateOptions? common = null, DeclarativeSourceContext? context = null)
        { context!.Own(lease); throw error; }
    }
    private sealed class Lease(Action cleanup) : IDisposable
    {
        internal int Disposals;
        public void Dispose() { ++Disposals; cleanup(); }
    }
    private sealed class ReentrantList : ArrayList, INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _handlers;
        private NotifyCollectionChangedEventHandler? _captured;
        internal Action? BeforeAdd, AfterAdd, AfterRemove;
        internal int RemoveAttempts;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { _captured = value; BeforeAdd?.Invoke(); _handlers += value; AfterAdd?.Invoke(); }
            remove { ++RemoveAttempts; _handlers -= value; AfterRemove?.Invoke(); }
        }
        internal void Emit() => _handlers?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
        internal void EmitCaptured() => _captured?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
    }
}
