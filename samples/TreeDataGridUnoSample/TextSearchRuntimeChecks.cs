using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TreeDataGridCore;
using TreeDataGridCore.Selection;
using Uno.Controls;

namespace TreeDataGridUnoSample;

/// <summary>Exercises committed-text processing in native controls, not OS input delivery.</summary>
internal static class TextSearchRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        var content = page.Content;
        var culture = CultureInfo.CurrentCulture;
        var items = new ObservableCollection<Item>(new[] { new Item("Alpha"), new Item("Alpine"), new Item("Beta") }
            .Concat(Enumerable.Range(0, 1000).Select(x => new Item($"Row {x}"))).Append(new Item("Zulu")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.WithTextColumn(x => x.Name, options => { options.IsTextSearchEnabled = true; options.Width = new(240); });
        var grid = new TextInputGrid { Model = source };
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            page.Content = grid;
            await Task.Delay(150);
            grid.Input("a");
            Check(source.RowSelection!.SelectedIndex == new IndexPath(0), "First character did not select the first matching Core model.");
            grid.Input("a");
            Check(source.RowSelection.SelectedIndex == new IndexPath(1), "Repeated character did not cycle.");
            grid.Input("l");
            Check(source.RowSelection.SelectedIndex == new IndexPath(1), "Extended candidate skipped its current matching row.");
            grid.Input("x");
            grid.Input("p");
            Check(source.RowSelection.SelectedIndex == new IndexPath(1), "A failed candidate poisoned subsequent input.");
            await Task.Delay(510);
            grid.Input("z");
            grid.UpdateLayout();
            Check(source.RowSelection.SelectedIndex == new IndexPath(items.Count - 1) && grid.TryGetRow(items.Count - 1) is not null,
                "Searching an unrealized model failed to select and realize its row.");
            grid.Model = null;
            grid.Model = source;
            source.RowSelection.Clear();
            await Task.Delay(100);
            void Cancel(object? sender, CancelEventArgs e) => e.Cancel = true;
            grid.SelectionChanging += Cancel;
            try { grid.Input("b"); }
            finally { grid.SelectionChanging -= Cancel; }
            Check(source.RowSelection.Count == 0, "Text search ignored SelectionChanging cancellation.");
            grid.SelectionMode = TreeDataGridSelectionMode.Cell;
            grid.Input("b");
            Check(((ITreeDataGridCellSelectionModel<Item>)source.Selection!).Count == 0, "Row text search modified a cell-selection model.");
            grid.SelectionMode = TreeDataGridSelectionMode.Row;
            void Replace(object? sender, CancelEventArgs e) => grid.Model = null;
            grid.SelectionChanging += Replace;
            try { grid.Input("b"); }
            finally { grid.SelectionChanging -= Replace; }
            Check(grid.Presentation is null && source.RowSelection!.Count == 0, "Text search mutated the retired source after a user callback.");
            Console.WriteLine("UNO_RUNTIME_TEXT_SEARCH_PASSED: committed-text prefix/cycling, failed match, timeout, virtualized row, cancellation, cell-selection exclusion and source replacement; OS delivery is a separate gate");
        }
        finally
        {
            grid.Model = null;
            page.Content = content;
            CultureInfo.CurrentCulture = culture;
        }
    }
    private sealed class TextInputGrid : Uno.Controls.TreeDataGrid
    {
        public void Input(string text) => OnTextInput(text);
    }
    private sealed record Item(string Name);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
