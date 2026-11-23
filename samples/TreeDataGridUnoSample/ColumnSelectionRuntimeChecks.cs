using System;
using System.Linq;
using System.Threading.Tasks;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Uno.Controls.Selection;
using NativeGrid = Uno.Controls.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Public column-selection APIs over the actual rendered view's columns.</summary>
internal static class ColumnSelectionRuntimeChecks
{
    public static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        using var source = new FlatTreeDataGridSource<Item>([new("First row"), new("Second row")]);
        source.Columns.Add(new TextColumn<Item, string>("First", item => item.Name, width: new(120)));
        source.Columns.Add(new TextColumn<Item, string>("Second", item => item.Name, width: new(120)));
        source.Columns.Add(new TextColumn<Item, string>("Third", item => item.Name, width: new(120)));
        source.RowSelection!.SelectedIndex = new IndexPath(1);
        var grid = new NativeGrid { Width = 420, Height = 180, Model = source };
        var other = new NativeGrid { Model = source };
        TreeDataGridColumnSelectionModel? selection = null;
        try
        {
            page.Content = grid;
            await Task.Delay(150);
            grid.UpdateLayout();
            Check(grid.TryGetCell(0, 0) is not null && grid.TryGetCell(2, 1) is not null,
                "The column-selection consumer did not render all test columns.");
            var columns = grid.Presentation!.Columns;
            selection = new(columns) { SingleSelect = false };
            ITreeDataGridColumnSelectionModel contract = selection;
            Check(ReferenceEquals(contract.Source, columns) && contract.Count == 0,
                "Column selection did not borrow the actual native view column collection.");
            contract.SelectAll();
            Check(contract.Count == 3 && contract.SelectedIndexes.SequenceEqual(new[] { 0, 1, 2 }) &&
                ReferenceEquals(contract.SelectedItems[0], columns[0]) && ReferenceEquals(contract.SelectedItems[2], columns[2]),
                "Range selection copied, lost or replaced actual native columns.");
            contract.SelectedItem = columns[1];
            Check(contract.Count == 1 && contract.SelectedIndex == 1 && ReferenceEquals(contract.SelectedItem, columns[1]),
                "Typed selected-item assignment did not update the shared flat selection engine.");
            var events = 0;
            selection.SelectionChanged += (_, _) => ++events;
            contract.BeginBatchUpdate();
            try { contract.Clear(); contract.Select(0); contract.Select(2); }
            finally { contract.EndBatchUpdate(); }
            Check(events == 1 && contract.SelectedIndexes.SequenceEqual(new[] { 0, 2 }),
                "The native column consumer lost transactional range/event behavior.");
            ((CellColumn)columns[1]).Header = "Native heading";
            grid.UpdateLayout();
            Check(grid.ColumnHeadersPresenter!.RealizedHeaders.Any(header => Equals(header.Content, "Native heading")),
                "The selected-column source was disconnected from the rendered native header.");
            Check(!ReferenceEquals(columns[1], other.Presentation!.Columns[1]) &&
                Equals(other.Presentation.Columns[1].Header, "Second"),
                "Independent grids unexpectedly shared view-owned column metadata.");
            Check(source.RowSelection.SelectedIndex == new IndexPath(1) && source.Rows.Count == 2,
                "Standalone column selection replaced or mutated Core row selection/data.");
            contract.Clear();
            contract.Source = null;
            Check(contract.Count == 0 && columns.Count == 3 && grid.TryGetCell(1, 1) is not null,
                "Detaching column selection disposed the borrowed view or its columns.");
            Console.WriteLine("UNO_RUNTIME_COLUMN_SELECTION_PASSED: actual rendered columns, shared Core flat engine, typed identity, range/batch events, isolated view metadata, row-selection independence and borrowed-source cleanup");
        }
        finally
        {
            if (selection is not null) selection.Source = null;
            grid.Model = null;
            other.Model = null;
            page.Content = previous;
        }
    }
    private sealed record Item(string Name);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
