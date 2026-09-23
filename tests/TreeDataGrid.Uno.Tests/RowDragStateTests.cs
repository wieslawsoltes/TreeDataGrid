using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TreeDataGridCore;
using Xunit;
using global::Uno.Controls.Models.TreeDataGrid;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class RowDragStateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Cancellation_during_model_resolution_rejects_the_drag(int cancelAt)
    {
        var models = new[] { new Item(), new Item() };
        using var source = new FlatTreeDataGridSource<Item>(models);
        var state = new RowDragState(source, [new(0), new(1)], models);
        var calls = 0;
        Assert.False(state.IsCurrent((actual, path) =>
        {
            Assert.Same(source, actual);
            if (calls++ == cancelAt) state.Release();
            return models[path[0]];
        }));
        Assert.Equal(cancelAt + 1, calls);
        Assert.Null(state.Source);
        Assert.Empty(state.Indexes);
        Assert.Empty(state.Models);
    }

    [Fact]
    public void Validation_uses_model_identity_not_value_equality()
    {
        var model = new EqualItem();
        using var source = new FlatTreeDataGridSource<EqualItem>([model]);
        var state = new RowDragState(source, [new(0)], [model]);
        Assert.False(state.IsCurrent((_, _) => new EqualItem()));
        Assert.True(state.IsCurrent((_, _) => model));
        Assert.False(state.IsCurrent((_, _) => null));
        state.Release();
    }

    [Fact]
    public void Release_is_idempotent_and_does_not_resolve_or_dispose_the_source()
    {
        var model = new Item();
        using var source = new FlatTreeDataGridSource<Item>([model]);
        var state = new RowDragState(source, [new(0)], [model]);
        var token = state.Token;
        state.Release();
        state.Release();
        Assert.False(state.IsCurrent(static (_, _) => throw new InvalidOperationException("Retired resolver")));
        Assert.Same(model, Assert.Single(source.Items));
        Assert.Equal(token, state.Token);
        Assert.Empty(state.Models);
    }

    [Fact]
    public void Application_lookup_failures_are_preserved()
    {
        var model = new Item();
        using var source = new FlatTreeDataGridSource<Item>([model]);
        var state = new RowDragState(source, [new(0)], [model]);
        var failure = new InvalidOperationException("Child selector failure");
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => state.IsCurrent((_, _) =>
        {
            state.Release();
            throw failure;
        })));
        Assert.Null(state.Source);
    }

    [Fact]
    public void Warm_validation_allocates_no_managed_storage()
    {
        var model = new Item();
        using var source = new FlatTreeDataGridSource<Item>([model]);
        var state = new RowDragState(source, [new(0)], [model]);
        Func<ITreeDataGridSource, IndexPath, object?> resolve = (_, _) => model;
        for (var i = 0; i < 1024; ++i) state.IsCurrent(resolve);
        var successes = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) if (state.IsCurrent(resolve)) ++successes;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(4096, successes);
        state.Release();
    }

    [Fact]
    public void Retained_released_state_does_not_keep_the_model_alive()
    {
        var (state, weak) = CreateReleased();
        for (var i = 0; i < 3 && weak.IsAlive; ++i)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        }
        Assert.False(weak.IsAlive);
        GC.KeepAlive(state);
    }

    [Fact]
    public void DragInfo_borrows_the_source_and_lazy_index_enumerable()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var indexes = new DeferredIndexes();
        var info = new DerivedInfo(source, indexes);
        Assert.Same(source, info.Source);
        Assert.Same(source, info.Model);
        Assert.Same(indexes, info.Indexes);
        Assert.Equal(0, indexes.Enumerations);
        Assert.Equal(new[] { new IndexPath(2).Append(1), new IndexPath(0), new IndexPath(0) }, info.Indexes);
        Assert.Equal(1, indexes.Enumerations);
        Assert.Equal("TreeDataGrid.Controls.Uno.RowDrag", DragInfo.DataFormat);
    }

    [Fact]
    public void Invalid_snapshot_contracts_are_rejected()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        Assert.Throws<ArgumentNullException>(() => new DragInfo(null!, Array.Empty<IndexPath>()));
        Assert.Throws<ArgumentNullException>(() => new DragInfo(source, null!));
        Assert.Throws<ArgumentNullException>(() => DragInfo.TryGet(null!, out _));
        Assert.Throws<ArgumentException>(() => new RowDragState(source, [], []));
        Assert.Throws<ArgumentException>(() => new RowDragState(source, [new(0)], []));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (RowDragState State, WeakReference Model) CreateReleased()
    {
        var model = new Item();
        using var source = new FlatTreeDataGridSource<Item>([model]);
        var state = new RowDragState(source, [new(0)], [model]);
        var weak = new WeakReference(model);
        state.Release();
        return (state, weak);
    }

    private sealed class Item { }
    private sealed class EqualItem { public override bool Equals(object? other) => other is EqualItem; public override int GetHashCode() => 0; }
    private sealed class DerivedInfo(ITreeDataGridSource source, IEnumerable<IndexPath> indexes) : DragInfo(source, indexes);
    private sealed class DeferredIndexes : IEnumerable<IndexPath>
    {
        internal int Enumerations;
        public IEnumerator<IndexPath> GetEnumerator()
        {
            ++Enumerations;
            yield return new IndexPath(2).Append(1);
            yield return new IndexPath(0);
            yield return new IndexPath(0);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
