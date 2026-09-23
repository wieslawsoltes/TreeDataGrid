using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace Uno.Controls.Presentation;

internal sealed partial class CellBinding<TModel, TValue>
{
    private void ClearOwners()
    {
        List<Exception>? failures = null;
        // One hostile event accessor must not strand the other expression
        // owners. Bookkeeping is retired before calling application code.
        for (var i = 0; i < _accessors.Length; ++i)
        {
            try { SetOwner(i, null); }
            catch (Exception error) { (failures ??= new()).Add(error); }
        }
        ThrowFailures(failures);
    }

    private void SetOwner(int index, object? owner)
    {
        var previous = index == 0 ? _rootOwner : _owners[index - 1];
        if (ReferenceEquals(previous, owner)) return;
        StoreOwner(index, null);
        if (previous is not null && !Contains(previous))
        {
            List<Exception>? failures = null;
            RemoveSubscriptions(previous, propertyAttempted: true, collectionAttempted: true, ref failures);
            ThrowFailures(failures);
        }
        var subscribe = owner is not null && !Contains(owner);
        StoreOwner(index, owner);
        if (!subscribe) return;
        var propertyAttempted = false;
        var collectionAttempted = false;
        try
        {
            // Finish this serialized subscription transaction before a queued
            // Dispose/Suspend/Retarget refresh processes its newer generation.
            if (owner is INotifyPropertyChanged property)
            {
                propertyAttempted = true;
                property.PropertyChanged += _propertyChanged;
            }
            if (owner is INotifyCollectionChanged collection)
            {
                collectionAttempted = true;
                collection.CollectionChanged += _collectionChanged;
            }
        }
        catch (Exception error)
        {
            // An add accessor can attach the handler and THEN throw. Attempt
            // removal for every attempted add, but never for an unattempted one.
            // Refresh serializes reentrancy, so this slot is still ours to retire.
            StoreOwner(index, null);
            List<Exception>? failures = new() { error };
            RemoveSubscriptions(owner!, propertyAttempted, collectionAttempted, ref failures);
            ThrowFailures(failures);
            throw;
        }
    }

    private void StoreOwner(int index, object? owner)
    {
        if (index == 0) _rootOwner = owner;
        else _owners[index - 1] = owner;
    }

    private void RemoveSubscriptions(object owner, bool propertyAttempted, bool collectionAttempted,
        ref List<Exception>? failures)
    {
        if (propertyAttempted && owner is INotifyPropertyChanged property)
        {
            try { property.PropertyChanged -= _propertyChanged; }
            catch (Exception error) { (failures ??= new()).Add(error); }
        }
        if (collectionAttempted && owner is INotifyCollectionChanged collection)
        {
            try { collection.CollectionChanged -= _collectionChanged; }
            catch (Exception error) { (failures ??= new()).Add(error); }
        }
    }

    private static void ThrowFailures(List<Exception>? failures)
    {
        if (failures is null) return;
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        throw new AggregateException(failures);
    }
}
