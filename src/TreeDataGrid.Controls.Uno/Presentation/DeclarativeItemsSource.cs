using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Uno.Controls.Presentation;

/// <summary>
/// A type-erasure boundary, not a second collection model. Preserve the caller's
/// IList, mutations, notifications and model identity for the actual Core source.
/// </summary>
internal sealed class DeclarativeItemsSource : IList<object>, IList, INotifyCollectionChanged
{
    private readonly IList _items;
    internal DeclarativeItemsSource(IEnumerable items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items as IList ?? (items is INotifyCollectionChanged
            ? throw new ArgumentException("An observable ItemsSource must implement IList, as required by Core.", nameof(items))
            : items.Cast<object>().ToList());
    }
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { if (_items is INotifyCollectionChanged observable) observable.CollectionChanged += value; }
        remove { if (_items is INotifyCollectionChanged observable) observable.CollectionChanged -= value; }
    }
    public object this[int index] { get => _items[index]!; set => _items[index] = value; }
    object? IList.this[int index] { get => _items[index]; set => _items[index] = value; }
    public int Count => _items.Count;
    public bool IsReadOnly => _items.IsReadOnly || _items.IsFixedSize;
    public void Add(object item) => _items.Add(item);
    int IList.Add(object? value) => _items.Add(value);
    public void Clear() => _items.Clear();
    public bool Contains(object item) => _items.Contains(item);
    bool IList.Contains(object? value) => _items.Contains(value);
    public int IndexOf(object item) => _items.IndexOf(item);
    int IList.IndexOf(object? value) => _items.IndexOf(value);
    public void Insert(int index, object item) => _items.Insert(index, item);
    void IList.Insert(int index, object? value) => _items.Insert(index, value);
    public bool Remove(object item)
    {
        var index = _items.IndexOf(item);
        if (index < 0) return false;
        _items.RemoveAt(index);
        return true;
    }
    void IList.Remove(object? value) => _items.Remove(value);
    public void RemoveAt(int index) => _items.RemoveAt(index);
    public void CopyTo(object[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    void ICollection.CopyTo(Array array, int index) => _items.CopyTo(array, index);
    bool IList.IsFixedSize => _items.IsFixedSize;
    bool ICollection.IsSynchronized => _items.IsSynchronized;
    object ICollection.SyncRoot => _items.SyncRoot;
    public IEnumerator<object> GetEnumerator() => _items.Cast<object>().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
