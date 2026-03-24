using System;
using System.Collections.Generic;

namespace Avalonia.Utilities
{
    public readonly struct Optional<T>
    {
        private readonly T? _value;

        public Optional(T? value)
        {
            _value = value;
            HasValue = true;
        }

        public bool HasValue { get; }
        public T Value => _value!;
    }

    internal static class WeakEventHandlerManager
    {
        private static readonly Dictionary<(object sender, Delegate handler), Delegate> s_collectionChangedHandlers = new();

        public static void Subscribe<TSender, TEventArgs, TOwner>(
            TSender sender,
            string eventName,
            EventHandler<TEventArgs> handler)
            where TSender : class
            where TEventArgs : EventArgs
        {
            if (sender is System.Collections.Specialized.INotifyCollectionChanged incc &&
                eventName == nameof(System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged))
            {
                System.Collections.Specialized.NotifyCollectionChangedEventHandler adapter =
                    (s, e) => handler(s, (TEventArgs)(object)e);
                s_collectionChangedHandlers[(sender, handler)] = adapter;
                incc.CollectionChanged += adapter;
                return;
            }

            throw new NotSupportedException($"Weak event subscription for '{eventName}' is not implemented.");
        }

        public static void Unsubscribe<TEventArgs, TOwner>(
            object sender,
            string eventName,
            EventHandler<TEventArgs> handler)
            where TEventArgs : EventArgs
        {
            if (sender is System.Collections.Specialized.INotifyCollectionChanged incc &&
                eventName == nameof(System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged) &&
                s_collectionChangedHandlers.Remove((sender, handler), out var adapter))
            {
                incc.CollectionChanged -= (System.Collections.Specialized.NotifyCollectionChangedEventHandler)adapter;
                return;
            }
        }
    }
}

namespace Avalonia.Reactive
{
    public static class ReactiveCompatibility
    {
    }
}
