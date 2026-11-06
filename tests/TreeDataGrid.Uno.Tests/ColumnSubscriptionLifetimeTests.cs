using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Xunit;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnSubscriptionLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Removal_commits_before_a_throwing_unsubscribe(bool retainHandler)
    {
        var error = new InvalidOperationException("remove");
        var column = new ProbeColumn { RemoveError = error, RetainOnRemove = retainHandler };
        var columns = new U.ColumnListBase<ProbeColumn> { column };
        var removed = 0;
        columns.CollectionChanged += (_, e) =>
        {
            Assert.Equal(NotifyCollectionChangedAction.Remove, e.Action);
            Assert.Empty(columns);
            ++removed;
        };
        Assert.Same(error, Record.Exception(() => columns.RemoveAt(0)));
        Assert.Empty(columns);
        Assert.Equal(1, removed);
        Assert.Equal((-1, -1d), columns.GetColumnAt(0));
        Assert.Equal(0, column.Disposals);
        Assert.Equal(1, column.Removes);
    }

    [Fact]
    public void Clear_attempts_every_unique_observer_and_preserves_error_order()
    {
        var firstError = new InvalidOperationException("first");
        var secondError = new InvalidOperationException("second");
        var first = new ProbeColumn { RemoveError = firstError };
        var second = new ProbeColumn { RemoveError = secondError };
        var columns = new U.ColumnListBase<ProbeColumn> { first, second, first };
        var resets = 0;
        columns.CollectionChanged += (_, e) => { Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action); ++resets; };
        var failure = Assert.Throws<AggregateException>(() => columns.Clear());
        Assert.Equal(new Exception[] { firstError, secondError }, failure.InnerExceptions);
        Assert.Empty(columns);
        Assert.Equal(1, resets);
        Assert.Equal(1, first.Removes);
        Assert.Equal(1, second.Removes);
        Assert.Equal(0, first.Subscribers + second.Subscribers);
        Assert.Equal(0, first.Disposals + second.Disposals);
    }

    [Fact]
    public void Failed_replacement_attachment_keeps_the_original_observed_and_usable()
    {
        var original = new ProbeColumn();
        var failure = new InvalidOperationException("add");
        var replacement = new ProbeColumn { AddError = failure };
        var columns = new U.ColumnListBase<ProbeColumn> { original };
        columns.CommitActualWidths();
        Assert.Same(failure, Record.Exception(() => columns[0] = replacement));
        Assert.Same(original, Assert.Single(columns));
        Assert.Equal(1, original.Subscribers);
        Assert.Equal(0, original.Removes);
        Assert.Equal(0, replacement.Subscribers);
        original.ChangeWidth(120);
        Assert.Equal((0, 0d), columns.GetColumnAt(119));
        columns.CellMeasured(0, 0, new Size(120, 20));
        columns.CommitActualWidths();
        columns.Clear();
    }

    [Fact]
    public void Failed_attachment_and_rollback_preserve_both_exceptions()
    {
        var add = new InvalidOperationException("add");
        var remove = new InvalidOperationException("rollback");
        var column = new ProbeColumn { AddError = add, RemoveError = remove };
        var columns = new U.ColumnListBase<ProbeColumn>();
        var failure = Assert.Throws<AggregateException>(() => columns.Add(column));
        Assert.Equal(new Exception[] { add, remove }, failure.InnerExceptions);
        Assert.Empty(columns);
        Assert.Equal(0, column.Subscribers);
        column.AddError = column.RemoveError = null;
        columns.Add(column);
        columns.CellMeasured(0, 0, new Size(80, 20));
        columns.Clear();
        Assert.Equal(0, column.Subscribers);
    }

    [Fact]
    public void Remove_callback_can_readd_the_same_column_without_losing_the_new_observer()
    {
        var column = new ProbeColumn();
        var columns = new U.ColumnListBase<ProbeColumn> { column };
        column.OnRemove = () =>
        {
            column.OnRemove = null;
            Assert.Empty(columns);
            columns.Add(column);
        };
        columns.RemoveAt(0);
        Assert.Same(column, Assert.Single(columns));
        Assert.Equal(2, column.Adds);
        Assert.Equal(1, column.Subscribers);
        column.ChangeWidth(120);
        Assert.Equal((0, 0d), columns.GetColumnAt(119));
        columns.Clear();
        Assert.Equal(0, column.Subscribers);
    }

    [Fact]
    public void Clear_callback_can_populate_a_new_collection_during_old_cleanup()
    {
        var first = new ProbeColumn();
        var second = new ProbeColumn();
        var next = new ProbeColumn();
        var columns = new U.ColumnListBase<ProbeColumn> { first, second };
        first.OnRemove = () => { Assert.Empty(columns); columns.Add(next); };
        columns.Clear();
        Assert.Same(next, Assert.Single(columns));
        Assert.Equal(0, first.Subscribers + second.Subscribers);
        Assert.Equal(1, next.Subscribers);
        columns.CellMeasured(0, 0, new Size(80, 20));
        columns.CommitActualWidths();
        columns.Clear();
    }

    [Fact]
    public void Replacement_is_visible_to_the_retired_columns_remove_callback()
    {
        var first = new ProbeColumn();
        var next = new ProbeColumn();
        var columns = new U.ColumnListBase<ProbeColumn> { first };
        first.OnRemove = () =>
        {
            Assert.Same(next, Assert.Single(columns));
            columns.CellMeasured(0, 0, new Size(80, 20));
            columns.CommitActualWidths();
        };
        columns[0] = next;
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(1, next.Subscribers);
        columns.Clear();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retained_retired_handlers_cannot_invalidate_current_geometry(bool failedAttachment)
    {
        var retired = new ProbeColumn { RetainOnRemove = true, RemoveError = new InvalidOperationException("remove") };
        var current = new ProbeColumn();
        var columns = new U.ColumnListBase<ProbeColumn>();
        if (failedAttachment)
        {
            retired.AddError = new InvalidOperationException("add");
            Assert.Throws<AggregateException>(() => columns.Add(retired));
        }
        else
        {
            columns.Add(retired);
            Assert.Throws<InvalidOperationException>(() => columns.RemoveAt(0));
        }
        columns.Add(current);
        Assert.Equal((0, 0d), columns.GetColumnAt(1));
        var reads = current.ActualReads;
        retired.Notify();
        Assert.Equal((0, 0d), columns.GetColumnAt(1));
        Assert.Equal(reads, current.ActualReads);
        Assert.Equal(1, retired.Removes);
        columns.Clear();
    }

    [Fact]
    public void Nested_mutation_during_add_preserves_the_newer_collection_and_rejects_the_stale_insert()
    {
        var old = new ProbeColumn();
        var next = new ProbeColumn();
        var pending = new ProbeColumn();
        var columns = new U.ColumnListBase<ProbeColumn> { old };
        pending.OnAdd = () => { columns.Clear(); columns.Add(next); };
        Assert.Throws<InvalidOperationException>(() => columns.Insert(1, pending));
        Assert.Same(next, Assert.Single(columns));
        Assert.Equal(0, pending.Subscribers + old.Subscribers);
        Assert.Equal(1, next.Subscribers);
        columns.CellMeasured(0, 0, new Size(80, 20));
        columns.Clear();
    }

    [Fact]
    public void Nested_add_of_the_same_column_has_one_current_observer()
    {
        var column = new ProbeColumn();
        var columns = new U.ColumnListBase<ProbeColumn>();
        column.OnAdd = () => { column.OnAdd = null; columns.Add(column); };
        Assert.Throws<InvalidOperationException>(() => columns.Add(column));
        Assert.Same(column, Assert.Single(columns));
        Assert.Equal(2, column.Adds);
        Assert.Equal(1, column.Removes);
        Assert.Equal(1, column.Subscribers);
        column.ChangeWidth(120);
        Assert.Equal((0, 0d), columns.GetColumnAt(119));
        columns.Clear();
        Assert.Equal(0, column.Subscribers);
    }

    [Fact]
    public void Layout_only_reentry_during_add_does_not_reject_the_insert()
    {
        var columns = new U.ColumnListBase<ProbeColumn> { new() };
        var next = new ProbeColumn { OnAdd = () => { columns.CommitActualWidths(); columns.GetColumnAt(0); } };
        columns.Add(next);
        Assert.Equal(2, columns.Count);
        Assert.Equal((1, 80d), columns.GetColumnAt(81));
        columns.CellMeasured(1, 0, new Size(80, 20));
        columns.Clear();
    }

    [Fact]
    public void Same_instance_replacement_and_duplicate_removal_do_not_resubscribe()
    {
        var column = new ProbeColumn();
        var columns = new U.ColumnListBase<ProbeColumn> { column, column };
        columns[0] = column;
        columns.RemoveAt(0);
        Assert.Equal(1, column.Adds);
        Assert.Equal(0, column.Removes);
        Assert.Equal(1, column.Subscribers);
        columns.Clear();
        Assert.Equal(1, column.Removes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Notification_failure_does_not_skip_cleanup_or_replace_the_primary_exception(bool clear)
    {
        var notification = new InvalidOperationException("notification");
        var cleanup = new InvalidOperationException("cleanup");
        var column = new ProbeColumn { RemoveError = cleanup };
        var columns = new U.ColumnListBase<ProbeColumn> { column };
        columns.CollectionChanged += (_, _) => throw notification;
        var error = Assert.Throws<AggregateException>(() => { if (clear) columns.Clear(); else columns.RemoveAt(0); });
        Assert.Equal(new Exception[] { notification, cleanup }, error.InnerExceptions);
        Assert.Empty(columns);
        Assert.Equal(0, column.Subscribers);
    }

    [Fact]
    public void Successful_cleanup_preserves_notification_exception_identity()
    {
        var notification = new InvalidOperationException("notification");
        var column = new ProbeColumn();
        var columns = new U.ColumnListBase<ProbeColumn> { column };
        columns.PropertyChanged += (_, _) => throw notification;
        Assert.Same(notification, Record.Exception(() => columns.Clear()));
        Assert.Empty(columns);
        Assert.Equal(0, column.Subscribers);
    }

    [Fact]
    public void Range_removal_publishes_the_complete_batch_before_cleanup_failures()
    {
        var firstError = new InvalidOperationException("first");
        var secondError = new InvalidOperationException("second");
        var first = new ProbeColumn { RemoveError = firstError };
        var second = new ProbeColumn { RemoveError = secondError };
        var columns = new U.ColumnListBase<ProbeColumn> { first, second };
        var events = 0;
        columns.CollectionChanged += (_, e) =>
        {
            Assert.Equal(NotifyCollectionChangedAction.Remove, e.Action);
            Assert.Equal(2, e.OldItems!.Count);
            Assert.Empty(columns);
            ++events;
        };
        var error = Assert.Throws<AggregateException>(() => columns.RemoveRange(0, 2));
        Assert.Equal(new Exception[] { firstError, secondError }, error.InnerExceptions);
        Assert.Equal(1, events);
        Assert.Equal(0, first.Subscribers + second.Subscribers);
    }

    [Fact]
    public void Reset_defers_retirement_until_the_replacement_batch_is_published()
    {
        var cleanup = new InvalidOperationException("cleanup");
        var first = new ProbeColumn { RemoveError = cleanup };
        var next = new ProbeColumn();
        var columns = new U.ColumnListBase<ProbeColumn> { first };
        var events = 0;
        columns.CollectionChanged += (_, e) => { Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action); ++events; };
        Assert.Same(cleanup, Record.Exception(() => columns.Reset(list => { list.Clear(); list.Add(next); })));
        Assert.Same(next, Assert.Single(columns));
        Assert.Equal(1, events);
        Assert.Equal(1, next.Subscribers);
        columns.Clear();
    }

    [Fact]
    public void Invalid_insert_and_null_replacement_do_not_change_observation()
    {
        var original = new ProbeColumn();
        var columns = new U.ColumnListBase<ProbeColumn> { original };
        Assert.Throws<ArgumentOutOfRangeException>(() => columns.Insert(2, new()));
        Assert.Throws<ArgumentNullException>(() => columns[0] = null!);
        Assert.Same(original, Assert.Single(columns));
        Assert.Equal(1, original.Subscribers);
        Assert.Equal(0, original.Removes);
        columns.Clear();
    }

    private sealed class ProbeColumn : U.IColumn, U.IUpdateColumnLayout, IDisposable
    {
        private PropertyChangedEventHandler? _handlers;
        private double _actualWidth = 80;
        public Action? OnAdd, OnRemove;
        public Exception? AddError, RemoveError;
        public bool RetainOnRemove;
        public int Adds, Removes, Disposals, ActualReads;
        public int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public double ActualWidth { get { ++ActualReads; return _actualWidth; } }
        public bool? CanUserResize => true;
        public object? Header => "Probe";
        public GridLength Width { get; private set; } = new(80);
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public double CellMeasured(double width, int rowIndex) => width;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(GridLength width) => Width = width;
        public void ChangeWidth(double width) { _actualWidth = width; Width = new(width); Notify(); }
        public void Notify() => _handlers?.Invoke(this, new(nameof(ActualWidth)));
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { ++Adds; _handlers += value; OnAdd?.Invoke(); if (AddError is { } error) throw error; }
            remove { ++Removes; if (!RetainOnRemove) _handlers -= value; OnRemove?.Invoke(); if (RemoveError is { } error) throw error; }
        }
        public void Dispose() => ++Disposals;
    }
}
