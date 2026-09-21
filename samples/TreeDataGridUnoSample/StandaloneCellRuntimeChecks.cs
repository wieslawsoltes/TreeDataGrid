using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Primitives;
using Uno.Controls.Selection;

namespace TreeDataGridUnoSample;

/// <summary>Deferred native checks of the reference-compatible cell entry point.</summary>
internal static class StandaloneCellRuntimeChecks
{
    public static void Run(DataTemplate template)
    {
        var factory = new TreeDataGridElementFactory();
        var selection = new Selection();
        var model = new TextModel("Standalone");
        var cell = new CustomTextCell();
        try
        {
            cell.Realize(factory, selection, model, 2, 3);
            Check(cell.Calls == 1 && cell.ColumnIndex == 2 && cell.RowIndex == 3 && cell.IsSelected &&
                ReferenceEquals(cell.Model, model) && cell.Value == "Standalone", "Standalone realization lost its model, indexes or selection.");
            Check(cell.TextAlignment == TextAlignment.Right && cell.TextWrapping == TextWrapping.Wrap && cell.TextTrimming == TextTrimming.None,
                "Standalone text realization bypassed specialized presentation setup.");
            Check(model.Subscribers == 1, "Standalone realization installed duplicate model subscriptions.");
            model.Text = "External change";
            Check(cell.Value == model.Text, "Standalone observation did not refresh the text facade.");
            cell.Value = "Control write";
            Check(model.Text == "Control write", "Standalone Value did not write to its borrowed model.");
            cell.Unrealize();
            Check(model.Subscribers == 0 && model.Disposals == 0 && cell.Model is null && cell.RowIndex == -1,
                "Standalone cleanup retained observers or disposed the caller-owned model.");

            cell.Realize(factory, null, model, 0, 0);
            Check(cell.Calls == 2 && !cell.IsSelected && model.Subscribers == 1,
                "A reused standalone cell duplicated realization or retained selection.");
            cell.BeginRebind();
            cell.Unrealize();
            model.Text = "Replacement";
            cell.Realize(factory, null, model, 1, 1);
            cell.EndRebind(true);
            Check(cell.Calls == 3 && cell.Value == "Replacement" && model.Subscribers == 1,
                "Standalone synchronous rebind did not restore the new value and one subscription.");
        }
        finally { cell.Unrealize(); }
        Check(model.Subscribers == 0 && model.Disposals == 0, "Final standalone cleanup retained or disposed the model.");

        var selectionFailure = new InvalidOperationException("Selection query failed.");
        Exception? selectionError = null;
        try { cell.Realize(factory, new Selection(() => throw selectionFailure), model, 0, 0); }
        catch (InvalidOperationException error) { selectionError = error; }
        Check(ReferenceEquals(selectionError, selectionFailure) && model.Subscribers == 0 && model.Disposals == 0 && cell.Model is null,
            "A failed selection query retained a standalone observer or disposed the borrowed model.");

        var checkModel = new CheckBoxCell(true);
        var check = new TreeDataGridCheckBoxCell();
        check.Realize(factory, null, checkModel, 0, 0);
        Check(check.Value == true && check.IsReadOnly && ReferenceEquals(check.Model, checkModel),
            "Standalone checkbox realization lost its scalar/read-only contract.");
        check.Unrealize();
        checkModel.Dispose();

        var content = new object();
        var templateModel = new TemplateCell(content, _ => template, null, null);
        var templateCell = new TreeDataGridTemplateCell();
        templateCell.Realize(factory, null, templateModel, 0, 0);
        Check(ReferenceEquals(templateCell.Model, templateModel) && ReferenceEquals(templateCell.DataContext, templateModel) &&
            ReferenceEquals(templateCell.Content, content) && ReferenceEquals(templateCell.ContentTemplate, template),
            "Standalone template realization lost its template, content or model context.");
        templateCell.Unrealize();
        Check(templateCell.DataContext is null && templateCell.Content is null, "Standalone template cleanup retained its content.");

        var failure = new InvalidOperationException("Template resolution failed.");
        var failing = new ThrowingCell();
        var failed = new TreeDataGridTemplateCell();
        failing.Failure = failure;
        Exception? received = null;
        try { failed.Realize(factory, null, failing, 0, 0); }
        catch (InvalidOperationException error) { received = error; }
        Check(ReferenceEquals(received, failure) && failed.Model is null && failed.RowIndex == -1 && failing.Disposals == 0,
            "Failed standalone realization lost its error, retained state or disposed borrowed data.");

        var rowModel = new RowModel("View row");
        using var source = new TreeDataGridCore.FlatTreeDataGridSource<RowModel>([rowModel]);
        source.Columns.Add(new TreeDataGridCore.Models.TextColumn<RowModel, string>("Name", x => x.Name, (x, value) => x.Name = value));
        using var presentation = Uno.Controls.Presentation.TreeDataGridPresentation.Create(source);
        ITreeDataGridRows rows = presentation.Rows;
        var publicModel = rows.RealizeCell(presentation.Columns[0], 0, 0);
        var publicControl = new TreeDataGridTextCell();
        try
        {
            publicControl.Realize(factory, null, publicModel, 0, 0);
            Check(ReferenceEquals(rows[0], source.Rows[0]) && ReferenceEquals(rows[0].Model, rowModel) && publicControl.Value == "View row",
                "Public row realization copied the Core row or failed to initialize the native cell.");
            publicControl.Value = "Public row write";
            Check(rowModel.Name == "Public row write", "A cell realized through ITreeDataGridRows did not write to the original Core model.");
            publicControl.Unrealize();
            Check(rowModel.Subscribers > 0, "The control disposed its borrowed public row cell before the caller released it.");
        }
        finally
        {
            try { publicControl.Unrealize(); }
            finally { rows.UnrealizeCell(publicModel, 0, 0); }
        }
        Check(rowModel.Subscribers == 0, "Public row cell cleanup retained its model binding.");

        Console.WriteLine("UNO_RUNTIME_STANDALONE_CELLS_PASSED: compatible overrides, borrowed models, observation, scalar writeback, selection, rebind, templates, failure cleanup");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed partial class CustomTextCell : TreeDataGridTextCell
    {
        public int Calls { get; private set; }
        public override void Realize(TreeDataGridElementFactory factory, ITreeDataGridSelectionInteraction? selection,
            ICell model, int columnIndex, int rowIndex)
        {
            ++Calls;
            base.Realize(factory, selection, model, columnIndex, rowIndex);
        }
    }
    private sealed class Selection(Action? queried = null) : ITreeDataGridSelectionInteraction
    {
        public event EventHandler? SelectionChanged { add { } remove { } }
        public bool IsCellSelected(int columnIndex, int rowIndex)
        {
            queried?.Invoke();
            return columnIndex == 2 && rowIndex == 3;
        }
    }
    private sealed class TextModel(string text) : ITextCell, INotifyPropertyChanged, IDisposable
    {
        private string? _text = text;
        private PropertyChangedEventHandler? _changed;
        public int Subscribers { get; private set; }
        public int Disposals { get; private set; }
        public bool CanEdit => true;
        public BeginEditGestures EditGestures => BeginEditGestures.Default;
        public object? Value => _text;
        public string? Text { get => _text; set { _text = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public TextAlignment TextAlignment => TextAlignment.Right;
        public TextWrapping TextWrapping => TextWrapping.Wrap;
        public TextTrimming TextTrimming => TextTrimming.None;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
        public void Dispose() => ++Disposals;
    }
    private sealed class ThrowingCell : Uno.Controls.Presentation.CellValue
    {
        public Exception Failure { get; set; } = new InvalidOperationException();
        public int Disposals { get; private set; }
        public override object? Value => null;
        public override bool CanEdit => false;
        public override DataTemplate? GetCellTemplate(Microsoft.UI.Xaml.Controls.Control anchor) => throw Failure;
        public override void Write(object? value) => throw new NotSupportedException();
        public override void Dispose() => ++Disposals;
    }
    private sealed class RowModel(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        private PropertyChangedEventHandler? _changed;
        public int Subscribers { get; private set; }
        public string Name { get => _name; set { _name = value; _changed?.Invoke(this, new(nameof(Name))); } }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
}
