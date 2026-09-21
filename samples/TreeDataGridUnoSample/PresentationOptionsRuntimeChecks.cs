using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Deferred native checks for assignable view configuration and rollback.</summary>
internal static class PresentationOptionsRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        var previousOptions = grid.PresentationOptions;
        grid.Model = null;
        using var source = new FlatTreeDataGridSource<Item>([new("first")]);
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name, width: new(240)) { PresentationKey = "Formatted" });
        var initial = Options("[{0}]");
        var replacement = Options("<{0}>");
        try
        {
            grid.PresentationOptions = initial;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(150);
            var previousPresentation = grid.Presentation;
            Check(ReferenceEquals(previousPresentation?.Model, source), "Typed options replaced the Core source identity.");
            Check(CellText(grid) == "[first]", "Typed presentation did not render the configured format.");

            grid.PresentationOptions = replacement;
            await Task.Delay(150);
            var active = grid.Presentation;
            var cell = grid.TryGetCell(0, 0);
            Check(active is not null && !ReferenceEquals(active, previousPresentation) && ReferenceEquals(active.Model, source),
                "Changing PresentationOptions did not replace view state over the same Core source.");
            Check(CellText(grid) == "<first>", "Changing PresentationOptions did not update native cell formatting.");

            Reject(new TreeDataGridPresentationOptions<Other>());
            Reject(new TreeDataGridPresentationOptions<Item>());
            var throwing = Options("unused");
            throwing.Columns["Formatted"] = _ => throw new InvalidOperationException("Configured factory failure");
            Reject(throwing);
            Reject(null);
            Check(CellText(grid) == "<first>", "A failed configuration damaged the previous native view.");

            // Null/default configuration is valid again after detaching the
            // source requiring a keyed presentation factory.
            grid.Model = null;
            grid.PresentationOptions = null;
            Check(grid.PresentationOptions is null && grid.Presentation is null, "Clearing detached configuration retained a presentation.");
            Console.WriteLine("UNO_RUNTIME_PRESENTATION_OPTIONS_PASSED: typed configuration, same Core identity, live options replacement, wrong-model/missing-key/throwing/null rollback");

            void Reject(ITreeDataGridPresentationOptions? options)
            {
                var threw = false;
                try { grid.PresentationOptions = options; }
                catch (Exception e) when (e is ArgumentException or InvalidOperationException) { threw = true; }
                Check(threw && ReferenceEquals(grid.PresentationOptions, replacement) && ReferenceEquals(grid.Presentation, active) &&
                    ReferenceEquals(grid.Model, source) && ReferenceEquals(grid.TryGetCell(0, 0), cell),
                    "A failed options assignment did not roll back the property and preserve the working presentation/cell.");
            }
        }
        finally
        {
            grid.Model = null;
            grid.PresentationOptions = previousOptions;
        }
    }

    private static string? CellText(Uno.Controls.TreeDataGrid grid) => grid.TryGetCell(0, 0) is TreeDataGridCell cell
        ? ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().FirstOrDefault()?.Text : null;
    private static TreeDataGridPresentationOptions<Item> Options(string format)
    {
        var options = new TreeDataGridPresentationOptions<Item>();
        options.Columns["Formatted"] = column => new Uno.Controls.Models.TreeDataGrid.TextColumn<Item, string>(
            (ValueColumn<Item, string>)column, new Uno.Controls.Models.TreeDataGrid.TextColumnOptions<Item> { StringFormat = format });
        return options;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed record Item(string Name);
    private sealed record Other(string Name);
}
