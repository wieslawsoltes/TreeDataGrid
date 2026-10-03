using System;
using System.Collections.Generic;

namespace Uno.Controls.Presentation;

/// <summary>
/// Small view-local cache of empty stack storage. It never owns cell/model
/// lifetimes and rejects nonempty or duplicate returns. UI-thread use only.
/// </summary>
internal struct EmptyStackPool<T>
{
    internal const int Capacity = 16;
    private Stack<T>?[]? _stacks;
    public int Count { get; private set; }

    public Stack<T> Rent()
    {
        if (Count == 0) return new();
        var index = --Count;
        var result = _stacks![index]!;
        _stacks[index] = null;
        return result;
    }

    public void ReturnEmpty(Stack<T> stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        if (stack.Count != 0)
            throw new InvalidOperationException("Only empty stack storage can be cached.");
        for (var index = 0; index < Count; ++index)
            if (ReferenceEquals(_stacks![index], stack))
                throw new InvalidOperationException("The stack is already in this cache.");
        if (Count == Capacity) return;
        (_stacks ??= new Stack<T>[Capacity])[Count++] = stack;
    }

    public void Clear()
    {
        if (_stacks is not null) Array.Clear(_stacks);
        Count = 0;
    }
}
