using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class PresentationCellOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Configuration_failure_releases_owned_new_and_popped_values(bool pooled)
    {
        using var fixture = new Fixture();
        var value = (ProbeValue)fixture.View.RealizeCell(0, 0);
        if (pooled) fixture.View.RecycleCell(fixture.Column, value);
        else fixture.Column.Next = value;
        var failure = new InvalidOperationException("configure");
        fixture.Column.Configure = _ => throw failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => fixture.View.RealizeCell(0, 0)));
        Assert.Equal(1, value.Disposals);
    }

    [Fact]
    public void Configuration_and_cleanup_errors_are_both_preserved()
    {
        using var fixture = new Fixture();
        var primary = new InvalidOperationException("configure");
        var cleanup = new InvalidOperationException("dispose");
        var value = new ProbeValue { DisposeAction = () => throw cleanup };
        fixture.Column.Next = value;
        fixture.Column.Configure = _ => throw primary;
        var failure = Assert.Throws<AggregateException>(() => fixture.View.RealizeCell(0, 0));
        Assert.Collection(failure.InnerExceptions, e => Assert.Same(primary, e), e => Assert.Same(cleanup, e));
        Assert.Equal(1, value.Disposals);
    }

    [Theory]
    [InlineData("factory")]
    [InlineData("reuse")]
    [InlineData("configure")]
    public void Disposed_presentation_never_returns_an_owned_cell(string callback)
    {
        using var fixture = new Fixture();
        var value = (ProbeValue)fixture.View.RealizeCell(0, 0);
        if (callback == "reuse")
        {
            fixture.View.RecycleCell(fixture.Column, value);
            fixture.Column.Reuse = (_, _) => { fixture.View.Dispose(); return true; };
        }
        else
        {
            fixture.Column.Next = value;
            if (callback == "factory") fixture.Column.Create = _ => fixture.View.Dispose();
            else fixture.Column.Configure = _ => fixture.View.Dispose();
        }
        Assert.Throws<ObjectDisposedException>(() => fixture.View.RealizeCell(0, 0));
        Assert.Equal(1, value.Disposals);
    }

    [Theory]
    [InlineData("suspend-resume")]
    [InlineData("row-replace")]
    [InlineData("sort")]
    [InlineData("visibility")]
    public void A_restored_identity_cannot_make_an_obsolete_configuration_current(string mutation)
    {
        using var fixture = new Fixture();
        var value = new ProbeValue();
        fixture.Column.Next = value;
        fixture.Column.Configure = _ =>
        {
            if (mutation == "suspend-resume") { fixture.View.Suspend(); fixture.View.Resume(); }
            else if (mutation == "row-replace") fixture.Items[0] = new("Replacement");
            else if (mutation == "sort") fixture.Source.SortBy(fixture.Definition, ListSortDirection.Descending);
            else { fixture.Definition.IsVisible = false; fixture.Definition.IsVisible = true; }
        };
        Assert.Throws<InvalidOperationException>(() => fixture.View.RealizeCell(0, 0));
        Assert.Equal(1, value.Disposals);
        fixture.Column.Configure = null;
        var recovered = fixture.View.RealizeCell(0, 0);
        Assert.Same(fixture.Source.Rows[0].Model, recovered.Value);
        recovered.Dispose();
    }

    [Fact]
    public void Failed_retained_reuse_leaves_disposal_with_its_caller()
    {
        using var fixture = new Fixture();
        var borrowed = (ProbeValue)fixture.View.RealizeCell(0, 0);
        fixture.Column.Configure = _ => { fixture.View.Suspend(); fixture.View.Resume(); };
        Assert.False(fixture.View.TryReuseCell(0, 1, borrowed));
        Assert.Equal(0, borrowed.Disposals);
        borrowed.Dispose();
        Assert.Equal(1, borrowed.Disposals);
    }

    [Fact]
    public void Rejected_reuse_cannot_retarget_the_fallback_to_cores_other_anonymous_row()
    {
        using var fixture = new Fixture();
        var old = (ProbeValue)fixture.View.RealizeCell(0, 0);
        fixture.View.RecycleCell(fixture.Column, old);
        fixture.Column.Reuse = (_, _) => { _ = fixture.View.Rows[1].Model; return false; };
        old.DisposeAction = () => { _ = fixture.View.Rows[1].Model; };
        var current = fixture.View.RealizeCell(0, 0);
        Assert.Same(fixture.Items[0], current.Value);
        Assert.Equal(1, old.Disposals);
        current.Dispose();
    }

    [Fact]
    public void Suspension_failure_does_not_mask_a_disposal_failure()
    {
        using var fixture = new Fixture();
        var primary = new InvalidOperationException("suspend");
        var cleanup = new InvalidOperationException("dispose");
        var value = new ProbeValue { SuspendAction = () => throw primary, DisposeAction = () => throw cleanup };
        var failure = Assert.Throws<AggregateException>(() => fixture.View.RecycleCell(fixture.Column, value));
        Assert.Collection(failure.InnerExceptions, e => Assert.Same(primary, e), e => Assert.Same(cleanup, e));
        Assert.Equal(1, value.Disposals);
    }

    [Fact]
    public void Suspension_callback_that_cycles_presentation_cannot_pool_the_old_value()
    {
        using var fixture = new Fixture();
        var value = new ProbeValue { SuspendAction = () => { fixture.View.Suspend(); fixture.View.Resume(); } };
        fixture.View.RecycleCell(fixture.Column, value);
        Assert.Equal(1, value.Disposals);
        var next = fixture.View.RealizeCell(0, 0);
        Assert.NotSame(value, next);
        next.Dispose();
    }

    [Fact]
    public void Empty_column_buckets_do_not_starve_later_columns_of_pooling()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("Item")]);
        var views = new List<ProbeColumn>();
        var options = new TreeDataGridPresentationOptions();
        options.Columns.Add("probe", definition =>
        {
            var result = new ProbeColumn(definition);
            views.Add(result);
            return result;
        });
        for (var index = 0; index < 128; ++index)
            source.Columns.Add(new TextColumn<Item, string>($"C{index}", item => item.Name) { PresentationKey = "probe" });
        using var presentation = TreeDataGridPresentation.Create(source, options);
        for (var index = 0; index < source.Columns.Count; ++index)
        {
            var first = presentation.RealizeCell(index, 0);
            presentation.RecycleCell((CellColumn)presentation.Columns[index], first);
            var second = presentation.RealizeCell(index, 0);
            Assert.Same(first, second);
            second.Dispose();
        }
    }

    [Fact]
    public void Warm_recycle_realize_cycles_reuse_stack_storage_without_allocating()
    {
        using var fixture = new Fixture();
        CellValue value = fixture.View.RealizeCell(0, 0);
        for (var i = 0; i < 1024; ++i)
        { fixture.View.RecycleCell(fixture.Column, value); value = fixture.View.RealizeCell(0, i & 1); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i)
        { fixture.View.RecycleCell(fixture.Column, value); value = fixture.View.RealizeCell(0, i & 1); }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Same(fixture.Items[1], value.Value);
        value.Dispose();
    }

    private sealed class Fixture : IDisposable
    {
        internal ObservableCollection<Item> Items { get; } = [new("A"), new("B")];
        internal FlatTreeDataGridSource<Item> Source { get; }
        internal TextColumn<Item, string> Definition { get; }
        internal ProbeColumn Column { get; }
        internal TreeDataGridPresentation View { get; }
        internal Fixture()
        {
            Source = new(Items);
            Definition = new("Name", item => item.Name) { PresentationKey = "probe" };
            Source.Columns.Add(Definition);
            Column = new(Definition);
            var options = new TreeDataGridPresentationOptions();
            options.Columns.Add("probe", _ => Column);
            View = TreeDataGridPresentation.Create(Source, options);
        }
        public void Dispose() { View.Dispose(); Source.Dispose(); }
    }
    private sealed record Item(string Name);
    private sealed class ProbeColumn(IColumn model) : CellColumn(model)
    {
        internal ProbeValue? Next;
        internal Action<IRow>? Create;
        internal Action<CellValue>? Configure;
        internal Func<CellValue, IRow, bool>? Reuse;
        public override CellValue CreateCell(IRow row)
        {
            var result = Next ?? new ProbeValue();
            Next = null;
            result.Model = row.Model;
            Create?.Invoke(row);
            return result;
        }
        internal override void ConfigureCell(CellValue value) { base.ConfigureCell(value); Configure?.Invoke(value); }
        internal override bool TryReuseCell(CellValue value, IRow row) => Reuse?.Invoke(value, row) ?? base.TryReuseCell(value, row);
    }
    private sealed class ProbeValue : CellValue
    {
        internal object? Model;
        internal Action? SuspendAction;
        internal Action? DisposeAction;
        internal int Disposals;
        public override object? Value => Model;
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new NotSupportedException();
        internal override bool TrySuspend() { Model = null; SuspendAction?.Invoke(); return true; }
        internal override bool TryRetarget(IRow row) { Model = row.Model; return true; }
        public override void Dispose() { ++Disposals; Model = null; DisposeAction?.Invoke(); }
    }
}
