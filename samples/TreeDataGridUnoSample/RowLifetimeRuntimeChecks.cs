using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using Uno.Controls.Selection;

namespace TreeDataGridUnoSample;

/// <summary>Public native row lifetime checks without internal ownership or timing sleeps.</summary>
internal static class RowLifetimeRuntimeChecks
{
    internal static void Run()
    {
        RunCase("negative index", () => InvalidIndex(-1));
        RunCase("past-end index", () => InvalidIndex(2));
        RunCase("maximum integer index", () => InvalidIndex(int.MaxValue));
        RunCase("recursive unrealization", RecursiveUnrealization);
        RunCase("reindex during clearing", ReindexDuringClearing);
        RunCase("realize during cleanup notification", RealizeDuringCleanup);
        RunCase("ordinary cleanup", () => CleanupFailures(false, false, false));
        RunCase("clearing-hook failure", () => CleanupFailures(true, false, false));
        RunCase("DataContext notification failure", () => CleanupFailures(false, true, false));
        RunCase("selection notification failure", () => CleanupFailures(false, false, true));
        RunCase("multiple cleanup failures", () => CleanupFailures(true, true, true));
        Console.WriteLine("UNO_RUNTIME_ROW_LIFETIME_PASSED: cases=11; index validation, recursive cleanup, teardown guards, independent cleanup attempts, exception identity/order and subsequent reuse");
    }

    private static void InvalidIndex(int index)
    {
        using var fixture = new Fixture();
        var row = fixture.Row;
        var failure = Capture(() => row.UpdateIndex(index));
        Check(failure is ArgumentOutOfRangeException { ParamName: "rowIndex" }, "An invalid index was not rejected with its argument name.");
        Check(row.RowIndex == 0 && ReferenceEquals(row.Model, fixture.Models[0]) && row.IsSelected &&
            row.Visibility == Visibility.Visible && row.ClearingCalls == 0, "Invalid reindexing changed live state before rejection.");
        row.Unrealize();
        AssertRetired(row);
        VerifyReusable(fixture);
    }

    private static void RecursiveUnrealization()
    {
        using var fixture = new Fixture();
        var entered = false;
        fixture.Row.Clearing = row =>
        {
            // Bound recursion so the pre-fix behavior fails rather than overflowing.
            if (entered) return;
            entered = true;
            row.Unrealize();
            row.UnrealizeOnItemRemoved();
            Check(row.RowIndex == 0 && ReferenceEquals(row.Model, fixture.Models[0]), "Nested cleanup retired the outer callback's identity.");
        };
        fixture.Row.Unrealize();
        Check(entered && fixture.Row.ClearingCalls == 1, "Recursive cleanup repeated the clearing hook.");
        AssertRetired(fixture.Row);
        VerifyReusable(fixture);
    }

    private static void ReindexDuringClearing()
    {
        using var fixture = new Fixture();
        fixture.Row.Clearing = row =>
        {
            var failure = Capture(() => row.UpdateIndex(1));
            Check(failure is InvalidOperationException && row.RowIndex == 0, "Reindexing was admitted while clearing.");
        };
        fixture.Row.Unrealize();
        AssertRetired(fixture.Row);
        VerifyReusable(fixture);
    }

    private static void RealizeDuringCleanup()
    {
        using var fixture = new Fixture();
        var attempts = 0;
        fixture.Observe(FrameworkElement.DataContextProperty, (_, _) =>
        {
            if (fixture.Row.Model is not null) return;
            ++attempts;
            Check(fixture.Row.RowIndex == -1, "Cleanup notification did not observe a retired index.");
            var failure = Capture(() => fixture.Realize(1));
            Check(failure is InvalidOperationException, "A new realization entered unfinished cleanup.");
            fixture.Row.Unrealize();
        });
        fixture.Row.Unrealize();
        Check(attempts == 1 && fixture.Row.ClearingCalls == 1, "Cleanup notifications or hooks repeated.");
        AssertRetired(fixture.Row);
        VerifyReusable(fixture);
    }

    private static void CleanupFailures(bool failHook, bool failContext, bool failSelection)
    {
        using var fixture = new Fixture();
        var row = fixture.Row;
        var observed = new List<string>();
        var hookFailure = new InvalidOperationException("Expected hook failure.");
        var contextFailure = new InvalidOperationException("Expected DataContext failure.");
        var selectionFailure = new InvalidOperationException("Expected selection failure.");
        var expected = new List<Exception>();
        if (failHook) expected.Add(hookFailure);
        if (failContext) expected.Add(contextFailure);
        if (failSelection) expected.Add(selectionFailure);
        row.Clearing = _ => { observed.Add("hook"); if (failHook) throw hookFailure; };
        fixture.Observe(FrameworkElement.DataContextProperty, (_, _) =>
        {
            if (row.Model is not null) return;
            observed.Add("context");
            if (failContext) throw contextFailure;
        });
        fixture.Observe(TreeDataGridRow.IsSelectedProperty, (_, _) =>
        {
            if (row.IsSelected) return;
            observed.Add("selection");
            if (failSelection) throw selectionFailure;
        });
        fixture.Observe(UIElement.VisibilityProperty, (_, _) =>
        {
            if (row.Visibility == Visibility.Collapsed) observed.Add("visibility");
        });
        var actual = Capture(row.Unrealize);
        if (expected.Count == 0) Check(actual is null, "Ordinary cleanup failed.");
        else if (expected.Count == 1) Check(ReferenceEquals(actual, expected[0]), "Single failure was replaced or wrapped.");
        else
        {
            Check(actual is AggregateException aggregate && aggregate.InnerExceptions.Count == expected.Count, "Multiple failures were lost.");
            var failures = ((AggregateException)actual!).InnerExceptions;
            for (var i = 0; i < expected.Count; ++i)
                Check(ReferenceEquals(failures[i], expected[i]), "Failures lost identity or encounter order.");
        }
        Check(observed.SequenceEqual(new[] { "hook", "context", "selection", "visibility" }), "A failing callback skipped or reordered later cleanup.");
        AssertRetired(row);
        VerifyReusable(fixture);
    }

    private static void VerifyReusable(Fixture fixture)
    {
        fixture.ClearCallbacks();
        fixture.Realize(1);
        Check(fixture.Row.RowIndex == 1 && ReferenceEquals(fixture.Row.Model, fixture.Models[1]) && fixture.Row.IsSelected &&
            fixture.Row.Visibility == Visibility.Visible, "Cleanup prevented subsequent reuse.");
        Check(fixture.Source.Rows.Count == 2, "Cleanup changed or disposed the caller's Core source.");
        fixture.Row.Unrealize();
        AssertRetired(fixture.Row);
    }
    private static void AssertRetired(ProbeRow row) => Check(row.RowIndex == -1 && row.Model is null && !row.IsSelected &&
        row.Visibility == Visibility.Collapsed, "Row retained model/index/selection/visibility state.");
    private static Exception? Capture(Action action)
    {
        try { action(); return null; }
        catch (Exception error) { return error; }
    }
    private static void RunCase(string name, Action action)
    {
        action();
        Console.WriteLine("UNO_ROW_LIFETIME_CASE_PASSED: " + name);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TreeDataGridElementFactory _factory = new();
        private readonly SelectedInteraction _selection = new();
        private readonly List<(DependencyProperty Property, long Token)> _callbacks = new();
        internal Item[] Models { get; } = [new("First"), new("Second")];
        internal FlatTreeDataGridSource<Item> Source { get; }
        internal TreeDataGridPresentation View { get; }
        internal ProbeRow Row { get; } = new();
        internal Fixture()
        {
            Source = new FlatTreeDataGridSource<Item>(Models);
            Source.Columns.Add(new TreeDataGridCore.Models.TextColumn<Item, string>("Name", item => item.Name, width: new(120)));
            View = TreeDataGridPresentation.Create(Source);
            Realize(0);
        }
        internal void Realize(int index) => Row.Realize(_factory, _selection, View.Columns, View.Rows, index);
        internal void Observe(DependencyProperty property, DependencyPropertyChangedCallback callback) =>
            _callbacks.Add((property, Row.RegisterPropertyChangedCallback(property, callback)));
        internal void ClearCallbacks()
        {
            Row.Clearing = null;
            foreach (var (property, token) in _callbacks) Row.UnregisterPropertyChangedCallback(property, token);
            _callbacks.Clear();
        }
        public void Dispose()
        {
            ClearCallbacks();
            try { Row.Unrealize(); }
            finally { try { View.Dispose(); } finally { Source.Dispose(); } }
        }
    }
    private sealed partial class ProbeRow : TreeDataGridRow
    {
        internal Action<ProbeRow>? Clearing { get; set; }
        internal int ClearingCalls { get; private set; }
        protected override void OnUnrealizing(int rowIndex, TreeDataGridRowUnrealizeReason reason)
        {
            ++ClearingCalls;
            Clearing?.Invoke(this);
            base.OnUnrealizing(rowIndex, reason);
        }
    }
    private sealed class SelectedInteraction : ITreeDataGridSelectionInteraction
    {
        public event EventHandler? SelectionChanged { add { } remove { } }
        public bool IsRowSelected(int rowIndex) => true;
    }
    private sealed record Item(string Name);
}
