using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

internal static class EditCompletionReentrancyRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        var host = new Border { Width = 360, Height = 70 };
        var factory = new TreeDataGridElementFactory();
        page.Content = host;
        var cases = 0;
        try
        {
            await Task.Delay(50);
            foreach (var throwing in new[] { false, true })
                Exercise("model cancellation, throwing=" + throwing, (cell, old) =>
                {
                    var next = new Probe("replacement");
                    var failure = new InvalidOperationException("Expected model cancellation failure.");
                    old.OnCancel = () =>
                    {
                        ReplaceAndEdit(cell, next);
                        if (throwing) throw failure;
                    };
                    Exception? received = null;
                    try { cell.CancelEdit(); } catch (Exception error) { received = error; }
                    Check(throwing ? ReferenceEquals(received, failure) : received is null, "The model cancellation result was changed.");
                    Check(old.Cancels == 1, "Old model was not cancelled exactly once.");
                    AssertReplacement(cell, next);
                });

            foreach (var commit in new[] { false, true })
                Exercise("editor cleanup, commit=" + commit, (cell, old) =>
                {
                    var editor = ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBox>().Single();
                    var next = new Probe("replacement");
                    var armed = true;
                    var token = editor.RegisterPropertyChangedCallback(TextBox.TextProperty, (_, _) =>
                    {
                        if (!armed || editor.Text.Length != 0) return;
                        armed = false;
                        ReplaceAndEdit(cell, next);
                    });
                    try
                    {
                        if (commit) Check(cell.CommitEdit(), "The completed old transaction reported failure.");
                        else cell.CancelEdit();
                        Check(!armed, "Native editor cleanup callback was not executed.");
                        Check(commit ? old.Ends == 1 && old.Cancels == 0 : old.Cancels == 1 && old.Ends == 0,
                            "The old transaction completed more than once.");
                        AssertReplacement(cell, next);
                    }
                    finally { editor.UnregisterPropertyChangedCallback(TextBox.TextProperty, token); }
                });

            Exercise("same-realization state callback", (cell, model) =>
            {
                var armed = true;
                var nested = false;
                var token = cell.RegisterPropertyChangedCallback(TreeDataGridCell.IsEditingProperty, (_, _) =>
                {
                    if (!armed || cell.IsEditing) return;
                    armed = false;
                    nested = cell.BeginEdit();
                });
                try
                {
                    cell.CancelEdit();
                    Check(!armed && nested && cell.IsEditing && cell.EditingText == "old" && model.Begins == 2 && model.Cancels == 1,
                        "Old cleanup erased the new edit started by IsEditing=false.");
                }
                finally { cell.UnregisterPropertyChangedCallback(TreeDataGridCell.IsEditingProperty, token); }
            });

            Exercise("validation publication", (cell, old) =>
            {
                var next = new Probe("replacement");
                old.RejectWrite = true;
                var armed = true;
                var token = cell.RegisterPropertyChangedCallback(TreeDataGridCell.HasValidationErrorProperty, (_, _) =>
                {
                    if (!armed || !cell.HasValidationError) return;
                    armed = false;
                    ReplaceAndEdit(cell, next);
                });
                try
                {
                    Check(!cell.CommitEdit() && !armed, "Rejected write did not exercise the validation callback.");
                    AssertReplacement(cell, next);
                    Check(ToolTipService.GetToolTip(cell) is null, "Retired validation installed its tooltip on the new edit.");
                }
                finally { cell.UnregisterPropertyChangedCallback(TreeDataGridCell.HasValidationErrorProperty, token); }
            });
            Console.WriteLine($"UNO_RUNTIME_EDIT_COMPLETION_REENTRANCY_PASSED: cases={cases}; normal/throwing model cancellation, commit/cancel text cleanup, same-realization restart, validation publication and current edit preservation");
        }
        finally { host.Child = null; page.Content = previous; }

        void ReplaceAndEdit(TreeDataGridTextCell cell, Probe model)
        {
            cell.Unrealize();
            cell.Realize(factory, null, model, 0, 0);
            cell.ApplyTemplate();
            Check(cell.BeginEdit(), "Replacement could not start its own edit.");
        }
        void Exercise(string name, Action<TreeDataGridTextCell, Probe> action)
        {
            var model = new Probe("old");
            var cell = new TreeDataGridTextCell { Width = 320, Height = 50 };
            host.Child = cell;
            try
            {
                cell.Realize(factory, null, model, 0, 0);
                cell.ApplyTemplate();
                page.UpdateLayout();
                Check(cell.BeginEdit(), "Could not start the fixture edit.");
                action(cell, model);
                ++cases;
                Console.WriteLine("UNO_EDIT_COMPLETION_CASE_PASSED: " + name);
            }
            catch (Exception error) { throw new InvalidOperationException("Edit-completion case failed: " + name, error); }
            finally { cell.Unrealize(); host.Child = null; }
        }
    }
    private static void AssertReplacement(TreeDataGridTextCell cell, Probe next) =>
        Check(ReferenceEquals(cell.Model, next) && cell.IsEditing && !cell.HasValidationError && cell.EditError is null &&
            cell.EditingText == "replacement" && next.Begins == 1 && next.Cancels == 0 && next.Ends == 0,
            $"Retired completion overwrote its replacement: editing={cell.IsEditing}, text={cell.EditingText}, begin={next.Begins}, cancel={next.Cancels}, end={next.Ends}.");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Probe(string text) : CellValue, IEditableObject
    {
        internal Action? OnCancel;
        internal bool RejectWrite;
        internal int Begins, Cancels, Ends;
        private string _text = text;
        public override object? Value => _text;
        public override bool CanEdit => true;
        public override object? EditTarget => this;
        public override void Write(object? value)
        {
            if (RejectWrite) throw new InvalidOperationException("Expected rejected write.");
            _text = Convert.ToString(value) ?? string.Empty;
        }
        public void BeginEdit() => ++Begins;
        public void EndEdit() => ++Ends;
        public void CancelEdit()
        {
            ++Cancels;
            var action = OnCancel;
            OnCancel = null;
            action?.Invoke();
        }
    }
}
