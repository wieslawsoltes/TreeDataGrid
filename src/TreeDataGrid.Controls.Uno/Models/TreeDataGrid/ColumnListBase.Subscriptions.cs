using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace Uno.Controls.Models.TreeDataGrid;

public partial class ColumnListBase<TColumn> where TColumn : class, IColumn
{
    // This revision tracks structure, not layout. An event accessor may safely
    // query or commit widths without retiring a pending collection operation.
    private int _collectionRevision;
    private int _columnMutationBatchDepth;
    private List<ColumnSubscription>? _deferredColumnCleanup;
    private readonly Dictionary<IColumn, (ColumnSubscription Subscription, int Count)> _subscriptions =
        new(ReferenceEqualityComparer.Instance);

    private void AcquireColumn(TColumn column)
    {
        if (_subscriptions.TryGetValue(column, out var existing))
        {
            _subscriptions[column] = (existing.Subscription, checked(existing.Count + 1));
            return;
        }

        var revision = _collectionRevision;
        var subscription = new ColumnSubscription(this, column);
        try
        {
            // Stage observation without publishing ownership or accepting events.
            // A nested mutation gets its own observer and wins over this operation.
            subscription.Attach();
            if (revision != _collectionRevision)
                throw new InvalidOperationException("The column collection changed while installing a column observer.");
            CheckReentrancy();
            _subscriptions.Add(column, (subscription, 1));
            subscription.Activate();
        }
        catch (Exception error)
        {
            CleanupColumn(subscription, error);
            throw;
        }
    }

    private ColumnSubscription? RetireColumn(IColumn column)
    {
        var existing = _subscriptions[column];
        if (existing.Count > 1)
        {
            _subscriptions[column] = (existing.Subscription, existing.Count - 1);
            return null;
        }

        _subscriptions.Remove(column);
        existing.Subscription.Deactivate();
        return existing.Subscription;
    }

    private void MarkCollectionMutation()
    {
        unchecked { ++_collectionRevision; ++_layoutRevision; }
        _columnWidthsDirty = true;
        InvalidateGeometry();
    }

    protected override void InsertItem(int index, TColumn item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if ((uint)index > (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        CheckReentrancy();
        var capacity = checked(Count + 1);
        _committedConstraints.EnsureCapacity(capacity);
        if (Items is List<TColumn> storage) storage.EnsureCapacity(capacity);
        AcquireColumn(item);
        // No application callbacks between acquiring ownership and the base
        // collection's structural write. Its notifications may throw or reenter,
        // but must observe aligned storage and committed observer ownership.
        _committedConstraints.Insert(index, (double.NaN, double.NaN));
        MarkCollectionMutation();
        base.InsertItem(index, item);
    }

    protected override void SetItem(int index, TColumn item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        CheckReentrancy();
        var old = this[index];
        // Installing a replacement must succeed before the old observer retires.
        // Acquiring the same object just increments its duplicate-entry count.
        AcquireColumn(item);
        var retired = RetireColumn(old);
        _committedConstraints[index] = (double.NaN, double.NaN);
        MarkCollectionMutation();
        Exception? failure = null;
        try { base.SetItem(index, item); }
        catch (Exception error) { failure = error; throw; }
        finally { CompleteColumnCleanup(retired, failure); }
    }

    protected override void RemoveItem(int index)
    {
        if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        CheckReentrancy();
        var retired = RetireColumn(this[index]);
        _committedConstraints.RemoveAt(index);
        MarkCollectionMutation();
        Exception? failure = null;
        try { base.RemoveItem(index); }
        catch (Exception error) { failure = error; throw; }
        finally { CompleteColumnCleanup(retired, failure); }
    }

    protected override void ClearItems()
    {
        CheckReentrancy();
        var retired = new List<ColumnSubscription>(_subscriptions.Count);
        foreach (var entry in _subscriptions.Values) retired.Add(entry.Subscription);
        foreach (var subscription in retired) subscription.Deactivate();
        _subscriptions.Clear();
        _committedConstraints.Clear();
        MarkCollectionMutation();
        Exception? failure = null;
        try { base.ClearItems(); }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            if (_columnMutationBatchDepth > 0)
            {
                if (retired.Count > 0)
                    (_deferredColumnCleanup ??= new()).AddRange(retired);
            }
            else CleanupColumns(retired, failure);
        }
    }

    protected override void MoveItem(int oldIndex, int newIndex)
    {
        if ((uint)oldIndex >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(oldIndex));
        if ((uint)newIndex >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        CheckReentrancy();
        var constraints = _committedConstraints[oldIndex];
        _committedConstraints.RemoveAt(oldIndex);
        _committedConstraints.Insert(newIndex, constraints);
        MarkCollectionMutation();
        base.MoveItem(oldIndex, newIndex);
    }

    // Preserve the shared base's batching/notification rules. Retired accessors
    // run only after the outer batch unwinds, so one throwing removal cannot
    // abort RemoveRange halfway or suppress its complete collection notification.
    public override void InsertRange(int index, Action<Action<TColumn>> action)
    {
        ++_columnMutationBatchDepth;
        Exception? failure = null;
        try { base.InsertRange(index, action); }
        catch (Exception error) { failure = error; throw; }
        finally { EndColumnMutationBatch(failure); }
    }

    public override void RemoveRange(int index, int count)
    {
        ++_columnMutationBatchDepth;
        Exception? failure = null;
        try { base.RemoveRange(index, count); }
        catch (Exception error) { failure = error; throw; }
        finally { EndColumnMutationBatch(failure); }
    }

    public override void Reset(Action<IList<TColumn>> action)
    {
        ++_columnMutationBatchDepth;
        Exception? failure = null;
        try { base.Reset(action); }
        catch (Exception error) { failure = error; throw; }
        finally { EndColumnMutationBatch(failure); }
    }

    private void EndColumnMutationBatch(Exception? failure)
    {
        if (--_columnMutationBatchDepth != 0) return;
        var retired = _deferredColumnCleanup;
        _deferredColumnCleanup = null;
        // Clear the queue before callbacks: a reentrant batch owns a new queue.
        if (retired is not null) CleanupColumns(retired, failure);
    }

    private void CompleteColumnCleanup(ColumnSubscription? subscription, Exception? failure)
    {
        if (subscription is null) return;
        if (_columnMutationBatchDepth > 0)
            (_deferredColumnCleanup ??= new()).Add(subscription);
        else CleanupColumn(subscription, failure);
    }

    private static void CleanupColumn(ColumnSubscription subscription, Exception? failure)
    {
        try { subscription.Dispose(); }
        catch (Exception cleanup)
        {
            if (failure is not null) throw new AggregateException(failure, cleanup);
            throw;
        }
    }

    private static void CleanupColumns(IReadOnlyList<ColumnSubscription> subscriptions, Exception? failure)
    {
        List<Exception>? errors = null;
        for (var i = 0; i < subscriptions.Count; ++i)
        {
            try { subscriptions[i].Dispose(); }
            catch (Exception cleanup) { (errors ??= new()).Add(cleanup); }
        }
        if (errors is null) return;
        if (failure is not null) errors.Insert(0, failure);
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        throw new AggregateException(errors);
    }

    private sealed class ColumnSubscription : IDisposable
    {
        private readonly WeakReference<ColumnListBase<TColumn>> _owner;
        private readonly IColumn _column;
        private readonly PropertyChangedEventHandler _handler;
        private bool _active;
        private bool _attachmentAttempted;

        public ColumnSubscription(ColumnListBase<TColumn> owner, IColumn column)
        {
            _owner = new(owner);
            _column = column;
            _handler = Changed;
        }

        public void Attach()
        {
            _attachmentAttempted = true;
            _column.PropertyChanged += _handler;
        }

        public void Activate() => _active = true;
        public void Deactivate() => _active = false;

        private void Changed(object? sender, PropertyChangedEventArgs args)
        {
            if (!_active) return;
            if (_owner.TryGetTarget(out var owner)) owner.OnColumnPropertyChanged(sender, args);
            else Dispose();
        }

        public void Dispose()
        {
            _active = false;
            if (!_attachmentAttempted) return;
            // Retire before invoking application code, even when the publisher
            // throws without removing the handler or invokes it during removal.
            _attachmentAttempted = false;
            _column.PropertyChanged -= _handler;
        }
    }
}
