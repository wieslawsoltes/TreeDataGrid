using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Xunit;
using Core = TreeDataGridCore;
using P = Uno.Controls.Presentation;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class TypedColumnProjectionTests
{
    [Fact]
    public void Legacy_only_entries_keep_the_original_mutable_layout_contract()
    {
        var original = new LegacyOnly();
        var columns = new U.ColumnList<Item> { original };
        IReadOnlyList<U.IColumn<Item>> typed = columns;
        var projected = typed[0];
        Assert.Same(original, columns[0]);
        Assert.Same(original, ((U.IColumns)columns)[0]);
        Assert.Same(projected, typed[0]);
        Assert.Equal(original.Header, projected.Header);
        Assert.Equal(original.Width, projected.Width);
        Assert.Equal(original.ActualWidth, projected.ActualWidth);
        Assert.Equal(original.CanUserResize, projected.CanUserResize);
        Assert.Null(projected.GetComparison(ListSortDirection.Ascending));
        Assert.Null(projected.GetComparison((ListSortDirection)47));
        var row = new Row(new());
        using var cell = Assert.IsAssignableFrom<IDisposable>(projected.CreateCell(row));
        Assert.Same(row, original.LastRow);
        Assert.Equal(1, original.Creates);
        var tag = new object(); projected.Tag = tag;
        projected.SortDirection = ListSortDirection.Descending;
        Assert.Same(tag, original.Tag);
        Assert.Equal(ListSortDirection.Descending, original.SortDirection);
        columns.Clear();
        Assert.Equal(0, original.Disposals);
        Assert.Equal(0, original.Subscribers);
    }

    [Fact]
    public void Typed_projection_preserves_event_sender_and_exact_handler_removal()
    {
        var original = new LegacyOnly();
        var columns = new U.ColumnList<Item> { original };
        var typed = ((IReadOnlyList<U.IColumn<Item>>)columns)[0];
        var events = 0;
        PropertyChangedEventHandler handler = (sender, args) =>
        {
            Assert.Same(original, sender);
            Assert.Equal("Header", args.PropertyName);
            ++events;
        };
        Assert.Equal(1, original.Adds);
        typed.PropertyChanged += handler;
        Assert.Same(handler, original.LastAdded);
        Assert.Equal(2, original.Subscribers);
        original.Notify(); Assert.Equal(1, events);
        typed.PropertyChanged -= handler;
        Assert.Same(handler, original.LastRemoved);
        original.Notify(); Assert.Equal(1, events);
        columns.Clear();
        Assert.Equal(2, original.Adds); Assert.Equal(2, original.Removes);
        Assert.Equal(0, original.Subscribers); Assert.Equal(0, original.Disposals);
    }

    [Fact]
    public void Duplicate_legacy_entries_share_one_facade_without_owning_the_column()
    {
        var original = new LegacyOnly();
        var columns = new U.ColumnList<Item> { original, original };
        IReadOnlyList<U.IColumn<Item>> typed = columns;
        var projection = typed[0];
        Assert.Same(projection, typed[1]);
        Assert.Equal(1, original.Subscribers);
        columns.RemoveAt(0);
        Assert.Same(projection, typed[0]); Assert.Equal(1, original.Subscribers);
        columns.Clear(); Assert.Equal(0, original.Subscribers);
        columns.Add(original);
        Assert.Same(projection, typed[0]); Assert.Equal(1, original.Subscribers);
        columns.Clear(); Assert.Equal(0, original.Disposals);
    }

    [Fact]
    public void Typed_enumeration_detects_mutation_like_the_original_list()
    {
        var first = new LegacyOnly(); var second = new LegacyOnly();
        var columns = new U.ColumnList<Item> { first };
        IReadOnlyList<U.IColumn<Item>> typed = columns;
        using var iterator = typed.GetEnumerator();
        Assert.True(iterator.MoveNext()); Assert.Same(typed[0], iterator.Current);
        columns.Add(second);
        Assert.Throws<InvalidOperationException>(() => iterator.MoveNext());
        columns.Clear();
    }

    [Fact]
    public void Legacy_factory_exception_crosses_the_facade_unchanged()
    {
        var failure = new InvalidOperationException("legacy factory");
        var original = new LegacyOnly { FactoryFailure = failure };
        var columns = new U.ColumnList<Item> { original };
        var typed = ((IReadOnlyList<U.IColumn<Item>>)columns)[0];
        Assert.Same(failure, Record.Exception(() => typed.CreateCell(new Row(new()))));
        Assert.Equal(1, original.Creates); Assert.Equal(0, original.Disposals);
        columns.Clear();
    }

    [Fact]
    public void Removed_legacy_column_and_its_facade_are_not_rooted_by_the_cache()
    {
        var columns = new U.ColumnList<Item>();
        var references = AddAndRemove(columns);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(references.Column.IsAlive);
        Assert.False(references.Facade.IsAlive);
        GC.KeepAlive(columns);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Column, WeakReference Facade) AddAndRemove(U.ColumnList<Item> columns)
    {
        var original = new LegacyOnly(); columns.Add(original);
        var facade = ((IReadOnlyList<U.IColumn<Item>>)columns)[0];
        var result = (new WeakReference(original), new WeakReference(facade));
        columns.Clear();
        return result;
    }

    [Fact]
    public void Repeated_typed_indexing_uses_the_same_facade_without_allocations()
    {
        var columns = new U.ColumnList<Item> { new LegacyOnly() };
        IReadOnlyList<U.IColumn<Item>> typed = columns;
        var expected = typed[0];
        for (var i = 0; i < 1024; ++i) _ = typed[0];
        var same = true; var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) same &= ReferenceEquals(expected, typed[0]);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(same); Assert.Equal(0L, allocated);
        columns.Clear();
    }

    [Fact]
    public void Native_subclass_explicit_factory_is_used_by_both_interface_routes()
    {
        using var column = new ExplicitNative();
        var row = new Row(new());
        using var first = Assert.IsAssignableFrom<IDisposable>(((U.IColumn<Item>)column).CreateCell(row));
        using var second = Assert.IsAssignableFrom<IDisposable>(((P.ICellColumn<Item>)column).CreateCell(row));
        Assert.Equal(2, column.Creates); Assert.Same(row, column.LastRow);
    }

    [Fact]
    public void Compatibility_subclass_explicit_factory_is_used_by_both_interface_routes()
    {
        var column = new ExplicitCompatibility();
        var row = new Row(new());
        using var first = Assert.IsAssignableFrom<IDisposable>(((U.IColumn<Item>)column).CreateCell(row));
        using var second = Assert.IsAssignableFrom<IDisposable>(((P.ICellColumn<Item>)column).CreateCell(row));
        Assert.Equal(2, column.Creates); Assert.Same(row, column.LastRow);
        Assert.Same(column.Comparison, ((P.ICellColumn<Item>)column).GetComparison(ListSortDirection.Ascending));
        Assert.Same(column.Comparison, ((U.IColumn<Item>)column).GetComparison(ListSortDirection.Ascending));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(80, 100, 100)]
    [InlineData(80, 30, 80)]
    public void Configured_pixel_estimate_does_not_replace_committed_constraints(double configured, double minimum, double committed)
    {
        using var column = new U.TextColumn<Item, string?>("Name", x => x.Name, width: new GridLength(configured),
            options: new() { MinWidth = new GridLength(minimum) });
        var columns = new U.ColumnList<Item> { column };
        Assert.Equal(configured, column.ActualWidth);
        Assert.Equal(configured, columns.GetEstimatedWidth(500));
        Assert.False(column.HasWidthMeasurement);
        column.CommitActualWidth();
        Assert.Equal(committed, column.ActualWidth);
        Assert.Equal(committed, columns.GetEstimatedWidth(500));
        Assert.False(column.HasWidthMeasurement);
        columns.Clear();
    }

    private sealed class Item { public string? Name { get; set; } = "Item"; }
    private sealed class Row(Item model) : Core.Models.IRow<Item>
    {
        public Item Model => model;
        object? Core.Models.IRow.Model => Model;
        public object? Header => null;
        public Core.GridLength Height { get; set; } = Core.GridLength.Auto;
        public void UpdateModelIndex(int delta) { }
    }
    private sealed class Cell : P.CellValue
    {
        public override object? Value => "explicit";
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new NotSupportedException();
    }
    private sealed class ExplicitNative() : U.TextColumn<Item, string?>("Name", x => x.Name), P.ICellColumn<Item>
    {
        internal int Creates;
        internal Core.Models.IRow? LastRow;
        public override P.CellValue CreateCell(Core.Models.IRow row) => throw new InvalidOperationException("Public factory must not run.");
        U.ICell P.ICellColumn<Item>.CreateCell(Core.Models.IRow<Item> row)
        { ++Creates; LastRow = row; return new Cell(); }
    }
    private sealed class ExplicitCompatibility() : U.ColumnBase<Item>("Name", new GridLength(80), new()), P.ICellColumn<Item>
    {
        internal int Creates;
        internal Core.Models.IRow? LastRow;
        internal readonly Comparison<Item?> Comparison = static (_, _) => 1;
        public override Comparison<Item?>? GetComparison(ListSortDirection direction) => Comparison;
        public override U.ICell CreateCell(Core.Models.IRow<Item> row) => throw new InvalidOperationException("Public factory must not run.");
        U.ICell P.ICellColumn<Item>.CreateCell(Core.Models.IRow<Item> row)
        { ++Creates; LastRow = row; return new Cell(); }
    }
    private sealed class LegacyOnly : P.ICellColumn<Item>, IDisposable
    {
        private PropertyChangedEventHandler? _handlers;
        internal int Creates, Disposals, Adds, Removes;
        internal Exception? FactoryFailure;
        internal Core.Models.IRow? LastRow;
        internal PropertyChangedEventHandler? LastAdded, LastRemoved;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public double ActualWidth => 80;
        public bool? CanUserResize => true;
        public object? Header => "Legacy";
        public GridLength Width { get; private set; } = new(80);
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public double CellMeasured(double width, int rowIndex) => width;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(GridLength width) => Width = width;
        U.ICell P.ICellColumn<Item>.CreateCell(Core.Models.IRow<Item> row)
        {
            ++Creates; LastRow = row;
            if (FactoryFailure is { } error) throw error;
            return new Cell();
        }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { ++Adds; LastAdded = value; _handlers += value; }
            remove { ++Removes; LastRemoved = value; _handlers -= value; }
        }
        internal void Notify() => _handlers?.Invoke(this, new("Header"));
        public void Dispose() => ++Disposals;
    }
}
