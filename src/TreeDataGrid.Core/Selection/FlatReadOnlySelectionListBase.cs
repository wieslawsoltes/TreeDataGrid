// Adapted from Avalonia 12.0.0 (MIT). Copyright (c) .NET Foundation and Contributors.
// See build/uno-parity-inputs/upstream and docs/uno-contract-materialization.json.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace TreeDataGridCore.Selection;

internal abstract class FlatReadOnlySelectionListBase<T> : IReadOnlyList<T?>, IList, INotifyCollectionChanged
{
    private static readonly NotifyCollectionChangedEventArgs s_reset = new(NotifyCollectionChangedAction.Reset);
    public abstract T? this[int index] { get; }
    public abstract int Count { get; }

    object? IList.this[int index] 
    { 
        get => this[index];
        set => ThrowReadOnlyException();
    }

    bool IList.IsFixedSize => false;
    bool IList.IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public abstract IEnumerator<T?> GetEnumerator();
    public void RaiseCollectionReset() => CollectionChanged?.Invoke(this, s_reset);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    int IList.Add(object? value) { ThrowReadOnlyException(); return 0; }
    void IList.Clear() => ThrowReadOnlyException();
    void IList.Insert(int index, object? value) => ThrowReadOnlyException();
    void IList.Remove(object? value) => ThrowReadOnlyException();
    void IList.RemoveAt(int index) => ThrowReadOnlyException();
    bool IList.Contains(object? value) => Count != 0 && ((IList)this).IndexOf(value) != -1;

    void ICollection.CopyTo(Array array, int index)
    {
        foreach (var item in this)
            array.SetValue(item, index++);
    }

    int IList.IndexOf(object? value)
    {
        for (int i = 0; i < Count; i++)
        {
            if (Equals(this[i], value))
                return i;
        }

        return -1;
    }

    [DoesNotReturn]
    private static void ThrowReadOnlyException() => throw new NotSupportedException("Collection is read-only.");
}
