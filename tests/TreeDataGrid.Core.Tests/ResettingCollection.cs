using System.Collections.Generic;
using System.Collections.Specialized;

namespace TreeDataGridCore.Tests.Collections
{
    internal class ResettingCollection<T> : List<T>, INotifyCollectionChanged
    {
        public ResettingCollection(IEnumerable<T> items)
        {
            AddRange(items);
        }

        public new void RemoveAt(int index)
        {
            var item = this[index];
            base.RemoveAt(index);
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove,
                    item,
                    index));
        }

        public void Reset(IEnumerable<T> items)
        {
            Clear();
            AddRange(items);
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void AddWithoutIndex(T item)
        {
            Add(item);
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    item));
        }

        public void RemoveAtWithoutIndex(int index)
        {
            var item = this[index];
            base.RemoveAt(index);
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove,
                    item));
        }

        public void ReplaceWithoutIndex(int index, T item)
        {
            var oldItem = this[index];
            this[index] = item;
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Replace,
                    item,
                    oldItem));
        }

        public void ReplaceRange(int index, int count, IEnumerable<T> items)
        {
            var oldItems = GetRange(index, count);
            var newItems = new List<T>(items);
            RemoveRange(index, count);
            InsertRange(index, newItems);
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Replace,
                    newItems,
                    oldItems,
                    index));
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;
    }
}
