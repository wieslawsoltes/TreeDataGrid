using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
namespace TreeDataGridCore.Utils
{
    internal interface ICollectionChangedListener
    {
        void PreChanged(INotifyCollectionChanged sender, NotifyCollectionChangedEventArgs e);
        void Changed(INotifyCollectionChanged sender, NotifyCollectionChangedEventArgs e);
        void PostChanged(INotifyCollectionChanged sender, NotifyCollectionChangedEventArgs e);
    }
    internal sealed class CollectionChangedEventManager
    {
        private readonly ConditionalWeakTable<INotifyCollectionChanged, List<WeakReference<ICollectionChangedListener>>> _entries = new();
        public static CollectionChangedEventManager Instance { get; } = new();
        public void AddListener(INotifyCollectionChanged collection, ICollectionChangedListener listener)
        {
            if (!_entries.TryGetValue(collection, out var listeners))
            {
                listeners = new();
                _entries.Add(collection, listeners);
                collection.CollectionChanged += OnChanged;
            }
            listeners.Add(new WeakReference<ICollectionChangedListener>(listener));
        }
        public void RemoveListener(INotifyCollectionChanged collection, ICollectionChangedListener listener)
        {
            if (_entries.TryGetValue(collection, out var listeners))
            {
                for (var i = 0; i < listeners.Count; i++)
                    if (listeners[i].TryGetTarget(out var target) && ReferenceEquals(target, listener))
                    {
                        listeners.RemoveAt(i);
                        if (listeners.Count == 0)
                        { collection.CollectionChanged -= OnChanged; _entries.Remove(collection); }
                        return;
                    }
            }
            throw new InvalidOperationException("Collection listener is not registered.");
        }
        private void OnChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not INotifyCollectionChanged source || !_entries.TryGetValue(source, out var listeners))
                return;
            // Capture a stable notification batch: subscriptions can change during callbacks.
            var snapshot = ArrayPool<ICollectionChangedListener>.Shared.Rent(listeners.Count);
            var count = 0;
            try
            {
                for (var i = listeners.Count - 1; i >= 0; --i)
                    if (!listeners[i].TryGetTarget(out _))
                        listeners.RemoveAt(i);
                foreach (var reference in listeners)
                    if (reference.TryGetTarget(out var target))
                        snapshot[count++] = target;
                for (var i = 0; i < count; ++i)
                    snapshot[i].PreChanged(source, e);
                for (var i = 0; i < count; ++i)
                    snapshot[i].Changed(source, e);
                for (var i = 0; i < count; ++i)
                    snapshot[i].PostChanged(source, e);
            }
            finally { ArrayPool<ICollectionChangedListener>.Shared.Return(snapshot, clearArray: true); }
            if (listeners.Count == 0)
            { source.CollectionChanged -= OnChanged; _entries.Remove(source); }

        }
    }
}
