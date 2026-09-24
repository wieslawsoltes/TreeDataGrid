using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Header callbacks through public native DP, column and realization contracts.</summary>
internal static class HeaderLifetimeRuntimeChecks
{
    internal static void Run()
    {
        RunCase("nested header getter", () => NestedGetter(0));
        RunCase("nested resize getter", () => NestedGetter(1));
        RunCase("nested sort getter", () => NestedGetter(2));
        RunCase("same-column replacement", GetterReplacesRealization);
        RunCase("recursive construction", RecursiveConstruction);
        RunCase("cancel during event add", () => CancelDuringAdd(false));
        RunCase("cancel then throwing event add", () => CancelDuringAdd(true));
        RunCase("cleanup reentry", CleanupReentry);
        RunCase("single cleanup failure", () => CleanupFailures(false));
        RunCase("ordered cleanup failures", () => CleanupFailures(true));
        RunCase("negative update index", () => InvalidIndex(-1));
        RunCase("past-end update index", () => InvalidIndex(1));
        RunCase("maximum update index", () => InvalidIndex(int.MaxValue));
        RunCase("warm unchanged refresh", WarmRefresh);
        Console.WriteLine("UNO_RUNTIME_HEADER_LIFETIME_PASSED: cases=14; nested metadata, same-column lifetimes, event-accessor cancellation, independent cleanup, reindex validation, reuse and unchanged-refresh allocation");
    }

    private static void NestedGetter(int kind)
    {
        using var fixture = new Fixture();
        var column = fixture.Column;
        if (kind == 0)
            column.ReadHeader = () => { column.HeaderValue = "Latest"; column.Notify(); };
        else if (kind == 1)
            column.ReadResize = () => { column.ResizeValue = false; column.Notify(); };
        else
            column.ReadSort = () => { column.SortValue = ListSortDirection.Descending; column.Notify(); };
        column.Notify();
        Check(Equals(fixture.Header.Header, column.HeaderValue) && Equals(fixture.Header.Content, column.HeaderValue),
            "A returning header getter overwrote a nested refresh.");
        Check(fixture.Header.CanUserResize == column.ResizeValue && fixture.Header.SortDirection == column.SortValue,
            "A returning metadata getter overwrote the newer permissions or sort glyph.");
        Check(column.Subscribers == fixture.Baseline + 1, "Nested refresh changed subscription ownership.");
    }

    private static void GetterReplacesRealization()
    {
        using var fixture = new Fixture();
        fixture.Column.ReadHeader = () =>
        {
            fixture.Header.Unrealize();
            fixture.Column.HeaderValue = "Replacement";
            fixture.Header.Realize(fixture.Columns, 0);
        };
        fixture.Column.Notify();
        Check(Equals(fixture.Header.Header, "Replacement") && Equals(fixture.Header.Content, "Replacement") && fixture.Header.ColumnIndex == 0,
            "A getter from an old lifetime overwrote the same column's replacement header.");
        Check(fixture.Column.Subscribers == fixture.Baseline + 1, "Same-column replacement retained an obsolete handler.");
    }

    private static void RecursiveConstruction()
    {
        using var fixture = new Fixture(realize: false);
        var calls = 0;
        fixture.Column.ReadHeader = () =>
        {
            ++calls;
            Check(Capture(() => fixture.Header.Realize(fixture.Columns, 0)) is InvalidOperationException,
                "Construction admitted a second realization from a metadata getter.");
            fixture.Header.Unrealize();
        };
        fixture.Header.Realize(fixture.Columns, 0);
        Check(calls == 1, "Construction getter was unexpectedly repeated.");
        Retired(fixture);
        Reuse(fixture);
    }

    private static void CancelDuringAdd(bool fail)
    {
        using var fixture = new Fixture(realize: false);
        var failure = new InvalidOperationException("Expected add failure");
        fixture.Column.BeforeAdd = () => fixture.Header.Unrealize();
        if (fail) fixture.Column.AfterAdd = () => throw failure;
        var actual = Capture(() => fixture.Header.Realize(fixture.Columns, 0));
        Check(fail ? ReferenceEquals(actual, failure) : actual is null, "A cancelled add changed the original failure identity.");
        Retired(fixture);
        Reuse(fixture);
    }

    private static void CleanupReentry()
    {
        using var fixture = new Fixture();
        var callbacks = 0;
        fixture.Column.AfterRemove = () =>
        {
            ++callbacks;
            fixture.Header.Unrealize();
            Check(Capture(() => fixture.Header.Realize(fixture.Columns, 0)) is InvalidOperationException,
                "The remove accessor admitted replacement during unfinished cleanup.");
        };
        fixture.Observe(TreeDataGridColumnHeader.HeaderProperty, () =>
        {
            if (fixture.Header.Header is not null) return;
            ++callbacks;
            fixture.Header.Unrealize();
            Check(Capture(() => fixture.Header.Realize(fixture.Columns, 0)) is InvalidOperationException,
                "A DP callback admitted replacement during unfinished cleanup.");
            Check(Capture(() => fixture.Header.UpdateColumnIndex(0)) is InvalidOperationException,
                "A DP callback reindexed a retired header.");
        });
        fixture.Header.Unrealize();
        Check(callbacks == 2, "Recursive cleanup repeated notifications or remove accessors.");
        Retired(fixture);
        Reuse(fixture);
    }

    private static void CleanupFailures(bool multiple)
    {
        using var fixture = new Fixture();
        var removeFailure = new InvalidOperationException("Expected remove failure");
        var contextFailure = new InvalidOperationException("Expected header callback failure");
        var resizeFailure = new InvalidOperationException("Expected resize callback failure");
        fixture.Column.AfterRemove = () => throw removeFailure;
        if (multiple)
        {
            fixture.Observe(TreeDataGridColumnHeader.HeaderProperty, () =>
            {
                if (fixture.Header.Header is null) throw contextFailure;
            });
            fixture.Observe(TreeDataGridColumnHeader.CanUserResizeProperty, () =>
            {
                if (!fixture.Header.CanUserResize) throw resizeFailure;
            });
        }
        var failure = Capture(fixture.Header.Unrealize);
        if (multiple)
        {
            Check(failure is AggregateException { InnerExceptions.Count: 3 }, "Cleanup lost one of its independent failures.");
            var errors = ((AggregateException)failure!).InnerExceptions;
            Check(ReferenceEquals(errors[0], removeFailure) && ReferenceEquals(errors[1], contextFailure) && ReferenceEquals(errors[2], resizeFailure),
                "Cleanup failures lost identity or encounter order.");
        }
        else Check(ReferenceEquals(failure, removeFailure), "A single cleanup failure was unnecessarily wrapped.");
        Retired(fixture);
        Reuse(fixture);
    }

    private static void InvalidIndex(int index)
    {
        using var fixture = new Fixture();
        var failure = Capture(() => fixture.Header.UpdateColumnIndex(index));
        Check(failure is ArgumentOutOfRangeException { ParamName: "columnIndex" }, "An invalid index was not rejected before mutation.");
        Check(fixture.Header.ColumnIndex == 0 && Equals(fixture.Header.Header, "Original") && fixture.Column.Subscribers == fixture.Baseline + 1,
            "An invalid index changed the current header lifetime.");
    }

    private static void WarmRefresh()
    {
        using var fixture = new Fixture();
        for (var index = 0; index < 1024; ++index) fixture.Column.Notify();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 4096; ++index) fixture.Column.Notify();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"UNO_HEADER_UNCHANGED_REFRESH_ALLOCATION: iterations=4096; managedBytes={allocated}");
        Check(allocated == 0, $"Unchanged header refresh allocated {allocated} managed bytes.");
        Check(Equals(fixture.Header.Content, "Original") && fixture.Header.CanUserResize && fixture.Header.SortDirection == ListSortDirection.Ascending,
            "The warm path changed header metadata.");
    }

    private static void Retired(Fixture fixture) => Check(fixture.Header.ColumnIndex == -1 && fixture.Header.Header is null && fixture.Header.Content is null &&
        fixture.Header.ContentTemplate is null && fixture.Header.ContentTemplateSelector is null && !fixture.Header.CanUserResize &&
        fixture.Header.SortDirection is null && fixture.Header.Visibility == Visibility.Collapsed && fixture.Column.Subscribers == fixture.Baseline,
        "Header retirement left an owned subscription, model index, content, permission or visible state.");

    private static void Reuse(Fixture fixture)
    {
        fixture.ClearCallbacks();
        fixture.Column.HeaderValue = "Reusable";
        fixture.Header.Realize(fixture.Columns, 0);
        Check(Equals(fixture.Header.Content, "Reusable") && fixture.Header.ColumnIndex == 0 && fixture.Header.Visibility == Visibility.Visible,
            "A completed or failed cleanup left the header unusable.");
        fixture.Header.Unrealize();
        Retired(fixture);
    }

    private static Exception? Capture(Action action)
    {
        try { action(); return null; }
        catch (Exception error) { return error; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void RunCase(string name, Action run) { run(); Console.WriteLine("UNO_HEADER_LIFETIME_CASE_PASSED: " + name); }

    private sealed class Fixture : IDisposable
    {
        internal TestColumn Column { get; } = new();
        internal ColumnListBase<TestColumn> Columns { get; }
        internal TreeDataGridColumnHeader Header { get; } = new();
        internal int Baseline { get; }
        private readonly List<(DependencyProperty Property, long Token)> _tokens = new();
        internal Fixture(bool realize = true)
        {
            Columns = new() { Column };
            Baseline = Column.Subscribers;
            if (realize) Header.Realize(Columns, 0);
        }
        internal void Observe(DependencyProperty property, Action callback) =>
            _tokens.Add((property, Header.RegisterPropertyChangedCallback(property, (_, _) => callback())));
        internal void ClearCallbacks()
        {
            Column.BeforeAdd = Column.AfterAdd = Column.AfterRemove = Column.ReadHeader = Column.ReadResize = Column.ReadSort = null;
            foreach (var (property, token) in _tokens) Header.UnregisterPropertyChangedCallback(property, token);
            _tokens.Clear();
        }
        public void Dispose()
        {
            ClearCallbacks();
            try { Header.Unrealize(); }
            finally { Columns.Clear(); }
            Check(Column.Subscribers == 0, "The fixture retained a caller-owned column subscription.");
        }
    }

    private sealed class TestColumn : IUpdateColumnLayout
    {
        private static readonly PropertyChangedEventArgs HeaderChanged = new(nameof(Header));
        private PropertyChangedEventHandler? _changed;
        internal Action? BeforeAdd, AfterAdd, AfterRemove, ReadHeader, ReadResize, ReadSort;
        internal int Subscribers;
        internal string HeaderValue = "Original";
        internal bool ResizeValue = true;
        internal ListSortDirection? SortValue = ListSortDirection.Ascending;
        public object? Header { get { var value = HeaderValue; InvokeOnce(ref ReadHeader); return value; } }
        public bool? CanUserResize { get { var value = ResizeValue; InvokeOnce(ref ReadResize); return value; } }
        public ListSortDirection? SortDirection { get { var value = SortValue; InvokeOnce(ref ReadSort); return value; } set => SortValue = value; }
        public object? Tag { get; set; }
        public GridLength Width { get; private set; } = new(120);
        public double ActualWidth => Width.Value;
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public double CellMeasured(double width, int rowIndex) => ActualWidth;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(GridLength width) => Width = width;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { InvokeOnce(ref BeforeAdd); _changed += value; ++Subscribers; InvokeOnce(ref AfterAdd); }
            remove { _changed -= value; --Subscribers; InvokeOnce(ref AfterRemove); }
        }
        internal void Notify() => _changed?.Invoke(this, HeaderChanged);
        private static void InvokeOnce(ref Action? action) { var current = action; action = null; current?.Invoke(); }
    }
}
