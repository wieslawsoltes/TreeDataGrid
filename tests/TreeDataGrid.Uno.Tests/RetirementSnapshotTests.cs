using System;
using System.Collections.Generic;
using System.Reflection;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class RetirementSnapshotTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(300)]
    public void Captured_owners_survive_collection_mutation_but_not_clear(int count)
    {
        var owners = Create(count);
        var expected = new List<object>(owners.Keys);
        var buffer = new RetirementSnapshot<object>();
        var snapshot = buffer.Capture(owners);
        owners.Clear();
        owners.Add(new object(), 0);
        Assert.Equal(expected, snapshot.ToArray());
        buffer.Clear();
        Assert.All(Storage(buffer), item => Assert.Null(item));
        buffer.Clear();
        Assert.Equal(new List<object>(owners.Keys), buffer.Capture(owners).ToArray());
        buffer.Clear();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Recursive_capture_is_rejected_even_for_an_empty_snapshot(int count)
    {
        var owners = Create(count);
        var buffer = new RetirementSnapshot<object>();
        _ = buffer.Capture(owners);
        Assert.Throws<InvalidOperationException>(() => { _ = buffer.Capture(owners); });
        buffer.Clear();
        _ = buffer.Capture(owners);
        buffer.Clear();
    }

    [Fact]
    public void Callback_failure_cannot_leave_references_in_reusable_storage()
    {
        var owners = Create(32);
        var buffer = new RetirementSnapshot<object>();
        var error = new InvalidOperationException("callback");
        Assert.Same(error, Record.Exception((Action)(() =>
        {
            try
            {
                var snapshot = buffer.Capture(owners);
                Assert.Equal(32, snapshot.Length);
                owners.Clear();
                throw error;
            }
            finally { buffer.Clear(); }
        })));
        Assert.All(Storage(buffer), item => Assert.Null(item));
        owners.Add(new object(), 0);
        _ = buffer.Capture(owners);
        buffer.Clear();
        Assert.All(Storage(buffer), item => Assert.Null(item));
    }

    [Fact]
    public void Growth_and_shrink_do_not_retain_the_old_tail()
    {
        var owners = Create(300);
        var buffer = new RetirementSnapshot<object>();
        _ = buffer.Capture(owners);
        buffer.Clear();
        var storage = Storage(buffer);
        owners.Clear();
        owners.Add(new object(), 0);
        Assert.Single(buffer.Capture(owners).ToArray());
        Assert.Same(storage, Storage(buffer));
        buffer.Clear();
        Assert.All(storage, item => Assert.Null(item));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(300)]
    public void Warm_capture_and_clear_allocate_no_managed_storage(int count)
    {
        var owners = Create(count);
        var buffer = new RetirementSnapshot<object>();
        for (var i = 0; i < 1024; ++i) { _ = buffer.Capture(owners); buffer.Clear(); }
        var total = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) { total += buffer.Capture(owners).Length; buffer.Clear(); }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(count * 4096, total);
        Assert.Equal(0L, allocated);
    }

    private static Dictionary<object, int> Create(int count)
    {
        var result = new Dictionary<object, int>(count);
        for (var i = 0; i < count; ++i) result.Add(new object(), i);
        return result;
    }

    // Inspect the retained backing storage, not merely the empty logical span:
    // otherwise a clear operation that only resets Count would pass this test.
    private static object?[] Storage(RetirementSnapshot<object> buffer) =>
        (object?[])typeof(RetirementSnapshot<object>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(buffer)!;
}
