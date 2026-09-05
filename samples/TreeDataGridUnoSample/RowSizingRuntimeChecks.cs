using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using TreeDataGridCore.Models;

namespace TreeDataGridUnoSample;

internal static class RowSizingRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid, DataTemplate wrapping)
    {
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 150).Select(i => new Item($"Row {i:000}",
            i == 0 ? "Short text" : string.Join(" ", Enumerable.Repeat("Variable height content", 2 + i % 7)))));
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name, width: new(150)));
        source.Columns.Add(new TemplateColumn<Item>("Wrapping content", "Wrapping", width: new(240)));
        var previousHeight = grid.RowHeight;
        grid.RowHeight = double.NaN;
        grid.CellTemplates["Wrapping"] = wrapping;
        try
        {
            grid.Model = source;
            grid.Scroll.ChangeView(0, 0, null, true);
            await Task.Delay(250);
            var presenter = grid.RowsPresenter;
            Check(presenter.GetRowHeight(1) > presenter.GetRowHeight(0), "Wrapping content did not produce variable row heights.");
            VerifyRows(grid);
            var tall = presenter.GetRowHeight(1);
            source.Columns[1].Width = new(420);
            await Task.Delay(200);
            Check(presenter.GetRowHeight(1) < tall, "Widening a wrapping column did not shrink its measured rows.");
            source.Columns[1].Width = new(240);
            await Task.Delay(150);
            items[1].Text = "Shorter";
            await Task.Delay(150);
            Check(presenter.GetRowHeight(1) <= presenter.GetRowHeight(0) + 1, "A shorter live value left an obsolete row height.");

            grid.Scroll.ChangeView(null, presenter.GetRowStart(50) + 5, null, true);
            await Task.Delay(200);
            var anchorRow = presenter.GetRowAt(grid.Scroll.VerticalOffset);
            var anchorModel = source.Rows[anchorRow].Model;
            var relative = presenter.GetRowStart(anchorRow) - grid.Scroll.VerticalOffset;
            items.Insert(0, new("Inserted", "An inserted row above the viewport"));
            await Task.Delay(200);
            var nextAnchor = presenter.GetRowAt(grid.Scroll.VerticalOffset);
            Check(ReferenceEquals(source.Rows[nextAnchor].Model, anchorModel), "Insertion above the viewport changed the anchored model.");
            Check(Math.Abs(presenter.GetRowStart(nextAnchor) - grid.Scroll.VerticalOffset - relative) < 1, "Insertion changed the intra-row scroll anchor.");
            items.RemoveAt(0);
            await Task.Delay(200);
            nextAnchor = presenter.GetRowAt(grid.Scroll.VerticalOffset);
            Check(ReferenceEquals(source.Rows[nextAnchor].Model, anchorModel), "Removal above the viewport changed the anchored model.");

            Check(grid.BringCellIntoView(120, 1), "Variable-height bring-into-view failed.");
            await Task.Delay(200);
            Check(presenter.RealizedCells.Any(x => x.RowIndex == 120), "Bring-into-view did not realize the requested variable-height row.");
            Check(presenter.GetRowStart(120) >= grid.Scroll.VerticalOffset - 1 &&
                presenter.GetRowStart(120) + presenter.GetRowHeight(120) <= grid.Scroll.VerticalOffset + grid.Scroll.ViewportHeight + 1,
                "Bring-into-view did not fit the measured target in the viewport.");
            VerifyRows(grid);
            Check(presenter.RealizedCells.Count < 100, "Variable-height layout realized the entire source.");

            items[120].Text = string.Join(" ", Enumerable.Repeat("A tall row keeps its intra-row anchor", 60));
            await Task.Delay(150);
            grid.Scroll.ChangeView(null, presenter.GetRowStart(120) + 70, null, true);
            await Task.Delay(150);
            source.Columns[1].Width = new(300);
            await Task.Delay(200);
            Check(presenter.GetRowAt(grid.Scroll.VerticalOffset) == 120 &&
                Math.Abs(grid.Scroll.VerticalOffset - presenter.GetRowStart(120) - 70) < 1,
                "Resizing lost the anchor inside a row taller than its initial estimate.");
            Check(grid.BringCellIntoView(149, 1), "Last-row bring-into-view failed.");
            await Task.Delay(200);
            Check(presenter.RealizedCells.Any(x => x.RowIndex == 149) &&
                presenter.GetRowStart(149) + presenter.GetRowHeight(149) <= grid.Scroll.VerticalOffset + grid.Scroll.ViewportHeight + 1,
                "The last variable-height row was not brought fully into view.");

            grid.RowHeight = 40;
            await Task.Delay(150);
            Check(presenter.GetRowHeight(120) == 40 && presenter.GetRowStart(120) == 4800, "Explicit fixed row heights were not honored.");
            grid.Model = null;
            Check(presenter.RealizedCells.Count == 0, "Source removal retained variable-height cells.");
            var invalidHeightThrew = false;
            try { grid.RowHeight = -1; } catch (ArgumentOutOfRangeException) { invalidHeightThrew = true; }
            Check(invalidHeightThrew && grid.RowHeight == 40, "Invalid row height left the dependency property in a broken state.");
        }
        finally { grid.Model = null; grid.RowHeight = previousHeight; }
        Console.WriteLine("UNO_RUNTIME_ROW_SIZING_PASSED: wrapping heights, width/live-value shrink, insertion/removal anchors, variable bring-into-view, bounded realization, fixed-height mode, source removal");
    }
    private static void VerifyRows(Uno.Controls.TreeDataGrid grid)
    {
        foreach (var group in grid.RowsPresenter.RealizedCells.GroupBy(x => x.RowIndex))
        {
            var expected = grid.RowsPresenter.GetRowHeight(group.Key);
            foreach (var cell in group)
                Check(Math.Abs(cell.ActualHeight - expected) < 1, "Cells in one row were arranged at different heights.");
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Item(string name, string text) : INotifyPropertyChanged
    {
        private string _text = text;
        public string Name { get; } = name;
        public string Text { get => _text; set { _text = value; PropertyChanged?.Invoke(this, new(nameof(Text))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
