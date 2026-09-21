using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using IRow = TreeDataGridCore.Models.IRow;

namespace Uno.Controls.Primitives;

/// <summary>Creates and identifies compatible grid containers.</summary>
public class TreeDataGridElementFactory
{
    // A shared factory must not keep a discarded grid alive. Parent-local pools
    // also avoid detaching retained controls and reapplying their native styles.
    private readonly ConditionalWeakTable<FrameworkElement, Pool> _parents = new();
    private readonly ConditionalWeakTable<Control, Entry> _entries = new();
    private readonly Pool _unparented = new();
    private readonly LinkedList<WeakReference<Control>> _fallback = new();

    /// <remarks>Uno panels are FrameworkElements, not Controls.</remarks>
    public Control GetOrCreateElement(object? data, FrameworkElement parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        var key = GetDataRecycleKey(data);
        if (_parents.TryGetValue(parent, out var pool) && Take(pool, key, parent) is { } retained)
            return retained;
        if (Take(_unparented, key, null) is { } unparented) return unparented;
        for (var node = _fallback.First; node is not null;)
        {
            var next = node.Next;
            if (!node.Value.TryGetTarget(out var candidate)) _fallback.Remove(node);
            else if (_entries.TryGetValue(candidate, out var entry) && entry.Key == key)
            {
                Remove(entry);
                if (candidate.Parent is Panel oldParent)
                {
                    oldParent.Children.Remove(candidate);
                    return candidate;
                }
                if (candidate.Parent is null) return candidate;
            }
            node = next;
        }
        return CreateElement(data);
    }

    public void RecycleElement(Control element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var key = GetElementRecycleKey(element);
        var entry = _entries.GetValue(element, static control => new(control));
        if (entry.Pool is not null) throw new InvalidOperationException("The element is already in this factory's recycle pool.");
        var pool = element.Parent is FrameworkElement parent
            ? _parents.GetValue(parent, static _ => new()) : _unparented;
        // Presenters have their own bounded pools. This pool serves direct
        // factory consumers only; no element belongs to both pools.
        if (pool.Count >= 64) return;
        if (!pool.Elements.TryGetValue(key, out var elements)) pool.Elements.Add(key, elements = new());
        elements.AddLast(entry.Node);
        ++pool.Count;
        entry.Pool = pool;
        entry.Key = key;
        if (element.Parent is null or Panel)
        {
            // Weak fallback entries preserve Avalonia's same-parent-first,
            // cross-panel fallback behavior without rooting an old visual tree.
            if (_fallback.Count == 64) _fallback.RemoveFirst();
            _fallback.AddLast(entry.Fallback);
        }
    }

    public bool CanReuseElement(Control element, object? data)
        => GetElementRecycleKey(element) == GetDataRecycleKey(data);

    protected virtual Control CreateElement(object? data) => data switch
    {
        IExpanderCellPresentation => new TreeDataGridExpanderCell(),
        CheckBoxCell => new TreeDataGridCheckBoxCell(),
        TemplateCell => new TreeDataGridTemplateCell(),
        CellValue { Kind: CellKind.CheckBox } => new TreeDataGridCheckBoxCell(),
        CellValue { Kind: CellKind.Template } => new TreeDataGridTemplateCell(),
        ICell => new TreeDataGridTextCell(),
        IColumn => new TreeDataGridColumnHeader(),
        IRow => new TreeDataGridRow(),
        _ => throw new NotSupportedException("Unsupported TreeDataGrid element data."),
    };

    protected virtual string GetDataRecycleKey(object? data) => data switch
    {
        IExpanderCellPresentation => typeof(TreeDataGridExpanderCell).FullName!,
        CheckBoxCell => typeof(TreeDataGridCheckBoxCell).FullName!,
        TemplateCell => typeof(TreeDataGridTemplateCell).FullName!,
        CellValue { Kind: CellKind.CheckBox } => typeof(TreeDataGridCheckBoxCell).FullName!,
        CellValue { Kind: CellKind.Template } => typeof(TreeDataGridTemplateCell).FullName!,
        ICell => typeof(TreeDataGridTextCell).FullName!,
        IColumn => typeof(TreeDataGridColumnHeader).FullName!,
        IRow => typeof(TreeDataGridRow).FullName!,
        _ => throw new NotSupportedException("Unsupported TreeDataGrid element data."),
    };

    protected virtual string GetElementRecycleKey(Control element) => element.GetType().FullName!;

    private Control? Take(Pool pool, string key, FrameworkElement? parent)
    {
        if (!pool.Elements.TryGetValue(key, out var elements)) return null;
        while (elements.First is { } node)
        {
            var element = node.Value;
            Remove(_entries.GetValue(element, static control => new(control)));
            // A direct consumer may have moved an element since recycling it.
            // Never steal it from its new parent.
            if (ReferenceEquals(element.Parent, parent)) return element;
        }
        return null;
    }

    private void Remove(Entry entry)
    {
        if (entry.Pool is { } pool && entry.Node.List is { } elements)
        {
            elements.Remove(entry.Node);
            if (elements.Count == 0) pool.Elements.Remove(entry.Key!);
            --pool.Count;
        }
        if (entry.Fallback.List is not null) _fallback.Remove(entry.Fallback);
        entry.Pool = null;
        entry.Key = null;
    }

    private sealed class Entry(Control control)
    {
        public readonly LinkedListNode<Control> Node = new(control);
        public readonly LinkedListNode<WeakReference<Control>> Fallback = new(new WeakReference<Control>(control));
        public Pool? Pool;
        public string? Key;
    }
    private sealed class Pool
    {
        public readonly Dictionary<string, LinkedList<Control>> Elements = new(StringComparer.Ordinal);
        public int Count;
    }
}
