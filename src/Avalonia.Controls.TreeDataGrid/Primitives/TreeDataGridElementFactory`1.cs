using System;
using System.Collections.Generic;

namespace Avalonia.Controls.Primitives
{
    /// <summary>
    /// An element factory with strongly typed recycle keys.
    /// </summary>
    /// <typeparam name="TKey">
    /// The immutable key used to identify compatible elements. Value types are compared and
    /// stored without boxing. Implement equality and hashing without allocating.
    /// </typeparam>
    /// <remarks>
    /// Can be assigned to <see cref="TreeDataGrid.ElementFactory"/>. Override the typed key
    /// methods instead of the string key methods inherited from <see cref="TreeDataGridElementFactory"/>.
    /// Creating a new pool or control can still allocate; warmed pool operations do not require
    /// allocating, formatting, or boxing a key.
    /// </remarks>
    public abstract class TreeDataGridElementFactory<TKey> : TreeDataGridElementFactory
        where TKey : notnull, IEquatable<TKey>
    {
        private readonly TreeDataGridElementPool<TKey> _recyclePool = new();

        public override Control GetOrCreateElement(object? data, Control parent)
        {
            return _recyclePool.TryTake(GetDataRecycleKeyCore(data), parent, out var element)
                ? element : CreateElement(data);
        }

        public override void RecycleElement(Control element)
        {
            _recyclePool.Add(GetElementRecycleKeyCore(element), element);
        }

        public override bool CanReuseElement(Control element, object? data)
        {
            return EqualityComparer<TKey>.Default.Equals(
                GetElementRecycleKeyCore(element), GetDataRecycleKeyCore(data));
        }

        /// <summary>Gets the key for a model, without converting it to a string or object.</summary>
        protected abstract TKey GetDataRecycleKeyCore(object? data);

        /// <summary>Gets the key retained by a control, including after unrealization.</summary>
        protected abstract TKey GetElementRecycleKeyCore(Control element);
    }
}
