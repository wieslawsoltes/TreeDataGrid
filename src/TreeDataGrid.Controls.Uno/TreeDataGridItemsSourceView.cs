using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Uno.Controls;

/// <summary>
/// Exposes the view-compatible items-source contract using Core's collection
/// normalization, weak notifications and lifetime implementation.
/// </summary>
public class TreeDataGridItemsSourceView : TreeDataGridCore.TreeDataGridItemsSourceView
{
    public TreeDataGridItemsSourceView(IEnumerable source) : base(source) { }

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
