using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnObserverLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Returning_add_after_retirement_or_suspension_does_not_reattach(bool dispose, bool after)
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        using var view = TreeDataGridPresentation.Create(source);
        var column = new Column();
        Action retire = dispose ? view.Dispose : view.Suspend;
        if (after) column.AfterAdd = retire; else column.BeforeAdd = retire;
        source.Columns.Add(column);
        Assert.Equal(0, column.Subscribers);
        Assert.Equal(1, column.Adds);
        Assert.Equal(1, column.Removes);
        Assert.Empty(view.NativeColumns);
        if (!dispose)
        {
            view.Resume();
            Assert.Single(view.NativeColumns);
            Assert.Equal(1, column.Subscribers);
        }
        view.Dispose();
        Assert.Equal(0, column.Subscribers);
        Assert.Single(source.Rows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Add_failure_rolls_back_partial_attachment_and_preserves_remove_failure(bool cleanupFails)
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        using var view = TreeDataGridPresentation.Create(source);
        var primary = new InvalidOperationException("add failed");
        var cleanup = new InvalidOperationException("remove failed");
        var column = new Column { AddFailure = primary, RemoveFailure = cleanupFails ? cleanup : null };
        var error = Record.Exception(() => source.Columns.Add(column));
        if (cleanupFails)
        {
            var aggregate = Assert.IsType<AggregateException>(error);
            Assert.Collection(aggregate.InnerExceptions, e => Assert.Same(primary, e), e => Assert.Same(cleanup, e));
        }
        else Assert.Same(primary, error);
        Assert.Equal(0, column.Subscribers);
        Assert.Empty(view.NativeColumns);
        column.AddFailure = column.RemoveFailure = null;
        view.Suspend(); view.Resume();
        Assert.Single(view.NativeColumns);
        Assert.Equal(1, column.Subscribers);
    }

    [Fact]
    public void Removing_a_definition_inside_resume_attachment_does_not_leave_an_obsolete_view()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var first = new Column(); var second = new Column();
        source.Columns.Add(first); source.Columns.Add(second);
        using var view = TreeDataGridPresentation.Create(source);
        view.Suspend();
        first.BeforeAdd = () => source.Columns.RemoveAt(0);
        view.Resume();
        Assert.Single(view.NativeColumns);
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(1, second.Subscribers);
        var changes = 0; view.ColumnsChanged += (_, _) => ++changes;
        second.Notify(); Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Suspension_cleanup_attempts_every_definition_and_preserves_failure_order(bool dispose)
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var columns = Enumerable.Range(0, 3).Select(_ => new Column()).ToArray();
        foreach (var column in columns) source.Columns.Add(column);
        var view = TreeDataGridPresentation.Create(source);
        var first = new InvalidOperationException("first removal");
        var last = new InvalidOperationException("last removal");
        columns[0].RemoveFailure = first; columns[2].RemoveFailure = last;
        var error = Assert.Throws<AggregateException>(dispose ? view.Dispose : view.Suspend);
        Assert.Collection(error.InnerExceptions, e => Assert.Same(first, e), e => Assert.Same(last, e));
        Assert.All(columns, column => Assert.Equal(0, column.Subscribers));
        foreach (var column in columns) column.RemoveFailure = null;
        if (!dispose)
        {
            view.Resume();
            Assert.All(columns, column => Assert.Equal(1, column.Subscribers));
        }
        view.Dispose(); Assert.All(columns, column => Assert.Equal(0, column.Subscribers));
    }

    [Fact]
    public void Resume_requested_inside_remove_waits_until_old_cleanup_finishes()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var column = new Column(); source.Columns.Add(column);
        using var view = TreeDataGridPresentation.Create(source);
        column.BeforeRemove = view.Resume;
        view.Suspend();
        Assert.Equal(1, column.Subscribers);
        Assert.Equal(2, column.Adds);
        Assert.Equal(1, column.Removes);
        var changes = 0; view.ColumnsChanged += (_, _) => ++changes;
        column.Notify(); Assert.Equal(1, changes);
        using var cell = view.RealizeCell(0, 0);
    }

    [Fact]
    public void Recursive_suspend_notification_is_idempotent()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var column = new Column(); source.Columns.Add(column);
        using var view = TreeDataGridPresentation.Create(source);
        var notifications = 0;
        view.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(view.SelectionInteraction)) return;
            ++notifications;
            view.Suspend();
        };
        view.Suspend();
        Assert.Equal(1, notifications);
        Assert.Equal(0, column.Subscribers);
    }

    private sealed class Item { public string Name => "Item"; }
    private sealed class Column : IColumn<Item>
    {
        private readonly ValueColumn<Item, string> _inner = new("Name", item => item.Name, width: new(100));
        private PropertyChangedEventHandler? _handlers;
        private static readonly PropertyChangedEventArgs WidthChanged = new("Width");
        internal Action? BeforeAdd, AfterAdd, BeforeRemove;
        internal Exception? AddFailure, RemoveFailure;
        internal int Adds, Removes;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public string Id => _inner.Id;
        public object? Header => _inner.Header;
        public GridLength Width { get => _inner.Width; set => _inner.Width = value; }
        public bool IsVisible { get => _inner.IsVisible; set => _inner.IsVisible = value; }
        public string? PresentationKey { get => _inner.PresentationKey; set => _inner.PresentationKey = value; }
        public ListSortDirection? SortDirection { get => _inner.SortDirection; set => _inner.SortDirection = value; }
        public object? Tag { get => _inner.Tag; set => _inner.Tag = value; }
        public Comparison<Item?>? GetComparison(ListSortDirection direction) => _inner.GetComparison(direction);
        public TResult Accept<TResult>(IColumnVisitor<Item, TResult> visitor) => visitor.Visit(_inner);
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                ++Adds;
                var before = BeforeAdd; BeforeAdd = null; before?.Invoke();
                _handlers += value;
                var after = AfterAdd; AfterAdd = null; after?.Invoke();
                if (AddFailure is { } error) throw error;
            }
            remove
            {
                ++Removes;
                var before = BeforeRemove; BeforeRemove = null; before?.Invoke();
                _handlers -= value;
                if (RemoveFailure is { } error) throw error;
            }
        }
        internal void Notify() => _handlers?.Invoke(this, WidthChanged);
    }
}
