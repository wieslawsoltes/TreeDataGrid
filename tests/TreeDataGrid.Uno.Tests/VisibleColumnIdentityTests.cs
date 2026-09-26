using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class VisibleColumnIdentityTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(1024)]
    public void Membership_never_invokes_column_equality_or_hash_callbacks(int count)
    {
        var list = new VisibleColumnList();
        var columns = new TrapColumn[count];
        for (var index = 0; index < count; ++index) list.Add(columns[index] = new());
        var stranger = new TrapColumn();
        for (var index = 0; index < count; ++index) Assert.True(list.Contains(columns[index]));
        Assert.False(list.Contains(stranger));
        Assert.False(list.Contains(null!));
        list.Clear();
        foreach (var column in columns) Assert.False(list.Contains(column));
    }

    [Fact]
    public void Mutations_publish_membership_matching_current_storage_inside_notifications()
    {
        var list = new VisibleColumnList();
        var a = new TrapColumn(); var b = new TrapColumn(); var c = new TrapColumn(); var d = new TrapColumn();
        var universe = new[] { a, b, c, d };
        var events = new List<NotifyCollectionChangedAction>();
        list.CollectionChanged += (_, args) => { events.Add(args.Action); Verify(); };
        ((INotifyPropertyChanged)list).PropertyChanged += (_, _) => Verify();
        list.Synchronize([a, b, c]);
        Verify();
        list.Synchronize([c, a, b]);
        list.Synchronize([c, d, b]);
        list.Synchronize([b]);
        list.Synchronize([a, c, d, b]);
        list.Synchronize([d, a, b, c]);
        list.Clear();
        Verify();
        Assert.Contains(NotifyCollectionChangedAction.Move, events);
        Assert.Contains(NotifyCollectionChangedAction.Replace, events);
        Assert.Contains(NotifyCollectionChangedAction.Remove, events);
        Assert.Contains(NotifyCollectionChangedAction.Reset, events);

        void Verify()
        {
            foreach (var candidate in universe)
            {
                var present = false;
                for (var index = 0; index < list.Count; ++index) present |= ReferenceEquals(candidate, list[index]);
                Assert.Equal(present, list.Contains(candidate));
            }
        }
    }

    [Fact]
    public void Duplicate_identity_remains_present_until_its_last_occurrence_is_removed()
    {
        var column = new TrapColumn();
        var list = new VisibleColumnList { column, column };
        Assert.True(list.Contains(column));
        list.RemoveAt(0);
        Assert.True(list.Contains(column));
        list.RemoveAt(0);
        Assert.False(list.Contains(column));
    }

    [Fact]
    public void Throwing_collection_observer_does_not_leave_a_stale_membership_cache()
    {
        var a = new TrapColumn(); var b = new TrapColumn();
        var list = new VisibleColumnList { a };
        Assert.True(list.Contains(a));
        var failure = new InvalidOperationException("observer");
        NotifyCollectionChangedEventHandler handler = (_, _) =>
        {
            Assert.False(list.Contains(a));
            Assert.True(list.Contains(b));
            throw failure;
        };
        list.CollectionChanged += handler;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => list[0] = b));
        list.CollectionChanged -= handler;
        Assert.False(list.Contains(a));
        Assert.True(list.Contains(b));
        list.Clear();
        Assert.False(list.Contains(b));
    }

    [Fact]
    public void Failed_batch_invalidates_membership_for_the_actual_partial_mutation()
    {
        var a = new TrapColumn(); var b = new TrapColumn();
        var list = new VisibleColumnList { a };
        Assert.True(list.Contains(a));
        var failure = new InvalidOperationException("batch");
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => list.InsertRange(1, insert =>
        {
            insert(b);
            Assert.True(list.Contains(b));
            throw failure;
        })));
        Assert.Equal(2, list.Count);
        Assert.True(list.Contains(b));
        list.Clear();
    }

    [Fact]
    public void Existing_single_listener_reentrancy_is_not_changed_by_an_internal_cache_listener()
    {
        var a = new TrapColumn(); var b = new TrapColumn();
        var list = new VisibleColumnList();
        var entered = false;
        list.CollectionChanged += (_, _) =>
        {
            Assert.True(list.Contains(a));
            if (!entered) { entered = true; list.Add(b); }
        };
        list.Add(a);
        Assert.True(list.Contains(a));
        Assert.True(list.Contains(b));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1024)]
    public void Warm_membership_queries_allocate_no_managed_storage(int count)
    {
        var list = new VisibleColumnList();
        for (var index = 0; index < count; ++index) list.Add(new TrapColumn());
        var target = list[count - 1];
        var stranger = new TrapColumn();
        for (var i = 0; i < 1024; ++i) { list.Contains(target); list.Contains(stranger); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var found = 0;
        for (var i = 0; i < 4096; ++i)
        {
            if (list.Contains(target)) ++found;
            if (list.Contains(stranger)) ++found;
        }
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(4096, found);
        Assert.Equal(0L, bytes);
        list.Clear();
    }

    [Fact]
    public void Foreign_equal_column_cannot_admit_a_cell_to_the_presentations_pool()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var definition = new TextColumn<Item, string>("Name", item => item.Name) { PresentationKey = "trap" };
        source.Columns.Add(definition);
        var owner = new TrapColumn(definition, equal: true);
        var options = new TreeDataGridPresentationOptions();
        options.Columns.Add("trap", _ => owner);
        using var presentation = TreeDataGridPresentation.Create(source, options);
        var foreign = new TrapColumn(definition, equal: true);
        var cell = new ProbeValue();
        presentation.RecycleCell(foreign, cell);
        Assert.Equal(0, cell.Suspends);
        Assert.Equal(1, cell.Disposals);

        var owned = presentation.RealizeCell(0, 0);
        presentation.RecycleCell(owner, owned);
        var reused = presentation.RealizeCell(0, 0);
        Assert.Same(owned, reused);
        reused.Dispose();
    }

    [Fact]
    public void Invalidating_membership_does_not_root_removed_columns()
    {
        var (list, weak) = CreateRetiredColumn();
        for (var attempt = 0; attempt < 3 && weak.IsAlive; ++attempt)
        { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(weak.IsAlive);
        GC.KeepAlive(list);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (VisibleColumnList, WeakReference) CreateRetiredColumn()
    {
        var column = new TrapColumn();
        var list = new VisibleColumnList { column };
        Assert.True(list.Contains(column));
        var weak = new WeakReference(column);
        list.Clear();
        return (list, weak);
    }

    private sealed class Item { public string Name => "Item"; }
    private sealed class TrapColumn : CellColumn
    {
        private readonly bool _equal;
        internal TrapColumn(IColumn? model = null, bool equal = false)
            : base(model ?? new TextColumn<Item, string>("Name", item => item.Name)) => _equal = equal;
        public override CellValue CreateCell(IRow row) => new ProbeValue();
        public override bool Equals(object? obj) => _equal ? obj is CellColumn : throw new InvalidOperationException("Column equality is not ownership.");
        public override int GetHashCode() => throw new InvalidOperationException("Column hashing is not ownership.");
    }
    private sealed class ProbeValue : CellValue
    {
        internal int Suspends;
        internal int Disposals;
        public override object? Value => "Item";
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new NotSupportedException();
        internal override bool TrySuspend() { ++Suspends; return true; }
        internal override bool TryRetarget(IRow row) => true;
        public override void Dispose() => ++Disposals;
    }
}
