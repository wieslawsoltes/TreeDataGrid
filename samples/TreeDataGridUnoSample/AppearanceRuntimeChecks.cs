using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Media;
using TreeDataGridCore;
using Uno.Controls;
using Uno.Controls.Primitives;
using Windows.Foundation;
using Windows.UI;
using GridLength = Microsoft.UI.Xaml.GridLength;

namespace TreeDataGridUnoSample;

/// <summary>Native appearance/layout gates. Actual OS high contrast and pointer resizing are separate gates.</summary>
internal static class AppearanceRuntimeChecks
{
    private static void ConfigureFlowDirection(FrameworkElement element, FlowDirection direction)
    {
        // The neutral browser reference assembly also describes the DOM head,
        // whose RTL support differs from the SkiaRenderer used by this sample.
        // Query the actual loaded implementation, then test its geometry below;
        // an unsupported head is a failure, never a skipped acceptance check.
        if (!Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent(
                "Microsoft.UI.Xaml.FrameworkElement", nameof(FrameworkElement.FlowDirection)))
            throw new PlatformNotSupportedException("The loaded renderer does not implement native FlowDirection.");
#pragma warning disable Uno0001 // Actual runtime capability checked above; this sample publishes with SkiaRenderer.
        element.FlowDirection = direction;
        Check(element.FlowDirection == direction, "The native FlowDirection value was not applied.");
#pragma warning restore Uno0001
    }

    internal static async Task RunAsync(MainPage page)
    {
        var content = page.Content;
        using var source = new FlatTreeDataGridSource<Item>([new("Bravo measuring text"), new("Alpha measuring text")]);
        source.WithTextColumn(x => x.Name, options => options.Width = GridLength.Auto);
        var grid = new Uno.Controls.TreeDataGrid { Width = 600, Height = 350, Source = source, FontSize = 14 };
        try
        {
            page.Content = grid;
            await Task.Delay(150);
            var row = grid.TryGetRow(0)!;
            var cell = (TreeDataGridCell)grid.TryGetCell(0, 0)!;
            var parent = VisualTreeHelper.GetParent(cell);
            var width = grid.Presentation!.Columns[0].ActualWidth;
            var height = grid.RowsPresenter!.GetRowHeight(0);
            grid.FontSize = 32;
            await Task.Delay(100);
            Check(grid.Presentation.Columns[0].ActualWidth > width + 20 && grid.RowsPresenter!.GetRowHeight(0) > height,
                "Font growth left stale column-width/row-height caches.");
            grid.FontSize = 14;
            await Task.Delay(100);
            Check(Math.Abs(grid.Presentation.Columns[0].ActualWidth - width) < 2 && Math.Abs(grid.RowsPresenter!.GetRowHeight(0) - height) < 2,
                "Reducing font size retained larger cached measurements.");
            Check(ReferenceEquals(cell, grid.TryGetCell(0, 0)) && ReferenceEquals(row, grid.TryGetRow(0)) &&
                ReferenceEquals(parent, VisualTreeHelper.GetParent(cell)), "Appearance changes replaced or reparented existing controls.");
            var borderHeight = grid.RowsPresenter!.GetRowHeight(0);
            row.BorderThickness = new(7, 4, 9, 6);
            cell.BorderThickness = new(3, 5, 4, 7);
            await Task.Delay(100);
            grid.UpdateLayout();
            var cellsOrigin = row.CellsPresenter!.TransformToVisual(row).TransformPoint(new Point());
            var borderText = ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().Single(x => x.Name == "PART_Text");
            var textOrigin = borderText.TransformToVisual(cell).TransformPoint(new Point());
            Check(Math.Abs(cellsOrigin.X - 7) < 1 && Math.Abs(cellsOrigin.Y - 4) < 1 &&
                textOrigin.X >= cell.Padding.Left + 3 - 0.5 && textOrigin.Y >= cell.Padding.Top + 5 - 0.5 &&
                grid.RowsPresenter!.GetRowHeight(0) > borderHeight,
                "Custom row/cell borders were drawn as overlays instead of reserving layout space.");
            Check(ReferenceEquals(row, grid.TryGetRow(0)) && ReferenceEquals(cell, grid.TryGetCell(0, 0)) &&
                ReferenceEquals(parent, VisualTreeHelper.GetParent(cell)), "Border layout changes replaced retained row/cell controls.");
            row.ClearValue(Control.BorderThicknessProperty);
            cell.ClearValue(Control.BorderThicknessProperty);
            await Task.Delay(100);
            var customForeground = new SolidColorBrush(Microsoft.UI.Colors.Fuchsia);
            grid.Foreground = customForeground;
            await Task.Delay(50);
            var header = grid.ColumnHeadersPresenter!.RealizedHeaders.Single();
            Check(Equals(header.Header, source.Columns[0].Header) && Equals(header.GetValue(TreeDataGridColumnHeader.HeaderProperty), header.Header),
                "The public header value/dependency property did not preserve the column header.");
            var headerContent = ShowcaseRuntimeChecks.Descendants(header).OfType<ContentPresenter>().Single(x => x.Name == "PART_ContentPresenter");
            Check(ReferenceEquals(headerContent.Content, header.Header), "The default header theme did not bind Header.");
            Check(ShowcaseRuntimeChecks.Descendants(headerContent).OfType<TextBlock>().Any(value => value.TextTrimming == TextTrimming.CharacterEllipsis),
                "The default string header template did not preserve reference ellipsis trimming.");
            grid.CanUserResizeColumns = true;
            await Task.Delay(50);
            Check(ShowcaseRuntimeChecks.Descendants(header).OfType<TreeDataGridColumnResizer>().Single().Visibility == Visibility.Visible,
                "The default theme did not use the native resize-cursor grip.");
            Check(ReferenceEquals(cell.Foreground, customForeground) && ReferenceEquals(header.Foreground, customForeground),
                "Default cell/header styles shadow the grid's inherited foreground.");
            var text = ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().Single(x => x.Name == "PART_Text");
            Check(ReferenceEquals(text.Foreground, customForeground), "Cell text did not inherit the custom foreground.");
            grid.ClearValue(Control.ForegroundProperty);
            grid.RequestedTheme = ElementTheme.Light;
            await Task.Delay(50);
            ReportTheme("light");
            var light = ((SolidColorBrush)text.Foreground).Color;
            grid.RequestedTheme = ElementTheme.Dark;
            await Task.Delay(50);
            ReportTheme("dark");
            Check(((SolidColorBrush)text.Foreground).Color != light, "Retained text did not update its theme resource.");

            // Source-only header sorting must operate on the same actual Core
            // model, not the mutually exclusive Model compatibility slot.
            Check(grid.Model is null, "Appearance fixture is not exercising Source-only configuration.");
            var headerPeer = FrameworkElementAutomationPeer.CreatePeerForElement(header);
            RuntimeAssertions.Pattern<IInvokeProvider>(headerPeer, PatternInterface.Invoke).Invoke();
            await Task.Delay(100);
            Check(((Item)source.Rows[0].Model!).Name.StartsWith("Alpha", StringComparison.Ordinal), "Header sorting ignored Source-only configuration.");

            using var wide = new FlatTreeDataGridSource<Item>([new("RTL text")]);
            for (var i = 0; i < 3; ++i) wide.WithTextColumn(x => x.Name, options => options.Width = new(250));
            grid.Source = wide;
            ConfigureFlowDirection(grid, FlowDirection.RightToLeft);
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(100);
            var first = Bounds(grid.TryGetCell(0, 0)!);
            var second = Bounds(grid.TryGetCell(1, 0)!);
            Check(first.X > second.X, "RTL did not mirror column order.");
            AssertHeaderAlignment();
            grid.BringCellIntoView(0, 2);
            await Task.Delay(100);
            Check(grid.TryGetCell(2, 0) is not null, "RTL horizontal bring-into-view did not realize the target column.");
            AssertTargetVisible(2);
            AssertHeaderAlignment();
            ConfigureFlowDirection(grid, FlowDirection.LeftToRight);
            grid.Scroll.ChangeView(0, 0, null, true);
            await Task.Delay(100);
            Check(Bounds(grid.TryGetCell(0, 0)!).X < Bounds(grid.TryGetCell(1, 0)!).X,
                "Returning from RTL left stale mirrored column geometry.");
            AssertHeaderAlignment();
            grid.BringCellIntoView(0, 2);
            await Task.Delay(100);
            AssertTargetVisible(2);
            AssertHeaderAlignment();
            Console.WriteLine("UNO_RUNTIME_APPEARANCE_PASSED: font growth/shrink, parent retention, border layout, header trimming, inherited foreground, live Light/Dark, Source header sorting, native RTL/LTR round trip, header alignment and visible horizontal targets");
            grid.Source = null;

            Rect Bounds(FrameworkElement element) => element.TransformToVisual(page).TransformBounds(new(0, 0, element.ActualWidth, element.ActualHeight));
            void ReportTheme(string step)
            {
                static string ColorOf(Brush brush) => brush is SolidColorBrush solid ? solid.Color.ToString() : brush?.GetType().Name ?? "null";
                Console.WriteLine($"UNO_THEME_STATE: {step}; actual={grid.ActualTheme}/{row.ActualTheme}/{cell.ActualTheme}/{text.ActualTheme}; foreground={ColorOf(grid.Foreground)}/{ColorOf(row.Foreground)}/{ColorOf(cell.Foreground)}/{ColorOf(text.Foreground)}; localGrid={grid.ReadLocalValue(Control.ForegroundProperty)}; selected={row.IsSelected}/{cell.IsSelected}");
            }
            void AssertTargetVisible(int index)
            {
                var target = grid.TryGetCell(index, 0);
                Check(target is not null, "Horizontal bring-into-view did not retain its target.");
                var targetBounds = Bounds(target!);
                var viewport = Bounds(grid.Scroll!);
                Check(targetBounds.Width > 0 && targetBounds.X < viewport.Right && targetBounds.Right > viewport.X,
                    "Horizontal bring-into-view realized a target outside the native scroll viewport.");
            }
            void AssertHeaderAlignment()
            {
                foreach (var currentHeader in grid.ColumnHeadersPresenter!.RealizedHeaders)
                {
                    if (grid.TryGetCell(currentHeader.ColumnIndex, 0) is not { } currentCell) continue;
                    var headerBounds = Bounds(currentHeader);
                    var cellBounds = Bounds(currentCell);
                    Check(Math.Abs(headerBounds.X - cellBounds.X) < 1 && Math.Abs(headerBounds.Width - cellBounds.Width) < 1,
                        "RTL body/header geometry diverged.");
                }
            }
        }
        finally { grid.Source = null; page.Content = content; }
    }
    private sealed record Item(string Name);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
