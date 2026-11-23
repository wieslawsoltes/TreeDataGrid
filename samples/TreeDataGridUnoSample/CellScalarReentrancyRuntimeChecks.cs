using System;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using ITextCell = Uno.Controls.Models.TreeDataGrid.ITextCell;

namespace TreeDataGridUnoSample;

/// <summary>Scalar facades must not write through obsolete permission/value getters.</summary>
internal static class CellScalarReentrancyRuntimeChecks
{
    internal static void Run()
    {
        CheckBoxRefresh(false);
        CheckBoxRefresh(true);
        CheckBoxPermission(false);
        CheckBoxPermission(true);
        NestedCheckBoxAssignment();
        TextPermission(false);
        TextPermission(true);
        NestedTextAssignment();
        RejectDirectCheckBoxAdoption();
        Console.WriteLine("UNO_RUNTIME_CELL_SCALAR_REENTRANCY_PASSED: cases=9; same-model/nested checkbox refresh, text/checkbox permission retirement, nested scalar write ordering, direct adoption rejection and subsequent writes");
    }

    private static void CheckBoxRefresh(bool nested)
    {
        var factory = new TreeDataGridElementFactory();
        var model = new CheckValue();
        var cell = new CheckProbe();
        cell.Realize(factory, null, model, 0, 0);
        try
        {
            model.Reading = () =>
            {
                model.Reading = null;
                model.Content = true;
                if (nested) cell.Refresh();
                else { cell.Unrealize(); cell.Realize(factory, null, model, 0, 0); }
            };
            cell.Refresh();
            Check(cell.Value == true && ReferenceEquals(cell.Model, model) && model.Writes == 0,
                "An obsolete checkbox getter republished its old scalar or wrote a model.");
        }
        finally { model.Reading = null; cell.Unrealize(); }
    }

    private static void CheckBoxPermission(bool sameModel)
    {
        var factory = new TreeDataGridElementFactory();
        var original = new CheckValue();
        var replacement = sameModel ? original : new CheckValue();
        var cell = new CheckProbe();
        cell.Realize(factory, null, original, 0, 0);
        try
        {
            original.CheckingWrite = () =>
            {
                original.CheckingWrite = null;
                cell.Unrealize();
                cell.Realize(factory, null, replacement, 0, 0);
            };
            cell.Value = true;
            Check(original.Writes == 0 && replacement.Writes == 0 && cell.Value == false &&
                ReferenceEquals(cell.Model, replacement), "Retired checkbox permission committed an obsolete scalar write.");
            cell.Value = true;
            Check(replacement.Writes == 1 && replacement.Content == true, "A current checkbox scalar write did not work.");
        }
        finally { original.CheckingWrite = null; cell.Unrealize(); }
    }

    private static void NestedCheckBoxAssignment()
    {
        var factory = new TreeDataGridElementFactory();
        var model = new CheckValue();
        var cell = new CheckProbe();
        cell.Realize(factory, null, model, 0, 0);
        try
        {
            model.CheckingWrite = () => { model.CheckingWrite = null; cell.Value = null; };
            cell.Value = true;
            Check(model.Writes == 1 && model.Content is null && cell.Value is null,
                "The older checkbox assignment overwrote a newer nested assignment.");
            cell.Value = false;
            Check(model.Writes == 2 && model.Content == false, "The checkbox write guard prevented later writes.");
        }
        finally { model.CheckingWrite = null; cell.Unrealize(); }
    }

    private static void TextPermission(bool sameModel)
    {
        var factory = new TreeDataGridElementFactory();
        var original = new TextValue();
        var replacement = sameModel ? original : new TextValue();
        var cell = new TreeDataGridTextCell();
        cell.Realize(factory, null, original, 0, 0);
        try
        {
            original.CheckingEdit = () =>
            {
                original.CheckingEdit = null;
                cell.Unrealize();
                cell.Realize(factory, null, replacement, 0, 0);
            };
            cell.Value = "obsolete";
            Check(original.Writes == 0 && replacement.Writes == 0 && cell.Value == "initial" &&
                ReferenceEquals(cell.Model, replacement), "Retired text permission committed an obsolete scalar write.");
            cell.Value = "current";
            Check(replacement.Writes == 1 && replacement.Content == "current", "A current text scalar write did not work.");
        }
        finally { original.CheckingEdit = null; cell.Unrealize(); }
    }

    private static void NestedTextAssignment()
    {
        var factory = new TreeDataGridElementFactory();
        var model = new TextValue();
        var cell = new TreeDataGridTextCell();
        cell.Realize(factory, null, model, 0, 0);
        try
        {
            model.CheckingEdit = () => { model.CheckingEdit = null; cell.Value = "newest"; };
            cell.Value = "older";
            Check(model.Writes == 1 && model.Content == "newest" && cell.Value == "newest",
                "The older text assignment overwrote a newer nested assignment.");
            cell.Value = "later";
            Check(model.Writes == 2 && model.Content == "later", "The text write guard prevented later writes.");
        }
        finally { model.CheckingEdit = null; cell.Unrealize(); }
    }

    private static void RejectDirectCheckBoxAdoption()
    {
        var factory = new TreeDataGridElementFactory();
        var model = new CheckValue();
        var replacement = new CheckValue();
        var cell = new CheckProbe();
        var column = new ProbeColumn();
        var row = new ProbeRow();
        var calls = 0;
        replacement.CheckingWrite = () => ++calls;
        cell.Realize(factory, null, model, 0, 0);
        cell.IsCurrent = true;
        var token = cell.RegisterPropertyChangedCallback(TreeDataGridCell.IsCurrentProperty, (_, _) =>
        {
            if (cell.IsCurrent) return;
            Exception? failure = null;
            try { cell.Realize(column, replacement, row, 0, 0, null); }
            catch (Exception error) { failure = error; }
            Check(failure is InvalidOperationException && calls == 0,
                "Direct checkbox adoption evaluated model metadata before rejecting unfinished retirement.");
        });
        try { cell.Unrealize(); }
        finally { cell.UnregisterPropertyChangedCallback(TreeDataGridCell.IsCurrentProperty, token); cell.Unrealize(); }
        Check(cell.Model is null && cell.Value is null && cell.Visibility == Visibility.Collapsed,
            "Rejected direct checkbox adoption retained state.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed partial class CheckProbe : TreeDataGridCheckBoxCell { internal void Refresh() => base.UpdateValue(); }
    private sealed class CheckValue : CellValue
    {
        internal bool? Content = false;
        internal Action? Reading;
        internal Action? CheckingWrite;
        internal int Writes;
        public override CellKind ContentKind => CellKind.CheckBox;
        public override bool? IsThreeState => true;
        public override object? Value { get { var result = Content; Reading?.Invoke(); return result; } }
        public override bool CanEdit => false;
        public override bool CanWrite { get { CheckingWrite?.Invoke(); return true; } }
        public override void Write(object? value) { ++Writes; Content = (bool?)value; }
    }
    private sealed class TextValue : CellValue, ITextCell
    {
        internal string? Content = "initial";
        internal Action? CheckingEdit;
        internal int Writes;
        public string? Text { get => Content; set { ++Writes; Content = value; } }
        public override object? Value => Content;
        public override bool CanEdit { get { CheckingEdit?.Invoke(); return true; } }
        public TextAlignment TextAlignment => TextAlignment.Left;
        public TextWrapping TextWrapping => TextWrapping.NoWrap;
        public TextTrimming TextTrimming => TextTrimming.None;
        public override void Write(object? value) => Text = (string?)value;
    }
    private sealed class ProbeColumn() : CellColumn(ValueColumn<object, object?>.FromDelegate(null, static row => row))
    {
        public override CellValue CreateCell(IRow row) => throw new NotSupportedException();
    }
    private sealed class ProbeRow : IRow
    {
        public object? Header => null;
        public object? Model { get; } = new object();
        public TreeDataGridCore.GridLength Height { get; set; } = TreeDataGridCore.GridLength.Auto;
    }
}
