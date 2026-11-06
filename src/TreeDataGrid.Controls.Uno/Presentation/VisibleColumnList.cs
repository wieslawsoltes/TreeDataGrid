using System;
using System.Collections.Generic;
using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Presentation;

/// <summary>Publishes a single, identity-preserving projection mutation.</summary>
internal sealed class VisibleColumnList : ColumnListBase<CellColumn>
{
    private HashSet<CellColumn>? _membership;
    private int _mutationDepth;

    // Ownership is reference identity, not a custom column's Equals contract.
    // The steady realization/recycling path performs one hash lookup, rather
    // than scanning every visible definition for each cell. Do not alter the
    // public Collection<T>.Contains contract of caller-owned column lists.
    public new bool Contains(CellColumn column)
    {
        if (_mutationDepth != 0)
        {
            // Native collection/property callbacks can query the projection
            // while its storage is changing. Never cache an intermediate state
            // or register an extra listener that changes reentrancy policy.
            for (var index = 0; index < Count; ++index)
                if (ReferenceEquals(this[index], column)) return true;
            return false;
        }
        if (_membership is null)
        {
            var membership = new HashSet<CellColumn>(Count, ReferenceEqualityComparer.Instance);
            for (var index = 0; index < Count; ++index) membership.Add(this[index]);
            _membership = membership;
        }
        return _membership.Contains(column);
    }

    protected override void InsertItem(int index, CellColumn item)
    {
        BeginMutation();
        try { base.InsertItem(index, item); }
        finally { EndMutation(); }
    }
    protected override void RemoveItem(int index)
    {
        BeginMutation();
        try { base.RemoveItem(index); }
        finally { EndMutation(); }
    }
    protected override void SetItem(int index, CellColumn item)
    {
        BeginMutation();
        try { base.SetItem(index, item); }
        finally { EndMutation(); }
    }
    protected override void MoveItem(int oldIndex, int newIndex)
    {
        BeginMutation();
        try { base.MoveItem(oldIndex, newIndex); }
        finally { EndMutation(); }
    }
    protected override void ClearItems()
    {
        BeginMutation();
        try { base.ClearItems(); }
        finally { EndMutation(); }
    }
    private void BeginMutation() { _membership = null; ++_mutationDepth; }
    private void EndMutation() { _membership = null; --_mutationDepth; }

    internal void Synchronize(IReadOnlyList<CellColumn> next)
    {
        var common = Math.Min(Count, next.Count);
        var first = 0;
        while (first < common && ReferenceEquals(this[first], next[first])) ++first;
        if (first == Count && first == next.Count) return;
        var suffix = 0;
        while (suffix < common - first && ReferenceEquals(this[Count - suffix - 1], next[next.Count - suffix - 1])) ++suffix;
        var removed = Count - first - suffix;
        var added = next.Count - first - suffix;
        if (removed == 0)
        {
            InsertRange(first, insert =>
            {
                for (var i = first; i < first + added; ++i) insert(next[i]);
            });
        }
        else if (added == 0)
            RemoveRange(first, removed);
        else if (removed == 1 && added == 1)
            this[first] = next[first];
        else if (removed == added && TryGetMove(next, first, removed, out var from, out var to))
            MoveItem(from, to);
        else
        {
            // General batch replacement is one atomic Reset. Never expose an
            // intermediate permutation or partially committed selection mapping.
            Reset(items =>
            {
                items.Clear();
                foreach (var item in next) items.Add(item);
            });
        }
    }

    private bool TryGetMove(IReadOnlyList<CellColumn> next, int first, int count, out int from, out int to)
    {
        var last = first + count - 1;
        if (ReferenceEquals(this[first], next[last]))
        {
            var matches = true;
            for (var i = first; i < last && matches; ++i)
                matches = ReferenceEquals(this[i + 1], next[i]);
            if (matches) { from = first; to = last; return true; }
        }
        if (ReferenceEquals(this[last], next[first]))
        {
            var matches = true;
            for (var i = first; i < last && matches; ++i)
                matches = ReferenceEquals(this[i], next[i + 1]);
            if (matches) { from = last; to = first; return true; }
        }
        from = to = -1;
        return false;
    }
}
