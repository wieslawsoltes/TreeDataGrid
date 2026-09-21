using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TreeDataGridCore;
using Uno.Controls;
using Uno.Controls.Presentation;

namespace TreeDataGridUnoSample;

/// <summary>Native fluent templates, binding evaluation and resource-scope cases, authored but unrun.</summary>
internal static class SourceExtensionsRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid, DataTemplate display, DataTemplate editing)
    {
        var previousOptions = grid.PresentationOptions;
        grid.Model = null;
        grid.PresentationOptions = null;
        var item = new EditingRuntimeChecks.EditableItem("Fluent row", 25);
        using var source = new FlatTreeDataGridSource<EditingRuntimeChecks.EditableItem>([item]);
        source.WithRowHeaderColumn("#", o => o.Width = new(45))
            .WithTextColumn(x => x.Age, o => { o.StringFormat = "Age {0}"; o.Width = new(110); })
            .WithTemplateColumn("Direct", display, editing, o => o.Width = new(220))
            .WithTemplateColumnFromResourceKeys("Resource", "FluentDisplay", "FluentEditing", o =>
            {
                o.Width = new(220);
                o.TextSearchBinding = new Binding
                {
                    Path = new PropertyPath(nameof(item.Name)), Mode = BindingMode.OneWay,
                    Converter = new SearchConverter(), ConverterParameter = "search:",
                };
            });
        grid.Resources["FluentDisplay"] = display;
        grid.Resources["FluentEditing"] = editing;
        try
        {
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(200);
            Check(CellText(0) == "1" && CellText(1) == "Age 25" && CellText(2) == item.Name && CellText(3) == item.Name,
                "Fluent row-header/text/direct/resource template columns did not render.");
            var presentation = grid.Presentation!;
            var search = (CellColumn)presentation.Columns[3];
            Check(search.IsTextSearchEnabled && search.GetSearchText(item) == "search:Fluent row",
                "Native template search binding did not apply its converter/parameter.");
            item.Name = "Fluent update";
            await Task.Delay(50);
            Check(CellText(2) == item.Name && CellText(3) == item.Name && search.GetSearchText(item) == "search:Fluent update",
                "Fluent templates or search binding did not refresh the row value.");
            Check(grid.BeginEdit(0, 3), "Fluent resource editing template did not begin editing.");
            ShowcaseRuntimeChecks.Descendants(grid.EditingCell!).OfType<TextBox>().Single().Text = "Fluent edit";
            Check(grid.CommitEdit() && item.Name == "Fluent edit", "Fluent native template editing did not commit.");

            // A second presentation resolves the same resource key in its own
            // view scope; the first view's cached DataTemplate must not leak across.
            using var second = TreeDataGridPresentation.Create(source);
            var otherAnchor = new ContentControl();
            otherAnchor.Resources["FluentDisplay"] = editing;
            Check(ReferenceEquals(((CellColumn)second.Columns[3]).GetCellTemplate(otherAnchor), editing),
                "A resource template cache was shared by separate presentations.");
            Check(!ReferenceEquals(second.Columns[3], search) && ReferenceEquals(((CellColumn)second.Columns[3]).Model, source.Columns[3]),
                "Fluent columns either shared view instances or replaced Core identity.");
            grid.Model = null;
            grid.Model = source;
            await Task.Delay(100);
            Check(CellText(3) == item.Name, "Reattaching a fluent source lost its native configuration.");
            Console.WriteLine("UNO_RUNTIME_SOURCE_EXTENSIONS_PASSED: Core fluent sources, row headers, inferred text, direct/key templates, native search converter, editing, view-local resource caches, reattach");
        }
        finally
        {
            grid.Model = null;
            grid.PresentationOptions = previousOptions;
            grid.Resources.Remove("FluentDisplay");
            grid.Resources.Remove("FluentEditing");
        }
        string? CellText(int column) => grid.TryGetCell(column, 0) is { } cell
            ? ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().FirstOrDefault(text => text.Visibility == Visibility.Visible)?.Text : null;
    }

    private sealed class SearchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) => $"{parameter}{value}";
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
