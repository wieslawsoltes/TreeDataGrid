using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TreeDataGridDemo.Models;

namespace TreeDataGridUnoSample;

internal static class ShowcaseRuntimeChecks
{
    public static async Task RunAsync(MainPage page, Func<UIElement, string, Task> capture)
    {
        page.ShowScenario(1);
        var grid = page.Grid;
        await Task.Delay(200);
        Check(ReferenceEquals(grid.Presentation!.Rows, page.PeopleSource.Rows), "People did not use shared Core rows.");
        Check(page.PeopleSource.Rows.Count == 6, "The shared People sample did not initialize its nested expansion bindings.");
        var manager = (Person)page.PeopleSource.Rows[0].Model!;
        manager.IsExpanded = false;
        Check(page.PeopleSource.Rows.Count == 5, "External expansion mutation did not collapse the shared source.");
        manager.IsExpanded = true;
        await Task.Delay(100);
        Check(grid.BeginEdit(0, 0), "The editable name inside an expander did not start editing.");
        grid.EditingCell!.EditingText = "Eleanor Pope (edited)";
        Check(grid.CommitEdit() && manager.Name == "Eleanor Pope (edited)", "Expander editor did not write the shared Person model.");
        manager.Name = "Eleanor Pope";
        Check(grid.BeginEdit(0, 2), "People age editor did not start.");
        grid.EditingCell!.EditingText = "-1";
        Check(!grid.CommitEdit() && grid.EditingCell.HasValidationError, "People validation accepted a negative age.");
        grid.UpdateLayout();
        var editor = Descendants(grid.EditingCell).OfType<TextBox>().Single();
        Check(editor.Text == "-1" && editor.ActualWidth > 0 && editor.ActualHeight > 0, "Invalid editor is not rendered with the user's input.");
        await capture(page, "people-validation");
        grid.CancelEdit();
        var active = grid.RowsPresenter.RealizedCells.Single(c => c.RowIndex == 0 && c.ColumnIndex == 3);
        var checkBox = Descendants(active).OfType<CheckBox>().Single();
        checkBox.IsChecked = !manager.IsActive;
        Check(manager.IsActive == checkBox.IsChecked, "Native checkbox changes did not write the shared Person.");
        checkBox.IsChecked = true;
        var added = new Person { Name = "Added child", Age = 21 };
        manager.Children.Add(added);
        await Task.Delay(100);
        Check(grid.RowsPresenter.RealizedCells.Any(c => ReferenceEquals(c.RowModel, added)), "Adding a shared child did not update the native hierarchy.");
        manager.Children.Remove(added);
        grid.SelectCell(0, 0);
        await capture(page, "people");

        page.ShowScenario(2);
        await Task.Delay(200);
        Check(grid.RowsPresenter.RealizedCells.Count < page.TemplateSource.Rows.Count * page.TemplateSource.Columns.Count,
            "Template sample did not virtualize rows.");
        var cell = grid.RowsPresenter.RealizedCells.Single(c => c.RowIndex == 0 && c.ColumnIndex == 3);
        var text = Descendants(cell).OfType<TextBlock>().Single(t => t.Text == "Details for item 001");
        var parent = VisualTreeHelper.GetParent(text);
        var previous = page.TemplateItems[0];
        page.TemplateItems[0] = new() { Name = previous.Name, Type = previous.Type, Details = "Replaced details", IsFlagged = true };
        await Task.Delay(100);
        Check(text.Text == "Replaced details" && ReferenceEquals(parent, VisualTreeHelper.GetParent(text)),
            "Template sample replacement recreated or failed to refresh its content.");
        await capture(page, "templates");
        grid.Scroll.ChangeView(null, 1500, null, true);
        await Task.Delay(150);
        foreach (var visible in grid.RowsPresenter.RealizedCells.Where(c => c.ColumnIndex == 3))
        {
            var model = (TemplateColumnItem)page.TemplateSource.Rows[visible.RowIndex].Model!;
            Check(Descendants(visible).OfType<TextBlock>().Any(t => t.Text == model.Details), "Scrolling retained another template row's details.");
        }
        page.TemplateItems[0] = previous;
        page.ShowScenario(3);
        await Task.Delay(200);
        Check(grid.RowsPresenter.RealizedCells.Select(c => grid.RowsPresenter.GetRowHeight(c.RowIndex)).Distinct().Count() > 1,
            "Variable-country sample did not measure multi-line row heights.");
        await capture(page, "variable-countries");
        Check(grid.BringCellIntoView(100, 0), "Variable-country bring-into-view failed.");
        await Task.Delay(150);
        Check(grid.RowsPresenter.RealizedCells.Any(c => c.RowIndex == 100), "Variable-country target was not realized.");
        page.ShowScenario(0);
        await Task.Delay(150);
        Console.WriteLine("UNO_RUNTIME_SHOWCASE_PASSED: shared People hierarchy, expander editing, validation, checkbox writeback, child mutation, template replacement/scroll, scenario switching");
    }

    internal static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); ++i)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
