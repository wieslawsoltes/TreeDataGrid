using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
namespace TreeDataGridCore.Tests
{
    internal class TestCollection<T> : ObservableCollection<T>, INotifyCollectionChanged
    {
        public TestCollection() { }
        public TestCollection(IEnumerable<T> values) : base(values) { }
        private NotifyCollectionChangedEventHandler? _handlers;
        event NotifyCollectionChangedEventHandler? INotifyCollectionChanged.CollectionChanged
        {
            add { base.CollectionChanged += value; _handlers += value; }
            remove { base.CollectionChanged -= value; _handlers -= value; }
        }
        public Delegate[]? GetCollectionChangedSubscribers() => _handlers?.GetInvocationList();
        public void RemoveRange(int index, int count)
        {
            var removed = new T[count];
            for (var i = 0; i < count; ++i) { removed[i] = Items[index]; Items.RemoveAt(index); }
            OnCollectionChanged(new(NotifyCollectionChangedAction.Remove, (IList)removed, index));
        }
    }
}
