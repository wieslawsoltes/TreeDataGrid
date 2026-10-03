using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Selection;
using Xunit;
using global::Uno.Controls;
using global::Uno.Controls.Presentation;
using Native = global::Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class PresentationSelectionHookTests
{
    [Fact]
    public void Row_hook_captures_paths_and_items_without_copying_models()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        using var view = new Probe(source);
        var model = new Item();
        var paths = new[] { new IndexPath(2).Append(3) };
        object?[] items = [model];
        TreeDataGridSelectionChangedEventArgs? observed = null;
        view.NativeSelectionChanged += (sender, args) =>
        {
            Assert.Same(view, sender);
            observed = args;
            paths[0] = new(99);
            items[0] = null;
        };
        view.Publish(new TreeDataGridSelectionChangedEventArgs(selectedIndexes: paths, selectedItems: items));
        Assert.NotNull(observed);
        Assert.Equal(new IndexPath(2).Append(3), Assert.Single(observed.SelectedIndexes));
        Assert.Same(model, Assert.Single(observed.SelectedItems));
        Assert.Empty(observed.DeselectedIndexes);
        Assert.Empty(observed.SelectedCellIndexes);
    }

    [Fact]
    public void Cell_hook_preserves_order_duplicates_and_does_not_invent_row_deltas()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        using var view = new Probe(source);
        CellIndex[] previous = [new(2, new(3))];
        CellIndex[] next = [new(4, new(5)), new(4, new(5)), new(1, new(2))];
        TreeDataGridSelectionChangedEventArgs? observed = null;
        view.NativeSelectionChanged += (_, args) => observed = args;
        view.PublishCells(previous, next);
        Assert.NotNull(observed);
        Assert.Equal(previous, observed.DeselectedCellIndexes);
        Assert.Equal(next, observed.SelectedCellIndexes);
        Assert.Empty(observed.SelectedIndexes);
        Assert.Empty(observed.SelectedItems);
        next[0] = default;
        Assert.Equal(new CellIndex(4, new(5)), observed.SelectedCellIndexes[0]);
    }

    [Fact]
    public void Unobserved_hooks_do_not_evaluate_lazy_arguments()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        using var view = new Probe(source);
        var lazy = new LazyCells(() => throw new InvalidOperationException("Unobserved enumeration"));
        view.PublishCells(lazy, lazy);
        Assert.Equal(0, lazy.Enumerations);
        for (var index = 0; index < 1024; ++index) view.PublishCells(lazy, lazy);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 4096; ++index) view.PublishCells(lazy, lazy);
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, lazy.Enumerations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retirement_during_argument_capture_prevents_publication(bool dispose)
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        using var view = new Probe(source);
        var observed = 0;
        view.NativeSelectionChanged += (_, _) => ++observed;
        var lazy = new LazyCells(() => { if (dispose) view.Dispose(); else view.Suspend(); });
        view.PublishCells(lazy, Array.Empty<CellIndex>());
        Assert.Equal(0, observed);
        Assert.Equal(1, lazy.Enumerations);
        if (!dispose)
        {
            view.Resume();
            view.PublishCells(Array.Empty<CellIndex>(), Array.Empty<CellIndex>());
            Assert.Equal(1, observed);
        }
    }

    [Fact]
    public void Nested_newer_delta_supersedes_the_inflight_capture()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        using var view = new Probe(source);
        var observed = new List<TreeDataGridSelectionChangedEventArgs>();
        view.NativeSelectionChanged += (_, args) => observed.Add(args);
        CellIndex[] newer = [new(7, new(8))];
        var lazy = new LazyCells(() => view.PublishCells(Array.Empty<CellIndex>(), newer));
        view.PublishCells(lazy, [new(1, new(1))]);
        Assert.Equal(newer, Assert.Single(observed).SelectedCellIndexes);
    }

    [Fact]
    public void Removing_the_last_observer_restores_lazy_suppression()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        using var view = new Probe(source);
        var count = 0;
        EventHandler<TreeDataGridSelectionChangedEventArgs> handler = (_, _) => ++count;
        var lazy = new LazyCells(static () => { });
        view.NativeSelectionChanged += handler;
        view.PublishCells(lazy, Array.Empty<CellIndex>());
        view.NativeSelectionChanged -= handler;
        view.PublishCells(lazy, lazy);
        Assert.Equal(1, count);
        Assert.Equal(1, lazy.Enumerations);
    }

    [Fact]
    public void Argument_and_listener_failures_propagate_without_poisoning_later_calls()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        using var view = new Probe(source);
        var failure = new InvalidOperationException("Application failure");
        EventHandler<TreeDataGridSelectionChangedEventArgs> handler = (_, _) => throw failure;
        view.NativeSelectionChanged += handler;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => view.PublishCells([], [])));
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => view.PublishCells(new LazyCells(() => throw failure), [])));
        view.NativeSelectionChanged -= handler;
        var observed = 0;
        view.NativeSelectionChanged += (_, _) => ++observed;
        view.PublishCells([], []);
        Assert.Equal(1, observed);
    }

    [Fact]
    public void Builtin_core_notifications_are_not_duplicated_by_custom_hooks()
    {
        using var source = new FlatTreeDataGridSource<Item>([new(), new()]);
        using var view = new Probe(source);
        var native = 0;
        var standard = 0;
        view.NativeSelectionChanged += (_, _) => ++native;
        view.Selection.SelectionChanged += (_, _) => ++standard;
        source.RowSelection!.SelectedIndex = new(1);
        Assert.Equal(1, standard);
        Assert.Equal(0, native);
    }

    private sealed class Item { }
    private sealed class LazyCells(Action before) : IEnumerable<CellIndex>
    {
        public int Enumerations;
        public IEnumerator<CellIndex> GetEnumerator()
        {
            ++Enumerations;
            before();
            yield return new(0, new(0));
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class Probe : TreeDataGridPresentation
    {
        private readonly TreeDataGridPresentation _inner;
        public Probe(ITreeDataGridSource source) => _inner = Create(source);
        public void Publish(TreeSelectionModelSelectionChangedEventArgs args) => RaiseNativeSelectionChanged(args);
        public void PublishCells(IEnumerable<CellIndex> previous, IEnumerable<CellIndex> next) => RaiseNativeCellSelectionChanged(previous, next);
        public override ITreeDataGridSource Model => _inner.Model;
        public override Native.IColumns Columns => _inner.Columns;
        public override TreeDataGridSelection Selection => _inner.Selection;
        public override CellValue RealizeCell(int columnIndex, int rowIndex) => _inner.RealizeCell(columnIndex, rowIndex);
        public override void RecycleCell(CellColumn column, CellValue cell) => _inner.RecycleCell(column, cell);
        public override event EventHandler? ColumnsChanged { add => _inner.ColumnsChanged += value; remove => _inner.ColumnsChanged -= value; }
        public override event NotifyCollectionChangedEventHandler? RowsChanged { add => _inner.RowsChanged += value; remove => _inner.RowsChanged -= value; }
        public override void Suspend() { base.Suspend(); _inner.Suspend(); }
        public override void Resume() { base.Resume(); _inner.Resume(); }
        public override void Dispose() { base.Dispose(); _inner.Dispose(); }
    }
}
