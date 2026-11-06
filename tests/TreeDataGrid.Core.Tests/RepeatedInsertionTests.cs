using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using CoreCollections = TreeDataGridCore.Models.CollectionExtensions;

namespace TreeDataGrid.Core.Tests;

public sealed class RepeatedInsertionTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 8)]
    [InlineData(2, 1)]
    [InlineData(2, 8)]
    [InlineData(4, 1)]
    [InlineData(4, 8)]
    public void Insertion_matches_List_InsertRange_at_every_boundary(int index, int count)
    {
        var actual = new List<int> { 10, 20, 30, 40 };
        var expected = new List<int>(actual);
        expected.InsertRange(index, Enumerable.Repeat(73, count));
        CoreCollections.InsertMany(actual, index, 73, count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Reference_and_null_values_preserve_exact_identity()
    {
        var left = new object();
        var right = new object();
        var repeated = new object();
        var list = new List<object?> { left, right };
        CoreCollections.InsertMany(list, 1, repeated, 11);
        Assert.Same(left, list[0]);
        Assert.Same(right, list[12]);
        for (var index = 1; index < 12; ++index) Assert.Same(repeated, list[index]);
        CoreCollections.InsertMany(list, 6, null, 9);
        Assert.Equal(22, list.Count);
        Assert.All(list.Skip(6).Take(9), value => Assert.Null(value));
        Assert.Same(repeated, list[5]);
        Assert.Same(repeated, list[15]);
        Assert.Same(right, list[^1]);
    }

    [Fact]
    public void Structs_containing_references_are_copied_without_losing_fields()
    {
        var token = new object();
        var value = new Entry("Repeated", token, long.MaxValue);
        var list = new List<Entry> { new("Left", token, 11), new("Right", token, 22) };
        CoreCollections.InsertMany(list, 1, value, 19);
        Assert.Equal("Left", list[0].Name);
        Assert.Equal("Right", list[^1].Name);
        for (var index = 1; index <= 19; ++index)
        {
            Assert.Equal(value, list[index]);
            Assert.Same(token, list[index].Token);
        }
    }

    [Theory]
    [InlineData(-1, 1, "index")]
    [InlineData(4, 1, "index")]
    [InlineData(0, -1, "count")]
    [InlineData(1, int.MaxValue, "count")]
    public void Invalid_operations_preserve_content_capacity_and_enumerator(int index, int count, string parameter)
    {
        var list = new List<int>(12) { 10, 20, 30 };
        var enumerator = list.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        var failure = Assert.Throws<ArgumentOutOfRangeException>(() => CoreCollections.InsertMany(list, index, 73, count));
        Assert.Equal(parameter, failure.ParamName);
        Assert.Equal(new[] { 10, 20, 30 }, list);
        Assert.Equal(12, list.Capacity);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(20, enumerator.Current);
    }

    [Fact]
    public void Null_list_is_rejected_before_access()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => CoreCollections.InsertMany<int>(null!, 0, 7, 1));
        Assert.Equal("list", failure.ParamName);
    }

    [Fact]
    public void Empty_insertion_is_noop_but_nonempty_insertion_invalidates_old_enumerators()
    {
        var list = new List<int> { 1, 2 };
        var enumerator = list.GetEnumerator();
        CoreCollections.InsertMany(list, 1, 9, 0);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
        CoreCollections.InsertMany(list, 1, 9, 1);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Equal(new[] { 1, 9, 2 }, list);
    }

    [Fact]
    public void Independent_lists_have_no_shared_insertion_state()
    {
        Parallel.For(0, 8, worker =>
        {
            var list = new List<int>(260) { -1, -2 };
            for (var iteration = 0; iteration < 512; ++iteration)
            {
                var count = 1 + ((iteration + worker * 31) & 255);
                var value = worker * 1000 + iteration;
                CoreCollections.InsertMany(list, 1, value, count);
                Assert.Equal(count + 2, list.Count);
                Assert.Equal(-1, list[0]);
                Assert.Equal(-2, list[^1]);
                for (var index = 1; index <= count; ++index) Assert.Equal(value, list[index]);
                list.RemoveRange(1, count);
            }
        });
    }

    [Fact]
    public void Failed_insertion_does_not_retain_a_model_in_static_storage()
    {
        var weak = FailWithTemporaryValue();
        for (var attempt = 0; attempt < 3 && weak.IsAlive; ++attempt)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(weak.IsAlive);
    }

    [Fact]
    public void Warm_capacity_sufficient_insertions_allocate_nothing()
    {
        var list = new List<int>(66) { -1, -2 };
        for (var iteration = 0; iteration < 1024; ++iteration)
        {
            CoreCollections.InsertMany(list, 1, iteration, 64);
            list.RemoveRange(1, 64);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        long sum = 0;
        for (var iteration = 0; iteration < 4096; ++iteration)
        {
            CoreCollections.InsertMany(list, 1, iteration, 64);
            sum += list[32];
            list.RemoveRange(1, 64);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(4095L * 4096 / 2, sum);
        Assert.Equal(new[] { -1, -2 }, list);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference FailWithTemporaryValue()
    {
        var value = new TemporaryModel();
        var weak = new WeakReference(value);
        var list = new List<TemporaryModel>();
        Assert.Throws<ArgumentOutOfRangeException>(() => CoreCollections.InsertMany(list, -1, value, 1));
        return weak;
    }

    private sealed class TemporaryModel { }
    private readonly record struct Entry(string Name, object Token, long Value);
}
