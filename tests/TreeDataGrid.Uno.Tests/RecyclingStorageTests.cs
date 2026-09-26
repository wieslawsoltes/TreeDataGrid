using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class RecyclingStorageTests
{
    [Fact]
    public void Empty_storage_is_reused_without_sharing_live_leases()
    {
        var cache = new EmptyStackPool<object>();
        var first = cache.Rent();
        var second = cache.Rent();
        Assert.NotSame(first, second);
        cache.ReturnEmpty(first);
        Assert.Equal(1, cache.Count);
        Assert.Same(first, cache.Rent());
        Assert.Equal(0, cache.Count);
        Assert.NotSame(first, cache.Rent());
    }

    [Fact]
    public void Returns_are_bounded_and_preserve_LIFO_storage_reuse()
    {
        var cache = new EmptyStackPool<object>();
        var stacks = new Stack<object>[EmptyStackPool<object>.Capacity + 4];
        for (var index = 0; index < stacks.Length; ++index)
        {
            stacks[index] = new();
            cache.ReturnEmpty(stacks[index]);
        }
        Assert.Equal(EmptyStackPool<object>.Capacity, cache.Count);
        for (var index = EmptyStackPool<object>.Capacity - 1; index >= 0; --index)
            Assert.Same(stacks[index], cache.Rent());
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Populated_null_and_duplicate_returns_cannot_corrupt_ownership()
    {
        var cache = new EmptyStackPool<object>();
        var stack = new Stack<object>();
        stack.Push(new());
        Assert.Throws<InvalidOperationException>(() => cache.ReturnEmpty(stack));
        Assert.Equal(0, cache.Count);
        Assert.Throws<ArgumentNullException>(() => cache.ReturnEmpty(null!));
        stack.Clear();
        cache.ReturnEmpty(stack);
        Assert.Throws<InvalidOperationException>(() => cache.ReturnEmpty(stack));
        Assert.Equal(1, cache.Count);
        Assert.Same(stack, cache.Rent());
    }

    [Fact]
    public void Warm_rent_push_pop_return_cycles_allocate_no_managed_bytes()
    {
        var cache = new EmptyStackPool<object>();
        var value = new object();
        for (var index = 0; index < 1024; ++index) Cycle(ref cache, value);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 4096; ++index) Cycle(ref cache, value);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Empty_storage_does_not_keep_a_retired_model_alive()
    {
        var cache = new EmptyStackPool<object>();
        var weak = UseModel(ref cache);
        Collect(weak);
        Assert.False(weak.IsAlive);
        Assert.Empty(cache.Rent());
    }

    [Fact]
    public void Clearing_the_cache_releases_all_stack_objects()
    {
        var cache = new EmptyStackPool<object>();
        var weak = Populate(ref cache);
        cache.Clear();
        Assert.Equal(0, cache.Count);
        Collect(weak);
        Assert.False(weak.IsAlive);
        cache.ReturnEmpty(cache.Rent());
        Assert.Equal(1, cache.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Boolean_boxes_preserve_type_value_and_identity(bool value)
    {
        Assert.IsType<bool>(BooleanBoxes.Box(value));
        Assert.Equal(value, (bool)BooleanBoxes.Box(value));
        Assert.Same(BooleanBoxes.Box(value), BooleanBoxes.Box(value));
        Assert.NotSame(BooleanBoxes.Box(value), BooleanBoxes.Box(!value));
    }

    [Fact]
    public void Warm_boolean_box_selection_allocates_no_managed_bytes()
    {
        object? result = BooleanBoxes.Box(false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 4096; ++index) result = BooleanBoxes.Box((index & 1) == 0);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Same(BooleanBoxes.Box(false), result);
    }

    private static void Cycle(ref EmptyStackPool<object> cache, object value)
    {
        var stack = cache.Rent();
        stack.Push(value);
        stack.Pop();
        cache.ReturnEmpty(stack);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference UseModel(ref EmptyStackPool<object> cache)
    {
        var value = new object();
        Cycle(ref cache, value);
        return new WeakReference(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Populate(ref EmptyStackPool<object> cache)
    {
        var stack = cache.Rent();
        cache.ReturnEmpty(stack);
        return new WeakReference(stack);
    }

    private static void Collect(WeakReference weak)
    {
        for (var index = 0; index < 3 && weak.IsAlive; ++index)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
