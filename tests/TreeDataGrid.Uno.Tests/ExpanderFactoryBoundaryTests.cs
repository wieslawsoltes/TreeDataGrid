using System;
using System.ComponentModel;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class ExpanderFactoryBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Permission_getter_cannot_return_true_after_retiring_the_expander(bool write)
    {
        var row = new Row();
        var inner = new ProbeCell();
        using var cell = new ExpanderCellValue<Model>(Definition(), inner, row);
        inner.ReadPermission = cell.Dispose;
        Assert.False(write ? cell.CanWrite : cell.CanEdit);
        Assert.Equal(1, inner.Disposals);
        inner.ReadPermission = () => throw new InvalidOperationException("Retired metadata getter");
        Assert.False(cell.CanEdit);
        Assert.False(cell.CanWrite);
        Assert.Equal(0, row.Subscribers);
    }

    [Fact]
    public void Visibility_getter_cannot_publish_true_after_retirement()
    {
        var row = new Row(); var inner = new ProbeCell();
        using var cell = new ExpanderCellValue<Model>(Definition(), inner, row);
        row.ReadVisibility = cell.Dispose;
        Assert.False(cell.ShowExpander);
        Assert.Equal(1, inner.Disposals);
        row.ReadVisibility = () => throw new InvalidOperationException("Retired visibility getter");
        Assert.False(cell.ShowExpander);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_inner_configuration_releases_the_value_and_preserves_failures(bool cleanupFails)
    {
        var primary = new InvalidOperationException("configuration failed");
        var cleanup = new InvalidOperationException("inner cleanup failed");
        var inner = new ProbeCell { CleanupFailure = cleanupFails ? cleanup : null };
        var definition = Definition();
        var factory = new ProbeColumn(definition.Inner, inner) { Configure = () => throw primary };
        using var column = new ExpanderCellColumn<Model>(definition, factory);
        var error = Record.Exception(() => column.CreateCell(new Row()));
        if (cleanupFails)
        {
            var aggregate = Assert.IsType<AggregateException>(error);
            Assert.Collection(aggregate.InnerExceptions, e => Assert.Same(primary, e), e => Assert.Same(cleanup, e));
        }
        else Assert.Same(primary, error);
        Assert.Equal(1, inner.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_outer_construction_disposes_owned_inner_exactly_once(bool cleanupFails)
    {
        var primary = new InvalidOperationException("row model failed");
        var cleanup = new InvalidOperationException("inner cleanup failed");
        var inner = new ProbeCell { CleanupFailure = cleanupFails ? cleanup : null };
        var definition = Definition();
        using var column = new ExpanderCellColumn<Model>(definition, new ProbeColumn(definition.Inner, inner));
        var row = new Row { ModelFailure = primary };
        var error = Record.Exception(() => column.CreateCell(row));
        if (cleanupFails)
        {
            var aggregate = Assert.IsType<AggregateException>(error);
            Assert.Collection(aggregate.InnerExceptions, e => Assert.Same(primary, e), e => Assert.Same(cleanup, e));
        }
        else Assert.Same(primary, error);
        Assert.Equal(1, inner.Disposals);
        Assert.Equal(0, row.Subscribers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Factory_retirement_rejects_the_returning_inner_value(bool duringConfiguration)
    {
        var inner = new ProbeCell();
        var definition = Definition();
        var factory = new ProbeColumn(definition.Inner, inner);
        using var column = new ExpanderCellColumn<Model>(definition, factory);
        if (duringConfiguration) factory.Configure = column.Dispose;
        else factory.Create = column.Dispose;
        Assert.Throws<ObjectDisposedException>(() => column.CreateCell(new Row()));
        Assert.Equal(1, inner.Disposals);
        Assert.Equal(1, factory.Disposals);
        Assert.Throws<ObjectDisposedException>(() => column.CreateCell(new Row()));
        Assert.Equal(1, factory.Creates);
    }

    [Fact]
    public void Warm_permission_and_visibility_reads_allocate_no_managed_storage()
    {
        using var cell = new ExpanderCellValue<Model>(Definition(), new ProbeCell(), new Row());
        for (var i = 0; i < 1024; ++i) { _ = cell.CanEdit; _ = cell.CanWrite; _ = cell.ShowExpander; }
        var accepted = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) if (cell.CanEdit && cell.CanWrite && cell.ShowExpander) ++accepted;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(4096, accepted);
        Assert.Equal(0L, allocated);
    }

    private static HierarchicalExpanderColumn<Model> Definition() => new(
        ValueColumn<Model, string>.FromDelegate("Name", static _ => "Name"), static _ => Array.Empty<Model>());
    private sealed class Model { }
    private sealed class Row : IExpanderRow<Model>
    {
        private readonly Model _model = new();
        private PropertyChangedEventHandler? _handlers;
        internal Action? ReadVisibility;
        internal Exception? ModelFailure;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public Model Model => ModelFailure is { } error ? throw error : _model;
        object? IRow.Model => Model;
        public object? Header => null;
        public GridLength Height { get; set; } = GridLength.Auto;
        public bool IsExpanded { get; set; }
        public bool ShowExpander { get { var callback = ReadVisibility; ReadVisibility = null; callback?.Invoke(); return true; } }
        public void UpdateShowExpander(bool value) { }
        public void UpdateModelIndex(int delta) { }
        public event PropertyChangedEventHandler? PropertyChanged { add => _handlers += value; remove => _handlers -= value; }
    }
    private sealed class ProbeCell : CellValue
    {
        internal Action? ReadPermission;
        internal Exception? CleanupFailure;
        internal int Disposals;
        public override object? Value => "Value";
        public override bool CanEdit { get { var callback = ReadPermission; ReadPermission = null; callback?.Invoke(); return true; } }
        public override void Write(object? value) { }
        public override void Dispose() { ++Disposals; if (CleanupFailure is { } error) throw error; }
    }
    private sealed class ProbeColumn(IColumn definition, ProbeCell cell) : CellColumn(definition)
    {
        internal Action? Create;
        internal Action? Configure;
        internal int Disposals;
        internal int Creates;
        public override CellValue CreateCell(IRow row) { ++Creates; Create?.Invoke(); return cell; }
        internal override void ConfigureCell(CellValue value) => Configure?.Invoke();
        public override void Dispose() => ++Disposals;
    }
}
