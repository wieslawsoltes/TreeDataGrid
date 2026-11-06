using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using Core = TreeDataGridCore.Models;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

internal static class RowConstructionRuntimeChecks
{
    internal static void Run()
    {
        RunCase("nested Count realization", NestedCount);
        RunCase("throwing Count recovery", ThrowingCount);
        RunCase("invalid index guard release", InvalidIndex);
        RunCase("throwing model lookup recovery", ThrowingLookup);
        RunCase("model lookup cancellation", CancelledLookup);
        RunCase("reindex Count replaces realization", ReindexReplacesRow);
        Console.WriteLine("UNO_RUNTIME_ROW_CONSTRUCTION_PASSED: cases=6; guarded Count callbacks, validation/lookup failures, cancelled construction, reindex ownership replacement and reuse");
    }
    private static void NestedCount()
    {
        using var fixture = new Fixture();
        var calls = 0;
        fixture.Rows.OnCount = () =>
        {
            ++calls;
            Check(Capture(() => fixture.Realize(1)) is InvalidOperationException, "Count admitted a nested realization.");
            Check(fixture.Row.RowIndex == -1 && fixture.Row.Model is null, "Nested Count published row state.");
        };
        fixture.Realize(0);
        Check(calls == 1 && fixture.Row.Prepared == 1, "Construction published more than one realization.");
        fixture.AssertCurrent(0);
    }
    private static void ThrowingCount()
    {
        using var fixture = new Fixture();
        var failure = new InvalidOperationException("Count failed");
        fixture.Rows.OnCount = () => throw failure;
        Check(ReferenceEquals(failure, Capture(() => fixture.Realize(0))), "Count error identity was lost.");
        Check(fixture.Row.RowIndex == -1 && fixture.Row.Rows is null && fixture.Row.Columns is null &&
            fixture.Row.Model is null && fixture.Row.Prepared == 0, "Count failure published ownership state.");
        fixture.Realize(1);
        fixture.AssertCurrent(1);
    }
    private static void InvalidIndex()
    {
        using var fixture = new Fixture();
        foreach (var index in new[] { -1, 2, int.MaxValue })
        {
            Check(Capture(() => fixture.Realize(index)) is ArgumentOutOfRangeException { ParamName: "rowIndex" }, "Invalid index was accepted.");
            Check(fixture.Row.RowIndex == -1 && fixture.Row.Model is null, "Invalid index published state.");
        }
        fixture.Realize(0);
        fixture.AssertCurrent(0);
    }
    private static void ThrowingLookup()
    {
        using var fixture = new Fixture();
        var failure = new InvalidOperationException("Model lookup failed");
        fixture.Rows.OnRead = () => throw failure;
        Check(ReferenceEquals(failure, Capture(() => fixture.Realize(0))), "Lookup error identity was lost.");
        Check(fixture.Row.RowIndex == -1 && fixture.Row.Model is null && fixture.Row.Prepared == 0 &&
            fixture.Row.Visibility == Visibility.Collapsed, "Lookup failure left a live row.");
        fixture.Realize(1);
        fixture.AssertCurrent(1);
    }
    private static void CancelledLookup()
    {
        using var fixture = new Fixture();
        fixture.Rows.OnRead = fixture.Row.Unrealize;
        fixture.Realize(0);
        Check(fixture.Row.RowIndex == -1 && fixture.Row.Model is null && fixture.Row.Prepared == 0 &&
            fixture.Row.Visibility == Visibility.Collapsed, "Cancelled lookup published its obsolete model.");
        fixture.Realize(1);
        fixture.AssertCurrent(1);
    }
    private static void ReindexReplacesRow()
    {
        using var fixture = new Fixture();
        fixture.Realize(0);
        fixture.Rows.OnCount = () =>
        {
            fixture.Row.Unrealize();
            fixture.Realize(1);
        };
        fixture.Row.UpdateIndex(0);
        fixture.AssertCurrent(1);
        Check(fixture.Row.Prepared == 2, "Reindex overwrote the callback's newer realization.");
    }
    private static Exception? Capture(Action action)
    {
        try { action(); return null; }
        catch (Exception error) { return error; }
    }
    private static void RunCase(string name, Action action)
    {
        action();
        Console.WriteLine("UNO_ROW_CONSTRUCTION_CASE_PASSED: " + name);
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private sealed class Fixture : IDisposable
    {
        private readonly TreeDataGridElementFactory _factory = new();
        internal Item[] Items { get; } = [new("First"), new("Second")];
        internal FlatTreeDataGridSource<Item> Source { get; }
        internal TreeDataGridPresentation View { get; }
        internal CallbackRows Rows { get; }
        internal ProbeRow Row { get; } = new();
        internal Fixture()
        {
            Source = new(Items);
            Source.Columns.Add(new Core.TextColumn<Item, string>("Name", item => item.Name));
            View = TreeDataGridPresentation.Create(Source);
            Rows = new(View.Rows);
        }
        internal void Realize(int index) => Row.Realize(_factory, null, View.Columns, Rows, index);
        internal void AssertCurrent(int index) => Check(Row.RowIndex == index && ReferenceEquals(Row.Model, Items[index]) &&
            ReferenceEquals(Row.Rows, Rows) && Row.Visibility == Visibility.Visible, "Current row identity/configuration is incorrect.");
        public void Dispose()
        {
            Rows.OnCount = Rows.OnRead = null;
            try { Row.Unrealize(); }
            finally { try { View.Dispose(); } finally { Source.Dispose(); } }
        }
    }
    private sealed class CallbackRows(UI.ITreeDataGridRows inner) : UI.ITreeDataGridRows
    {
        internal Action? OnCount;
        internal Action? OnRead;
        public int Count { get { InvokeOnce(ref OnCount); return inner.Count; } }
        public Core.IRow this[int index] { get { InvokeOnce(ref OnRead); return inner[index]; } }
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => inner.CollectionChanged += value;
            remove => inner.CollectionChanged -= value;
        }
        public int ModelIndexToRowIndex(IndexPath index) => inner.ModelIndexToRowIndex(index);
        public IndexPath RowIndexToModelIndex(int index) => inner.RowIndexToModelIndex(index);
        public (int index, double y) GetRowAt(double y) => inner.GetRowAt(y);
        public UI.ICell RealizeCell(UI.IColumn column, int columnIndex, int rowIndex) => inner.RealizeCell(column, columnIndex, rowIndex);
        public void UnrealizeCell(UI.ICell cell, int columnIndex, int rowIndex) => inner.UnrealizeCell(cell, columnIndex, rowIndex);
        public IEnumerator<Core.IRow> GetEnumerator() => inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        private static void InvokeOnce(ref Action? callback)
        {
            var action = callback;
            callback = null;
            action?.Invoke();
        }
    }
    private sealed partial class ProbeRow : TreeDataGridRow
    {
        internal int Prepared;
        protected override void OnRealized(int index) { ++Prepared; base.OnRealized(index); }
    }
    private sealed record Item(string Name);
}
