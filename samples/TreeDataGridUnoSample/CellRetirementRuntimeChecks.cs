using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Public native callbacks must not interrupt the rest of cell retirement.</summary>
internal static class CellRetirementRuntimeChecks
{
    internal static void Run()
    {
        for (var failure = 0; failure < 6; ++failure) FailureCleanup(failure);
        FailureCleanup(6);
        RecursiveCleanup();
        RejectReplacement();
        PreserveSynchronousRebind();
        Console.WriteLine("UNO_RUNTIME_CELL_RETIREMENT_PASSED: cases=10; independent current/selection/content/cancellation/adapter cleanup, error identity/order, recursive cleanup, rejected adoption, borrowed ownership and rebind reuse");
    }

    private static void FailureCleanup(int mode)
    {
        var factory = new TreeDataGridElementFactory();
        var model = new TextModel();
        var cell = new ProbeCell();
        var expected = new List<Exception>();
        Exception? cancellation = mode is 4 or 6 ? new InvalidOperationException("cancel") : null;
        Exception? current = mode is 1 or 6 ? new InvalidOperationException("current") : null;
        Exception? selected = mode is 2 or 6 ? new InvalidOperationException("selected") : null;
        Exception? content = mode is 3 or 6 ? new InvalidOperationException("content") : null;
        Exception? removal = mode is 5 or 6 ? new InvalidOperationException("unsubscribe") : null;
        foreach (var error in new[] { cancellation, current, selected, content, removal })
            if (error is not null) expected.Add(error);
        cell.Realize(factory, null, model, 1, 2);
        cell.IsCurrent = cell.IsSelected = true;
        var currentToken = cell.RegisterPropertyChangedCallback(TreeDataGridCell.IsCurrentProperty, (_, _) =>
        {
            if (!cell.IsCurrent && current is not null) throw current;
        });
        var selectedToken = cell.RegisterPropertyChangedCallback(TreeDataGridCell.IsSelectedProperty, (_, _) =>
        {
            if (!cell.IsSelected && selected is not null) throw selected;
        });
        try
        {
            cell.CancelFailure = cancellation;
            cell.ContentFailure = content;
            model.RemoveFailure = removal;
            var actual = Capture(cell.Unrealize);
            if (expected.Count == 0) Check(actual is null, "Normal retirement failed.");
            else if (expected.Count == 1) Check(ReferenceEquals(expected[0], actual), "A single failure was replaced or wrapped.");
            else
            {
                Check(actual is AggregateException, "Multiple failures were not aggregated.");
                var errors = ((AggregateException)actual!).InnerExceptions;
                Check(errors.Count == expected.Count, "A cleanup stage failed to execute or published duplicate failures.");
                for (var i = 0; i < errors.Count; ++i)
                    Check(ReferenceEquals(errors[i], expected[i]), "Cleanup error identity/order changed.");
            }
            AssertRetired(cell, model);
            Check(cell.ClearCalls == 1, "Virtual content cleanup was skipped or called twice.");
        }
        finally
        {
            cell.UnregisterPropertyChangedCallback(TreeDataGridCell.IsCurrentProperty, currentToken);
            cell.UnregisterPropertyChangedCallback(TreeDataGridCell.IsSelectedProperty, selectedToken);
            cell.CancelFailure = cell.ContentFailure = model.RemoveFailure = null;
            cell.Unrealize();
        }
        VerifyReuse(cell, model, factory);
        Console.WriteLine($"UNO_CELL_RETIREMENT_CASE_PASSED: failure-mode={mode}");
    }

    private static void RecursiveCleanup()
    {
        var factory = new TreeDataGridElementFactory();
        var model = new TextModel();
        var cell = new ProbeCell();
        cell.Realize(factory, null, model, 0, 0);
        cell.IsCurrent = true;
        var calls = 0;
        var token = cell.RegisterPropertyChangedCallback(TreeDataGridCell.IsCurrentProperty, (_, _) =>
        {
            if (cell.IsCurrent) return;
            ++calls;
            cell.Unrealize();
            Check(!cell.BeginEdit(), "A new edit was accepted during retirement.");
        });
        try
        {
            cell.Unrealize();
            Check(calls == 1 && cell.ClearCalls == 1 && cell.CancelCalls == 1, "Recursive retirement repeated a lifecycle stage.");
            AssertRetired(cell, model);
        }
        finally { cell.UnregisterPropertyChangedCallback(TreeDataGridCell.IsCurrentProperty, token); cell.Unrealize(); }
        VerifyReuse(cell, model, factory);
    }

    private static void RejectReplacement()
    {
        var factory = new TreeDataGridElementFactory();
        var model = new TextModel();
        var replacement = new TextModel();
        var cell = new ProbeCell();
        cell.Realize(factory, null, model, 0, 0);
        cell.IsCurrent = true;
        var attempted = false;
        var token = cell.RegisterPropertyChangedCallback(TreeDataGridCell.IsCurrentProperty, (_, _) =>
        {
            if (cell.IsCurrent) return;
            attempted = true;
            Check(Capture(() => cell.Realize(factory, null, replacement, 2, 3)) is InvalidOperationException,
                "A cleanup callback adopted a new cell model.");
            Check(replacement.Subscribers == 0, "Rejected adoption created an orphaned model adapter.");
        });
        try { cell.Unrealize(); Check(attempted, "Replacement boundary was not exercised."); AssertRetired(cell, model); }
        finally { cell.UnregisterPropertyChangedCallback(TreeDataGridCell.IsCurrentProperty, token); cell.Unrealize(); }
        VerifyReuse(cell, replacement, factory);
    }

    private static void PreserveSynchronousRebind()
    {
        var factory = new TreeDataGridElementFactory();
        var first = new TextModel();
        var second = new TextModel();
        var cell = new ProbeCell();
        cell.Realize(factory, null, first, 0, 0);
        cell.BeginRebind();
        try
        {
            cell.Unrealize();
            Check(first.Subscribers == 0 && cell.Model is null && cell.ClearCalls == 0 && cell.Visibility == Visibility.Visible,
                "Synchronous rebind retained the model or destroyed retained content.");
            cell.Realize(factory, null, second, 1, 1);
            cell.EndRebind(true);
            Check(ReferenceEquals(cell.Model, second) && second.Subscribers == 1, "Rebind did not adopt the replacement model once.");
        }
        finally { cell.EndRebind(false); cell.Unrealize(); }
        AssertRetired(cell, second);
    }

    private static void VerifyReuse(ProbeCell cell, TextModel model, TreeDataGridElementFactory factory)
    {
        cell.Realize(factory, null, model, 4, 5);
        Check(ReferenceEquals(cell.Model, model) && cell.ColumnIndex == 4 && cell.RowIndex == 5 && model.Subscribers == 1,
            "A retired cell could not be reused with one model subscription.");
        cell.Unrealize();
        AssertRetired(cell, model);
    }
    private static void AssertRetired(ProbeCell cell, TextModel model) => Check(
        cell.RowIndex == -1 && cell.ColumnIndex == -1 && cell.Model is null && cell.Row is null && cell.RowModel is null &&
        cell.Column is null && !cell.IsSelected && !cell.IsCurrent && !cell.IsEditing && !cell.HasValidationError &&
        cell.Visibility == Visibility.Collapsed && model.Subscribers == 0 && model.Disposals == 0,
        "Retirement retained cell state, a subscription or disposed a borrowed model.");
    private static Exception? Capture(Action action) { try { action(); return null; } catch (Exception error) { return error; } }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed partial class ProbeCell : TreeDataGridCell
    {
        internal Exception? CancelFailure;
        internal Exception? ContentFailure;
        internal int CancelCalls;
        internal int ClearCalls;
        public override void CancelEdit() { ++CancelCalls; if (CancelFailure is { } error) throw error; base.CancelEdit(); }
        protected override void ClearContent() { ++ClearCalls; if (ContentFailure is { } error) throw error; base.ClearContent(); }
    }
    private sealed class TextModel : ITextCell, INotifyPropertyChanged, IDisposable
    {
        private PropertyChangedEventHandler? _changed;
        internal Exception? RemoveFailure;
        internal int Subscribers;
        internal int Disposals;
        public object? Value => Text;
        public string? Text { get; set; } = "Borrowed cell";
        public bool CanEdit => true;
        public BeginEditGestures EditGestures => BeginEditGestures.Default;
        public TextAlignment TextAlignment => TextAlignment.Left;
        public TextWrapping TextWrapping => TextWrapping.NoWrap;
        public TextTrimming TextTrimming => TextTrimming.None;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; if (RemoveFailure is { } error) throw error; }
        }
        public void Dispose() => ++Disposals;
    }
}
