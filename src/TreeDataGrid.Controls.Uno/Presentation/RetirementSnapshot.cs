using System;
using System.Collections.Generic;

namespace Uno.Controls.Presentation;

/// <summary>Reusable callback-isolated ownership snapshot; clear before the owner leaves retirement.</summary>
internal struct RetirementSnapshot<T> where T : class
{
    private T[]? _items;
    private int _count;
    private bool _active;

    internal ReadOnlySpan<T> Capture<TValue>(Dictionary<T, TValue> owners)
    {
        ArgumentNullException.ThrowIfNull(owners);
        if (_active) throw new InvalidOperationException("A retirement snapshot is already active.");
        var count = owners.Count;
        // Allocation and copying precede publication. Neither operation invokes
        // application code. Keep an earlier reusable buffer if allocation fails.
        if (_items is null || _items.Length < count)
            _items = new T[Math.Max(count, 4)];
        owners.Keys.CopyTo(_items, 0);
        _count = count;
        _active = true;
        return _items.AsSpan(0, count);
    }

    internal void Clear()
    {
        if (!_active) return;
        // Retain capacity, never retired controls. Clear even after a throwing
        // lifecycle callback; a smaller next viewport must not retain the tail.
        Array.Clear(_items!, 0, _count);
        _count = 0;
        _active = false;
    }
}
