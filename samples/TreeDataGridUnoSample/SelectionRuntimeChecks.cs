using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using Uno.Controls;
using Uno.Controls.Presentation;
using IndexPath = TreeDataGridCore.IndexPath;

namespace TreeDataGridUnoSample;

internal static class SelectionRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid, ControlTemplate alternateTemplate)
    {
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 200).Select(i => new Item($"Item {i:000}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name, width: new(220)));
        source.Columns.Add(new TextColumn<Item, string>("Hidden", x => x.Name) { IsVisible = false });
        source.Columns.Add(new TextColumn<Item, string>("Other", x => x.Name, width: new(220)));
        grid.SelectionMode = TreeDataGridSelectionMode.MultipleRows;
        grid.Model = source;
        grid.Scroll.ChangeView(0, 0, null, true);
        await Task.Delay(200);
        Check(grid.SelectCell(1, 0), "Selecting a visible row failed.");
        VerifySelection(grid);
        Check(grid.RowsPresenter.RealizedCells.Count(x => x.IsSelected) == 2, "Row selection did not highlight every visible cell.");
        grid.MoveSelection(TreeDataGridNavigation.Down, extend: true);
        grid.MoveSelection(TreeDataGridNavigation.Down, extend: true);
        Check(source.RowSelection!.Count == 3, "Extended navigation did not preserve its range anchor.");
        VerifySelection(grid);
        grid.MoveSelection(TreeDataGridNavigation.End);
        await Task.Delay(200);
        Check(source.RowSelection.SelectedIndex == new IndexPath(199), "End navigation selected the wrong model.");
        Check(grid.RowsPresenter.RealizedCells.Any(x => x.RowIndex == 199 && x.IsSelected), "End navigation did not bring the selection into view.");

        grid.SelectionMode = TreeDataGridSelectionMode.MultipleCells;
        grid.MoveSelection(TreeDataGridNavigation.Home);
        grid.SelectCell(0, 0);
        grid.MoveSelection(TreeDataGridNavigation.Right, extend: true);
        grid.MoveSelection(TreeDataGridNavigation.Down, extend: true);
        await Task.Delay(150);
        var cells = (ITreeDataGridCellSelectionModel<Item>)source.Selection!;
        Check(cells.Count == 6, "Rectangular selection did not map through the hidden Core column.");
        VerifySelection(grid);
        Check(grid.RowsPresenter.RealizedCells.Count(x => x.IsSelected) == 4, "Visible rectangular highlight is incorrect.");
        source.SortBy(source.Columns[0], ListSortDirection.Descending);
        grid.BringCellIntoView(source.Rows.ModelIndexToRowIndex(cells.SelectedIndex.RowIndex), 0);
        await Task.Delay(150);
        VerifySelection(grid);
        Check(grid.RowsPresenter.RealizedCells.Where(x => x.IsSelected).All(x => ((Item)x.RowModel!).Name is "Item 000" or "Item 001"), "Sorting changed the selected model identities.");

        // Source creation failure must not leave a new Model DP paired with the
        // previous source's cells, nor dispose the previous working presentation.
        using var invalid = new FlatTreeDataGridSource<Item>(items);
        invalid.Columns.Add(new TextColumn<Item, string>("Invalid", x => x.Name) { PresentationKey = "Unregistered" });
        var previousPresentation = grid.Presentation;
        var threw = false;
        try { grid.Model = invalid; }
        catch (InvalidOperationException) { threw = true; }
        Check(threw && ReferenceEquals(grid.Model, source) && ReferenceEquals(grid.Presentation, previousPresentation), "Failed source replacement did not restore the working Model/presentation pair.");
        VerifySelection(grid);

        var previousPresenter = grid.RowsPresenter;
        var previousTemplate = grid.Template;
        var previousSelection = source.Selection;
        grid.Template = alternateTemplate;
        grid.ApplyTemplate();
        await Task.Delay(200);
        Check(!ReferenceEquals(previousPresenter, grid.RowsPresenter), "The alternate control template was not applied.");
        Check(previousPresenter.RealizedCells.Count == 0, "The old template retained realized cells.");
        Check(ReferenceEquals(previousSelection, source.Selection), "Retemplating replaced shared Core selection.");
        grid.BringCellIntoView(199, 1);
        await Task.Delay(150);
        VerifySelection(grid);
        Check(grid.RowsPresenter.RealizedCells.Count > 0, "Retemplating lost the rows presentation.");
        grid.Template = previousTemplate;
        grid.ApplyTemplate();
        await Task.Delay(150);
        grid.Model = null;
        grid.SelectionMode = TreeDataGridSelectionMode.Source;

        using var hierarchy = new HierarchicalTreeDataGridSource<Node>([new Node("Root", [new Node("Child", [])])]);
        hierarchy.Columns.Add(new HierarchicalExpanderColumn<Node>(new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
        grid.Model = hierarchy;
        grid.Scroll.ChangeView(0, 0, null, true);
        await Task.Delay(150);
        grid.SelectCell(0, 0);
        grid.MoveSelection(TreeDataGridNavigation.Right);
        Check(hierarchy.Rows.Count == 2, "Right navigation did not expand the Core row.");
        grid.MoveSelection(TreeDataGridNavigation.Right);
        Check(hierarchy.RowSelection!.SelectedIndex == new IndexPath(0, 0), "Right navigation did not move into the expanded child.");
        grid.MoveSelection(TreeDataGridNavigation.Left);
        Check(hierarchy.RowSelection.SelectedIndex == new IndexPath(0), "Left navigation did not select the parent.");
        grid.MoveSelection(TreeDataGridNavigation.Left);
        Check(hierarchy.Rows.Count == 1, "Left navigation did not collapse the Core row.");
        grid.Model = null;
        Console.WriteLine("UNO_RUNTIME_SELECTION_PASSED: row/cell highlights, sorted/hidden mapping, navigation, bring-into-view, hierarchy, source failure rollback, retemplating");
    }
    private static void VerifySelection(Uno.Controls.TreeDataGrid grid)
    {
        foreach (var cell in grid.RowsPresenter.RealizedCells)
        {
            Check(cell.IsSelected == grid.Presentation!.Selection.IsSelected(cell.RowIndex, cell.ColumnIndex), "A recycled cell has stale selected state.");
            Check(ReferenceEquals(cell.RowModel, grid.Model!.Rows[cell.RowIndex].Model), "A selected cell retained a stale model.");
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed record Item(string Name);
    private sealed record Node(string Name, Node[] Children);
}
