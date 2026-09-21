using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Deferred native focus/Tab/virtualization checks; not an OS-keyboard transport test.</summary>
internal static class FocusRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        var previousContent = page.Content;
        var items = Enumerable.Range(0, 200).Select(i => new Item($"Row {i:000}")).ToArray();
        using var source = new FlatTreeDataGridSource<Item>(items);
        for (var i = 0; i < 8; ++i)
            source.Columns.Add(new TextColumn<Item, string>($"Column {i}", x => x.Name, width: new(150)));
        var before = new Button { Content = "Before grid" };
        var after = new Button { Content = "After grid" };
        var grid = new Uno.Controls.TreeDataGrid { Width = 480, Height = 240, RowHeight = 26, Model = source };
        var host = new StackPanel { Children = { before, grid, after } };
        try
        {
            page.Content = host;
            await Settle();
            var initial = (TreeDataGridCell)grid.TryGetCell(1, 1)!;
            Check(initial.IsTabStop && initial.Focus(FocusState.Keyboard), "Default cells did not accept native keyboard focus.");
            Check(grid.SelectCell(1, 1) && grid.MoveSelection(TreeDataGridNavigation.Down), "Focused row navigation failed.");
            var cell = (TreeDataGridCell)grid.TryGetCell(1, 2)!;
            Check(ReferenceEquals(FocusManager.GetFocusedElement(grid.XamlRoot ?? throw new InvalidOperationException("The focus fixture must be attached to a XamlRoot.")), cell),
                "Vertical row navigation did not focus the matching column in the next row.");
            Check(ReferenceEquals(Next(FocusNavigationDirection.Next), grid.TryGetCell(2, 2)) &&
                ReferenceEquals(Next(FocusNavigationDirection.Previous), grid.TryGetCell(0, 2)),
                "Native forward/reverse Tab order did not match displayed column order.");

            var row = grid.TryGetRow(2)!;
            var parent = cell.Parent;
            var unloads = 0;
            cell.Unloaded += (_, _) => ++unloads;
            Check(grid.BringCellIntoView(150, 7), "Far two-axis bring-into-view failed.");
            await Settle();
            Check(ReferenceEquals(FocusManager.GetFocusedElement(grid.XamlRoot ?? throw new InvalidOperationException("The focus fixture must be attached to a XamlRoot.")), cell) &&
                ReferenceEquals(grid.TryGetRow(2), row) && ReferenceEquals(cell.RowModel, items[2]) &&
                cell.RowIndex == 2 && cell.ColumnIndex == 1 && ReferenceEquals(cell.Parent, parent) && unloads == 0,
                "Scrolling recycled or detached the focused row/cell for another model.");
            Check(after.Focus(FocusState.Keyboard), "Could not move focus outside the grid.");
            await Settle();
            Check(grid.TryGetRow(2) is null, "The offscreen focused row was not released after focus left it.");

            var horizontal = (TreeDataGridCell)grid.TryGetCell(7, 150)!;
            Check(horizontal.Focus(FocusState.Keyboard), "Could not focus the last visible column.");
            Check(grid.BringCellIntoView(150, 0), "Horizontal return failed.");
            await Settle();
            Check(ReferenceEquals(grid.TryGetCell(7, 150), horizontal) && horizontal.ColumnIndex == 7 &&
                ReferenceEquals(FocusManager.GetFocusedElement(grid.XamlRoot ?? throw new InvalidOperationException("The focus fixture must be attached to a XamlRoot.")), horizontal),
                "Horizontal virtualization recycled the focused column.");
            Check(before.Focus(FocusState.Keyboard), "Could not release horizontal focus retention.");
            await Settle();
            Check(grid.TryGetCell(7, 150) is null, "The offscreen focused column was retained after focus left.");

            var header = grid.ColumnHeadersPresenter!.TryGetElement(0)!;
            var headerParent = header.Parent;
            Check(header.Focus(FocusState.Keyboard), "Could not focus a header.");
            Check(grid.BringCellIntoView(150, 7), "Header retention scrolling failed.");
            await Settle();
            Check(ReferenceEquals(grid.ColumnHeadersPresenter!.TryGetElement(0), header) && header.ColumnIndex == 0 &&
                ReferenceEquals(header.Parent, headerParent) && ReferenceEquals(FocusManager.GetFocusedElement(grid.XamlRoot ?? throw new InvalidOperationException("The focus fixture must be attached to a XamlRoot.")), header),
                "Horizontal scrolling recycled the focused header.");
            Check(after.Focus(FocusState.Keyboard), "Could not release header focus retention.");
            await Settle();
            Check(grid.ColumnHeadersPresenter!.TryGetElement(0) is null, "An offscreen header survived after focus left it.");

            Check(grid.BringCellIntoView(0, 0), "Return after pooled recycling failed.");
            await Settle();
            Check(grid.TryGetCell(0, 0)!.Focus(FocusState.Keyboard), "Could not focus the recycled first cell.");
            Check(ReferenceEquals(Next(FocusNavigationDirection.Next), grid.TryGetCell(1, 0)),
                "Tab order followed pooled native child insertion order instead of current columns.");
            var lastColumn = grid.TryGetRow(0)!.CellsPresenter!.RealizedCells.Max(x => x.ColumnIndex);
            Check(grid.TryGetCell(lastColumn, 0)!.Focus(FocusState.Keyboard), "Could not focus the final realized column.");
            Check(ReferenceEquals(Next(FocusNavigationDirection.Next), grid.TryGetCell(0, 1)),
                "Tab order followed recycled row insertion order instead of current row indexes.");
            grid.Model = null;
            await Settle();
            Check(grid.RowsPresenter!.RealizedCells.Count == 0 && grid.ColumnHeadersPresenter!.RealizedCount == 0,
                "Source removal retained focused source containers.");
            Console.WriteLine("UNO_RUNTIME_FOCUS_PASSED: native cell focus, matching-column arrows, forward/reverse Tab, ordered recycled children, two-axis focused retention/release, header focus and source cleanup");
        }
        finally { grid.Model = null; page.Content = previousContent; }

        DependencyObject? Next(FocusNavigationDirection direction) =>
            FocusManager.FindNextElement(direction, new FindNextElementOptions { SearchRoot = grid.RowsPresenter });
        async Task Settle() { await Task.Delay(100); grid.UpdateLayout(); }
    }

    private sealed record Item(string Name);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
