using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Session adoption is conditional on the same live native cell realization.</summary>
internal static class EditStartReentrancyRuntimeChecks
{
    public static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        var host = new Border { Width = 360, Height = 70 };
        var factory = new TreeDataGridElementFactory();
        page.Content = host;
        var cases = 0;
        try
        {
            await Task.Delay(50);
            Exercise("permission retirement", (cell, old) =>
            {
                old.OnCanEdit = cell.Unrealize;
                Check(!cell.BeginEdit() && cell.Model is null && !cell.IsEditing, "Permission getter retired its cell but editing continued.");
                Check(old.Begins == 0 && old.Cancels == 0, "A retired permission getter started a model transaction.");
            });
            foreach (var boundary in new[] { "value", "options", "target", "format" })
            {
                Exercise(boundary + " replacement", (cell, old) =>
                {
                    var next = new Probe("new");
                    Action replace = () => Replace(cell, next);
                    if (boundary == "value") old.OnRead = replace;
                    else if (boundary == "options") old.OnOptions = replace;
                    else if (boundary == "target") old.OnTarget = replace;
                    else old.Raw = new FormattedValue(replace);
                    Check(!cell.BeginEdit(), "A retired getter started an edit on its replacement.");
                    Check(ReferenceEquals(cell.Model, next) && !cell.IsEditing && cell.Value == "new", "Retired input changed its replacement's native state.");
                    Check(old.Begins == 0 && next.Begins == 0 && next.Writes == 0, "An accessor retirement crossed the edit transaction boundary.");
                });
            }
            Exercise("same model new realization", (cell, model) =>
            {
                model.OnCanEdit = () => Replace(cell, model);
                Check(!cell.BeginEdit() && !cell.IsEditing && model.Begins == 0,
                    "Identity-only validation missed a new realization of the same cell model.");
            });
            Exercise("recursive same realization", (cell, model) =>
            {
                var nested = true;
                model.OnCanEdit = () => nested = cell.BeginEdit();
                Check(cell.BeginEdit() && !nested && model.Begins == 1,
                    "Recursive permission evaluation started duplicate edit sessions.");
                cell.CancelEdit();
                Check(model.Cancels == 1 && !cell.IsEditing, "Recursive edit cancellation was unbalanced.");
            });
            Exercise("new realization starts from getter", (cell, old) =>
            {
                var next = new Probe("new");
                var nested = false;
                old.OnCanEdit = () => { Replace(cell, next); nested = cell.BeginEdit(); };
                Check(!cell.BeginEdit() && nested && cell.IsEditing && ReferenceEquals(cell.Model, next),
                    "The obsolete outer call erased a newer edit started by its getter.");
                Check(old.Begins == 0 && next.Begins == 1 && next.Cancels == 0 && cell.EditingText == "new",
                    "An obsolete getter changed the newer edit's transaction or buffer.");
                cell.CancelEdit();
                Check(next.Cancels == 1, "The adopted replacement edit did not cancel exactly once.");
            });
            Exercise("new realization starts from model BeginEdit", (cell, old) =>
            {
                var next = new Probe("new");
                var nested = false;
                old.OnBegin = () => { Replace(cell, next); nested = cell.BeginEdit(); };
                Check(!cell.BeginEdit() && nested && cell.IsEditing && ReferenceEquals(cell.Model, next),
                    "A returning model BeginEdit replaced a newer active edit.");
                Check(old.Begins == 1 && old.Cancels == 1 && next.Begins == 1 && next.Cancels == 0,
                    "Retired/unadopted edit ownership did not balance cancellation.");
            });
            Exercise("native editor callback", (cell, old) =>
            {
                Check(cell.BeginEdit(), "Could not prime the native editor.");
                cell.CancelEdit();
                old.Begins = old.Cancels = 0;
                var editor = ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBox>().Single();
                var armed = true;
                var nested = false;
                var next = new Probe("new");
                TextChangedEventHandler callback = (_, _) =>
                {
                    if (!armed) return;
                    armed = false;
                    Replace(cell, next);
                    nested = cell.BeginEdit();
                };
                editor.TextChanged += callback;
                try
                {
                    Check(!cell.BeginEdit() && !armed && nested && cell.IsEditing && cell.EditingText == "new",
                        "An old native editor initialization overwrote its replacement's buffer.");
                    Check(old.Begins == 0 && next.Begins == 1 && next.Cancels == 0, "Native text initialization crossed realizations.");
                }
                finally { editor.TextChanged -= callback; }
            });
            Exercise("editing state callback", (cell, old) =>
            {
                var next = new Probe("new");
                var armed = true;
                var nested = false;
                var token = cell.RegisterPropertyChangedCallback(TreeDataGridCell.IsEditingProperty, (_, _) =>
                {
                    if (!armed || !cell.IsEditing) return;
                    armed = false;
                    Replace(cell, next);
                    nested = cell.BeginEdit();
                });
                try
                {
                    Check(!cell.BeginEdit() && !armed && nested && cell.IsEditing && ReferenceEquals(cell.Model, next),
                        "A retired IsEditing callback overwrote its replacement edit.");
                    Check(old.Begins == 1 && old.Cancels == 1 && next.Begins == 1 && next.Cancels == 0,
                        "Native state publication did not preserve current edit ownership.");
                }
                finally { cell.UnregisterPropertyChangedCallback(TreeDataGridCell.IsEditingProperty, token); }
            });
            Exercise("throw after session adoption", (cell, model) =>
            {
                var failure = new InvalidOperationException("Expected native edit-state failure.");
                var token = cell.RegisterPropertyChangedCallback(TreeDataGridCell.IsEditingProperty, (_, _) =>
                {
                    if (cell.IsEditing) throw failure;
                });
                Exception? received = null;
                try { cell.BeginEdit(); }
                catch (Exception error) { received = error; }
                finally { cell.UnregisterPropertyChangedCallback(TreeDataGridCell.IsEditingProperty, token); }
                Check(ReferenceEquals(received, failure) && !cell.IsEditing && model.Begins == 1 && model.Cancels == 1,
                    "A throwing native state callback leaked its edit or changed the original exception.");
                Check(cell.BeginEdit(), "A failed edit left the recursive-start guard set.");
                cell.EditingText = "recovered";
                Check(cell.CommitEdit() && model.Text == "recovered" && model.Ends == 1,
                    "Editing did not recover after failed session adoption.");
            });
            Exercise("throw inside model BeginEdit", (cell, model) =>
            {
                var failure = new InvalidOperationException("Expected model edit failure.");
                model.OnBegin = () => throw failure;
                Exception? received = null;
                try { cell.BeginEdit(); }
                catch (Exception error) { received = error; }
                Check(ReferenceEquals(received, failure) && !cell.IsEditing && model.Begins == 1 && model.Cancels == 1,
                    "A throwing model BeginEdit was not cancelled once with its original failure.");
                Check(cell.BeginEdit(), "A throwing model BeginEdit prevented retry.");
            });
            Console.WriteLine($"UNO_RUNTIME_EDIT_START_REENTRANCY_PASSED: cases={cases}; permission/value/options/format/target boundaries, same-model generations, recursive starts, newer edit preservation, native editor/state callbacks, exception cleanup and retry");
        }
        finally { host.Child = null; page.Content = previous; }

        void Replace(TreeDataGridTextCell cell, Probe model)
        {
            cell.Unrealize();
            cell.Realize(factory, null, model, 0, 0);
            cell.ApplyTemplate();
        }
        void Exercise(string name, Action<TreeDataGridTextCell, Probe> check)
        {
            var model = new Probe("old");
            var cell = new TreeDataGridTextCell { Width = 320, Height = 50 };
            host.Child = cell;
            try
            {
                cell.Realize(factory, null, model, 0, 0);
                cell.ApplyTemplate();
                page.UpdateLayout();
                check(cell, model);
                ++cases;
                Console.WriteLine("UNO_EDIT_START_CASE_PASSED: " + name);
            }
            catch (Exception error) { throw new InvalidOperationException("Edit-start case failed: " + name, error); }
            finally { cell.Unrealize(); host.Child = null; }
            Check(model.Disposals == 0, "A native cell disposed a borrowed edit model.");
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Probe(string text) : CellValue, IEditableObject
    {
        internal string Text = text;
        internal object? Raw;
        internal Action? OnCanEdit, OnRead, OnOptions, OnTarget, OnBegin;
        internal int Begins, Cancels, Ends, Writes, Disposals;
        public override object? Value { get { InvokeOnce(ref OnRead); return Raw ?? Text; } }
        public override bool CanEdit { get { InvokeOnce(ref OnCanEdit); return true; } }
        public override TextCellOptions? TextOptions { get { InvokeOnce(ref OnOptions); return null; } }
        public override object? EditTarget { get { InvokeOnce(ref OnTarget); return this; } }
        public override void Write(object? value) { ++Writes; Text = Convert.ToString(value) ?? string.Empty; }
        public void BeginEdit() { ++Begins; InvokeOnce(ref OnBegin); }
        public void CancelEdit() => ++Cancels;
        public void EndEdit() => ++Ends;
        public override void Dispose() => ++Disposals;
        private static void InvokeOnce(ref Action? callback)
        {
            var action = callback;
            callback = null;
            action?.Invoke();
        }
    }
    private sealed class FormattedValue(Action format)
    {
        private Action? _format = format;
        public override string ToString()
        {
            var action = _format;
            _format = null;
            action?.Invoke();
            return "obsolete formatted value";
        }
    }
}
