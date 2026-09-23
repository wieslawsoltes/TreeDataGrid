using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using Uno.Controls.Presentation;
using Native = Uno.Controls.Models.TreeDataGrid;
using NativeGrid = Uno.Controls.TreeDataGrid;
using IndexPath = TreeDataGridCore.IndexPath;
using SelectionArgs = Uno.Controls.TreeDataGridSelectionChangedEventArgs;

namespace TreeDataGridUnoSample;

/// <summary>Exercises audit-discovered contracts through public native and trimmed-consumer APIs.</summary>
internal static class ParityContractRuntimeChecks
{
    internal static async Task RunTextOptionsAsync(MainPage page)
    {
        var previous = page.Content;
        var model = new NumberRow { Number = 12.5m };
        using var source = new FlatTreeDataGridSource<NumberRow>([model, new() { Number = 4m }]);
        source.Columns.Add(new TextColumn<NumberRow, decimal>("Number", row => row.Number,
            (row, value) => row.Number = value, width: new(240)) { PresentationKey = "LiveOptions" });
        var options = new Native.TextColumnOptions<NumberRow>
        {
            StringFormat = "N={0:F1}", Culture = CultureInfo.InvariantCulture,
        };
        var factories = new TreeDataGridPresentationOptions<NumberRow>();
        factories.Columns["LiveOptions"] = definition => new Native.TextColumn<NumberRow, decimal>(
            (ValueColumn<NumberRow, decimal>)definition, options);
        var grid = new NativeGrid { Width = 420, Height = 180, PresentationOptions = factories, Model = source };
        try
        {
            page.Content = grid;
            await Settle(grid);
            var cell = grid.TryGetCell(0, 0) as Uno.Controls.Primitives.TreeDataGridCell ??
                throw new InvalidOperationException("Live-options cell was not realized.");
            Check(RenderedText(cell) == "N=12.5", "Initial text options did not render.");
            options.StringFormat = "V={0:F2}";
            options.Culture = CultureInfo.GetCultureInfo("fr-FR");
            options.TextAlignment = TextAlignment.Right;
            options.IsTextSearchEnabled = true;
            // The reference options object has no change event. A normal model
            // notification refreshes the retained control; no timer applies options.
            model.Number = 12.75m;
            await Settle(grid);
            Check(ReferenceEquals(cell, grid.TryGetCell(0, 0)) && RenderedText(cell) == "V=12,75",
                "The retained native cell used its obsolete construction-time format/culture.");
            Check(((Native.ITextCell)cell.Model!).TextAlignment == TextAlignment.Right &&
                ((CellColumn)grid.Presentation!.Columns[0]).IsTextSearchEnabled,
                "Live text/search metadata was frozen at construction.");
            Check(grid.BeginEdit(0, 0), "The live-options column could not begin editing.");
            var editor = ShowcaseRuntimeChecks.Descendants(grid.EditingCell!).OfType<TextBox>().Single();
            editor.Text = "21,5";
            Check(grid.CommitEdit() && model.Number == 21.5m, "Editing ignored the current French parsing culture.");
            await Settle(grid);
            Check(ReferenceEquals(cell, grid.TryGetCell(0, 0)) && RenderedText(cell) == "V=21,50",
                "Committed text did not retain the cell or current formatting.");
            grid.Model = null;
            Check(model.Subscribers == 0 && source.Rows.Count == 2,
                "Retiring the grid retained a model binding or disposed the borrowed Core source.");
            Console.WriteLine("UNO_RUNTIME_MUTABLE_TEXT_OPTIONS_PASSED: retained native text refresh, current format/culture, live alignment/search metadata, native editor parsing, identity and subscription cleanup");
        }
        finally { grid.Model = null; page.Content = previous; }
    }

    internal static async Task RunSelectionHooksAsync(MainPage page)
    {
        var previous = page.Content;
        var models = new[] { new NumberRow(), new NumberRow() };
        using var source = new FlatTreeDataGridSource<NumberRow>(models);
        source.Columns.Add(new TextColumn<NumberRow, decimal>("Number", row => row.Number, width: new(240)));
        var factory = new ProbeOptions();
        var grid = new NativeGrid { Width = 420, Height = 180, PresentationOptions = factory, Model = source };
        var observed = new List<SelectionArgs>();
        EventHandler<SelectionArgs> listener = (sender, args) =>
        {
            Check(ReferenceEquals(sender, grid), "A native grid selection event exposed a different sender.");
            observed.Add(args);
        };
        grid.SelectionChanged += listener;
        try
        {
            page.Content = grid;
            await Settle(grid);
            var presentation = factory.Current!;
            observed.Clear();
            Check(grid.SelectCell(1, 0), "The wrapped public presentation could not select a Core row.");
            Check(observed.Count == 1 && observed[0].SelectedIndexes.Single() == new IndexPath(1),
                "Built-in Core selection was lost or duplicated by the hook integration.");
            presentation.Publish(new SelectionArgs(selectedIndexes: [new(0)], selectedItems: [models[0]]));
            Check(observed.Count == 2 && observed[1].SelectedIndexes.Single() == new IndexPath(0) &&
                ReferenceEquals(observed[1].SelectedItems.Single(), models[0]),
                "The protected row-selection hook was disconnected from the native grid.");
            presentation.PublishCells([new(0, new(1))], [new(0, new(0))]);
            Check(observed.Count == 3 && observed[2].SelectedCellIndexes.Single() == new CellIndex(0, new(0)) &&
                observed[2].SelectedItems.Count == 0 && observed[2].SelectedIndexes.Count == 0,
                "The protected cell hook was disconnected or fabricated row deltas.");

            grid.SelectionChanged -= listener;
            presentation.PublishCells(UnexpectedEnumeration(), UnexpectedEnumeration());
            Check(observed.Count == 3, "An unsubscribed hook still reached the native listener.");
            grid.SelectionChanged += listener;
            presentation.Suspend();
            presentation.PublishCells(UnexpectedEnumeration(), UnexpectedEnumeration());
            presentation.Resume();
            presentation.PublishCells([], []);
            Check(observed.Count == 4, "Resume did not restore a single native publication path.");
            grid.Model = null;
            presentation.PublishCells(UnexpectedEnumeration(), UnexpectedEnumeration());
            Check(observed.Count == 4 && source.Rows.Count == 2,
                "A retired presentation published into its old grid or disposed the source.");
            Console.WriteLine("UNO_RUNTIME_PRESENTATION_SELECTION_HOOKS_PASSED: actual custom presentation, single Core event, protected row/cell hooks, native sender and model identity, lazy unsubscription, suspension/resume and retired-source isolation");
        }
        finally { grid.SelectionChanged -= listener; grid.Model = null; page.Content = previous; }
    }

    private static IEnumerable<CellIndex> UnexpectedEnumeration()
    {
        yield return RejectEnumeration();
    }
    private static CellIndex RejectEnumeration() =>
        throw new InvalidOperationException("An inactive/unobserved hook evaluated its arguments.");
    private static string? RenderedText(FrameworkElement cell) => ShowcaseRuntimeChecks.Descendants(cell)
        .OfType<TextBlock>().FirstOrDefault(text => text.Visibility == Visibility.Visible)?.Text;
    private static async Task Settle(NativeGrid grid) { await Task.Delay(100); grid.UpdateLayout(); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class NumberRow : INotifyPropertyChanged
    {
        private decimal _number;
        private PropertyChangedEventHandler? _changed;
        private static readonly PropertyChangedEventArgs NumberChanged = new(nameof(Number));
        internal int Subscribers;
        public decimal Number
        {
            get => _number;
            set { if (_number == value) return; _number = value; _changed?.Invoke(this, NumberChanged); }
        }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
    private sealed class ProbeOptions : ITreeDataGridPresentationOptions
    {
        internal ProbePresentation? Current;
        public TreeDataGridPresentation Create(ITreeDataGridSource model) => Current = new(TreeDataGridPresentation.Create(model));
    }
    private sealed class ProbePresentation(TreeDataGridPresentation inner) : TreeDataGridPresentation
    {
        public void Publish(TreeSelectionModelSelectionChangedEventArgs args) => RaiseNativeSelectionChanged(args);
        public void PublishCells(IEnumerable<CellIndex> previous, IEnumerable<CellIndex> next) => RaiseNativeCellSelectionChanged(previous, next);
        public override ITreeDataGridSource Model => inner.Model;
        public override Native.IColumns Columns => inner.Columns;
        public override TreeDataGridSelection Selection => inner.Selection;
        public override CellValue RealizeCell(int columnIndex, int rowIndex) => inner.RealizeCell(columnIndex, rowIndex);
        public override void RecycleCell(CellColumn column, CellValue cell) => inner.RecycleCell(column, cell);
        public override event EventHandler? ColumnsChanged { add => inner.ColumnsChanged += value; remove => inner.ColumnsChanged -= value; }
        public override event NotifyCollectionChangedEventHandler? RowsChanged { add => inner.RowsChanged += value; remove => inner.RowsChanged -= value; }
        public override void Suspend() { base.Suspend(); inner.Suspend(); }
        public override void Resume() { base.Resume(); inner.Resume(); }
        public override void Dispose() { base.Dispose(); inner.Dispose(); }
    }
}
