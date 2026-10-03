using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Uno.Controls;

/// <summary>
/// Exposes the view-compatible items-source contract using Core's collection
/// normalization, weak notifications and lifetime implementation.
/// </summary>
public class TreeDataGridItemsSourceView : TreeDataGridCore.TreeDataGridItemsSourceView
{
    public TreeDataGridItemsSourceView(IEnumerable source) : base(source) { }

    // These declarations restore the native type's portable metadata surface.
    // Core still owns the one collection, weak listener and disposal state.
    /// <summary>Gets the current size of the borrowed or normalized collection.</summary>
    public new int Count => base.Count;

    /// <summary>Gets the Core collection without copying or re-enumerating it.</summary>
    public new IList Inner => base.Inner;

    /// <summary>Gets the item at the specified model index.</summary>
    public new object? this[int index] => base[index];

    /// <summary>Gets whether the source supports stable-key lookup.</summary>
    public new bool HasKeyIndexMapping => base.HasKeyIndexMapping;

    /// <summary>Observes the same weak Core subscription through the native API.</summary>
    public new event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add => base.CollectionChanged += value;
        remove => base.CollectionChanged -= value;
    }

    /// <summary>Gets the item at the specified model index.</summary>
    public new object? GetAt(int index) => base.GetAt(index);

    /// <summary>Finds an item using the underlying collection's equality policy.</summary>
    public new int IndexOf(object? item) => base.IndexOf(item);

    /// <summary>Gets a stable item key when supported by the underlying contract.</summary>
    /// <remarks>The current Core implementation does not support key mapping.</remarks>
    public new string KeyFromIndex(int index) => base.KeyFromIndex(index);

    /// <summary>Finds a stable item key when supported by the underlying contract.</summary>
    /// <remarks>The current Core implementation does not support key mapping.</remarks>
    public new int IndexFromKey(string key) => base.IndexFromKey(key);

    /// <summary>Detaches the view without disposing the caller-owned collection.</summary>
    public new void Dispose() => base.Dispose();

    /// <summary>Publishes the original change object through Core's event storage.</summary>
    protected new void OnItemsSourceChanged(NotifyCollectionChangedEventArgs args) =>
        base.OnItemsSourceChanged(args);

    public new static TreeDataGridItemsSourceView Empty { get; } = new(Array.Empty<object>());

    public new static TreeDataGridItemsSourceView GetOrCreate(IEnumerable? items) => items switch
    {
        TreeDataGridItemsSourceView view => view,
        null => Empty,
        _ => new(items),
    };
}

/// <summary>A typed items-source view sharing the same Core-backed base contract.</summary>
public class TreeDataGridItemsSourceView<T> : TreeDataGridItemsSourceView, IReadOnlyList<T>
{
    public TreeDataGridItemsSourceView(IEnumerable<T> source) : base(source) { }

    private TreeDataGridItemsSourceView(IEnumerable source) : base(source) { }

    public new static TreeDataGridItemsSourceView<T> Empty { get; } = new(Array.Empty<T>());

    public new T this[int index] => GetAt(index)!;

    public new T? GetAt(int index) => (T?)Inner[index];

    public IEnumerator<T> GetEnumerator() => Inner.Cast<T>().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Inner.GetEnumerator();

    public new static TreeDataGridItemsSourceView<T> GetOrCreate(IEnumerable? items) => items switch
    {
        TreeDataGridItemsSourceView<T> view => view,
        null => Empty,
        _ => new(items),
    };
}
