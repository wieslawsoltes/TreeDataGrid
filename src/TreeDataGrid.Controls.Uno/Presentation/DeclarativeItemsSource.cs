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
internal sealed class DeclarativeItemsSource : IList<object>, IList, INotifyCollectionChanged, IDisposable
{
    private readonly IList _items;
    private NotifyCollectionChangedEventHandler? _changed;
    private Subscription? _subscription;
    private bool _disposed;

    internal DeclarativeItemsSource(IEnumerable items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items as IList ?? (items is INotifyCollectionChanged
            ? throw new ArgumentException("An observable ItemsSource must implement IList, as required by Core.", nameof(items))
            : items.Cast<object>().ToList());
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add
        {
            if (value is null) return;
            ObjectDisposedException.ThrowIf(_disposed, this);
            _changed += value;
            if (_subscription is null && _items is INotifyCollectionChanged observable)
            {
                var subscription = new Subscription(this, observable);
                _subscription = subscription;
                try { subscription.Attach(); }
                catch (Exception error)
                {
                    // A remove/re-add callback can already own a newer relay,
                    // including another registration of this same delegate.
                    // Roll back only the registration in the failed generation.
                    if (ReferenceEquals(_subscription, subscription))
                    {
                        _subscription = null;
                        _changed -= value;
                    }
                    try { subscription.Dispose(); }
                    catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                    throw;
                }
                // A synchronous event from a custom add accessor may remove the
                // last listener. Do not leave that just-attached relay alive.
                if (!ReferenceEquals(_subscription, subscription)) subscription.Dispose();
            }
        }
        remove
        {
            if (value is null) return;
            _changed -= value;
            if (_changed is null)
            {
                var subscription = _subscription;
                _subscription = null;
                subscription?.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _changed = null;
        var subscription = _subscription;
        _subscription = null;
        subscription?.Dispose();
    }

    private sealed class Subscription(DeclarativeItemsSource owner, INotifyCollectionChanged source) : IDisposable
    {
        private readonly WeakReference<DeclarativeItemsSource> _owner = new(owner);
        private INotifyCollectionChanged? _source = source;
        private bool _attaching;
        // Even a throwing event add may already have stored the handler.
        // Claim the rollback before entering application-controlled accessors.
        private bool _attachmentAttempted;

        public void Attach()
        {
            var current = _source;
            if (current is null) return;
            _attaching = true;
            _attachmentAttempted = true;
            Exception? failure = null;
            try { current.CollectionChanged += OnChanged; }
            catch (Exception error) { failure = error; }
            finally { _attaching = false; }
            // Disposal during add retires delivery immediately, but the add
            // accessor may attach afterwards. Release once after it returns.
            if (_source is null)
            {
                try { Detach(current); }
                catch (Exception cleanup)
                {
                    if (failure is not null) throw new AggregateException(failure, cleanup);
                    throw;
                }
            }
            if (failure is not null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void OnChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            if (_source is null) return;
            if (_owner.TryGetTarget(out var target) && ReferenceEquals(target._subscription, this))
            {
                // Core registers weak listeners under this projection's identity.
                // Directly forwarding the underlying event accessor sends the
                // original IList as sender and silently bypasses that registry.
                target._changed?.Invoke(target, args);
            }
            else Dispose();
        }

        public void Dispose()
        {
            var current = _source;
            _source = null;
            if (current is not null && !_attaching) Detach(current);
        }

        private void Detach(INotifyCollectionChanged source)
        {
            if (!_attachmentAttempted) return;
            _attachmentAttempted = false;
            source.CollectionChanged -= OnChanged;
        }
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
