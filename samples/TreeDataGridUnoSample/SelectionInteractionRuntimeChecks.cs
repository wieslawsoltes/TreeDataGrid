using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls;
using Uno.Controls.Presentation;
using Uno.Controls.Selection;
using IColumns = Uno.Controls.Models.TreeDataGrid.IColumns;

namespace TreeDataGridUnoSample;

/// <summary>Custom interaction visuals and observation lifetime; not OS input evidence.</summary>
internal static class SelectionInteractionRuntimeChecks
{
    public static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        using var source = new FlatTreeDataGridSource<Item>([new("zero"), new("one"), new("two")]);
        source.Columns.Add(new TextColumn<Item, string>("First", item => item.Name, width: new(120)));
        source.Columns.Add(new TextColumn<Item, string>("Second", item => item.Name, width: new(120)));
        var first = new Interaction { Row = 1, CellRow = 2, CellColumn = 1 };
        var policy = new Policy(first);
        var grid = new Uno.Controls.TreeDataGrid
        {
            Width = 360, Height = 180, SelectionMode = TreeDataGridSelectionMode.Source,
            PresentationOptions = policy, Model = source,
        };
        try
        {
            page.Content = grid;
            await Settle();
            var custom = policy.Created ?? throw new InvalidOperationException("The custom presentation policy was not used.");
            Check(first.Subscribers == 1, "A custom interaction was not observed exactly once.");
            Check(grid.TryGetRow(1)?.IsSelected == true && grid.TryGetRow(0)?.IsSelected == false &&
                grid.RowsPresenter!.RealizedCells.All(cell => cell.IsSelected == (cell.RowIndex == 1 || (cell.RowIndex == 2 && cell.ColumnIndex == 1))),
                "Row/cell visuals bypassed the presentation's custom interaction queries.");
            Check(source.RowSelection!.Count == 0, "Custom interaction visuals changed the shared Core selection.");

            first.Row = 0;
            first.CellRow = -1;
            first.Notify();
            Check(grid.TryGetRow(0)?.IsSelected == true && grid.TryGetRow(1)?.IsSelected == false,
                "Custom SelectionChanged did not refresh realized rows.");

            var second = new Interaction { Row = 2 };
            custom.SetInteraction(second);
            Check(first.Subscribers == 0 && second.Subscribers == 1 && grid.TryGetRow(2)?.IsSelected == true,
                "Replacing the presentation interaction retained its previous observer or highlight.");
            first.Row = 1;
            first.Notify();
            Check(grid.TryGetRow(2)?.IsSelected == true && grid.TryGetRow(1)?.IsSelected == false,
                "A retired interaction changed current selection visuals.");

            page.Content = previous;
            await Task.Delay(100);
            Check(second.Subscribers == 0, "Unloading retained the custom selection-interaction observer.");
            page.Content = grid;
            await Settle();
            Check(second.Subscribers == 1 && grid.TryGetRow(2)?.IsSelected == true,
                "Reloading failed to reconnect the current interaction exactly once.");
            custom.SetInteraction(null);
            Check(second.Subscribers == 0 && grid.RowsPresenter!.RealizedRows.All(row => !row.IsSelected) &&
                grid.RowsPresenter!.RealizedCells.All(cell => !cell.IsSelected),
                "Removing the interaction left selection observers or highlights.");
            var disposedWithPublishedProperties = false;
            custom.AfterDispose = () =>
            {
                disposedWithPublishedProperties = true;
                Check(grid.Rows is null && grid.Columns is null && grid.Presentation is null &&
                    grid.GetValue(Uno.Controls.TreeDataGrid.PresentationProperty) is null,
                    "Disposal callbacks observed public properties pointing at the retired presentation.");
            };
            grid.Model = null;
            Check(disposedWithPublishedProperties, "The custom presentation was not disposed.");
            Console.WriteLine("UNO_RUNTIME_SELECTION_INTERACTION_PASSED: custom row/cell queries, replacement, retired notifications and unload/reload subscriptions");
        }
        finally { grid.Model = null; page.Content = previous; }
        async Task Settle() { await Task.Delay(150); grid.UpdateLayout(); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed record Item(string Name);
    private sealed class Interaction : ITreeDataGridSelectionInteraction
    {
        public int Row { get; set; } = -1;
        public int CellRow { get; set; } = -1;
        public int CellColumn { get; set; } = -1;
        public event EventHandler? SelectionChanged;
        public int Subscribers => SelectionChanged?.GetInvocationList().Length ?? 0;
        public bool IsRowSelected(int rowIndex) => rowIndex == Row;
        public bool IsCellSelected(int columnIndex, int rowIndex) => columnIndex == CellColumn && rowIndex == CellRow;
        public void Notify() => SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private sealed class Policy(Interaction interaction) : ITreeDataGridPresentationOptions
    {
        public CustomPresentation? Created { get; private set; }
        public TreeDataGridPresentation Create(ITreeDataGridSource model) => Created = new(model, interaction);
    }
    private sealed class CustomPresentation : TreeDataGridPresentation
    {
        private readonly TreeDataGridPresentation _inner;
        private ITreeDataGridSelectionInteraction? _interaction;
        public Action? AfterDispose { get; set; }
        public CustomPresentation(ITreeDataGridSource model, ITreeDataGridSelectionInteraction interaction)
        { _inner = TreeDataGridPresentation.Create(model); _interaction = interaction; }
        public override ITreeDataGridSource Model => _inner.Model;
        public override IColumns Columns => _inner.Columns;
        public override TreeDataGridSelection Selection => _inner.Selection;
        public override ITreeDataGridSelectionInteraction? SelectionInteraction => _interaction;
        public override event EventHandler? ColumnsChanged { add => _inner.ColumnsChanged += value; remove => _inner.ColumnsChanged -= value; }
        public override event NotifyCollectionChangedEventHandler? RowsChanged { add => _inner.RowsChanged += value; remove => _inner.RowsChanged -= value; }
        public override CellValue RealizeCell(int columnIndex, int rowIndex) => _inner.RealizeCell(columnIndex, rowIndex);
        public override void RecycleCell(CellColumn column, CellValue cell) => _inner.RecycleCell(column, cell);
        public override void Suspend() { base.Suspend(); _inner.Suspend(); }
        public override void Resume() { _inner.Resume(); base.Resume(); }
        public override void Dispose()
        {
            try { base.Dispose(); AfterDispose?.Invoke(); }
            finally { _inner.Dispose(); }
        }
        public void SetInteraction(ITreeDataGridSelectionInteraction? value)
        { _interaction = value; RaisePropertyChanged(new(nameof(SelectionInteraction))); }
    }
}
