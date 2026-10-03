using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Native rendering checks for direct and resource-key column facades, not yet executed.</summary>
internal static class ColumnCompatibilityRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid, DataTemplate display, DataTemplate editing)
    {
        var previousOptions = grid.PresentationOptions;
        grid.Model = null;
        var item = new EditingRuntimeChecks.EditableItem("Facade model", 20);
        using var source = new FlatTreeDataGridSource<EditingRuntimeChecks.EditableItem>([item]);
        source.Columns.Add(new TemplateColumn<EditingRuntimeChecks.EditableItem>("Direct", "Direct", width: new(240)));
        source.Columns.Add(new TemplateColumn<EditingRuntimeChecks.EditableItem>("Resource", "Resource", width: new(240)));
        source.Columns.Add(new TextColumn<EditingRuntimeChecks.EditableItem, int>("Age", x => x.Age, width: new(120)) { PresentationKey = "Text" });
        var options = new TreeDataGridPresentationOptions<EditingRuntimeChecks.EditableItem>();
        options.Columns["Direct"] = _ => new UI.TemplateColumn<EditingRuntimeChecks.EditableItem>("Direct UI", display, editing);
        options.Columns["Resource"] = _ => new UI.TemplateColumn<EditingRuntimeChecks.EditableItem>("Resource UI", "CompatDisplay", "CompatEditing");
        options.Columns["Text"] = column => new UI.TextColumn<EditingRuntimeChecks.EditableItem, int>(
            (ValueColumn<EditingRuntimeChecks.EditableItem, int>)column,
            new UI.TextColumnOptions<EditingRuntimeChecks.EditableItem> { StringFormat = "Age {0}", TextAlignment = TextAlignment.Right });
        grid.Resources["CompatDisplay"] = display;
        grid.Resources["CompatEditing"] = editing;
        try
        {
            grid.PresentationOptions = options;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(200);
            Check(CellText(0) == item.Name && CellText(1) == item.Name && CellText(2) == "Age 20", "Column facade templates or text options did not render.");
            Check(grid.ColumnHeadersPresenter!.RealizedHeaders.Any(header => Equals(header.Content, "Direct UI")), "Custom UI header was replaced by Core metadata.");
            Check(ReferenceEquals(((CellColumn)grid.Presentation!.Columns[0]).Model, source.Columns[0]), "The direct-template facade changed the Core column identity.");
            Check(Math.Abs(grid.Presentation.Columns[0].ActualWidth - 240) < 0.01, "The public ActualWidth was not committed from native layout.");
            item.Name = "Bound update";
            await Task.Delay(50);
            Check(CellText(0) == item.Name && CellText(1) == item.Name, "Native bindings through direct/key templates did not update.");

            Check(grid.BeginEdit(0, 1), "Resource-key editing template did not enter editing.");
            var editor = ShowcaseRuntimeChecks.Descendants(grid.EditingCell!).OfType<TextBox>().Single();
            editor.Text = "Edited by key";
            Check(grid.CommitEdit() && item.Name == "Edited by key", "Resource-key editing template did not commit its native binding.");

            // UI metadata remains independent from the source definition.
            ((CellColumn)grid.Presentation.Columns[0]).Header = "Renamed UI";
            grid.UpdateLayout();
            Check(Equals(source.Columns[0].Header, "Direct") && grid.ColumnHeadersPresenter!.RealizedHeaders.Any(header => Equals(header.Content, "Renamed UI")),
                "View header changes either mutated Core metadata or failed to invalidate the native header.");
            UI.IColumns layout = grid.Presentation.Columns;
            layout.SetColumnWidth(0, new Microsoft.UI.Xaml.GridLength(280));
            grid.UpdateLayout();
            await Task.Delay(50);
            Check(source.Columns[0].Width.Value == 280 && Math.Abs(layout[0].ActualWidth - 280) < 0.01 &&
                layout.GetColumnAt(279).index == 0 && layout.GetColumnAt(280).index == 1,
                "The public column-layout contract diverged from Core width or native committed geometry.");
            Check(ReferenceEquals(grid.TryGetRow(0)!.Columns, layout),
                "Rows did not expose the same public IColumns object as the presentation.");
            Console.WriteLine("UNO_RUNTIME_COLUMN_COMPATIBILITY_PASSED: typed UI factories, direct/resource display and edit templates, Core identity, actual width, formatted text, live header metadata");
        }
        finally
        {
            grid.Model = null;
            grid.PresentationOptions = previousOptions;
            grid.Resources.Remove("CompatDisplay");
            grid.Resources.Remove("CompatEditing");
        }
        string? CellText(int column) => grid.TryGetCell(column, 0) is { } cell
            ? ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().FirstOrDefault(text => text.Visibility == Visibility.Visible)?.Text : null;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
