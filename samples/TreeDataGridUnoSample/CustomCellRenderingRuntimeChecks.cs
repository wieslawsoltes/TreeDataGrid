using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using TreeDataGridCore;
using TreeDataGridUnoSample.Demo.Views;
using TreeDataGridUnoSamples;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Custom-drawn cells from a custom element factory keep current text through recycling.</summary>
internal static class CustomCellRenderingRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        if (!SkiaTextCellElementFactory.IsSupported)
        {
            Console.WriteLine("UNO_RUNTIME_CUSTOM_CELL_RENDERING_SKIPPED: no Skia renderer");
            return;
        }
        var previous = page.Content;
        var items = new ObservableCollection<CustomCellRow>(Enumerable.Range(0, 2000).Select(index => new CustomCellRow(index)));
        using var source = new FlatTreeDataGridSource<CustomCellRow>(items);
        for (var column = 0; column < 16; ++column)
            source.Columns.Add(new TextColumn<CustomCellRow, string>($"C{column}",
                column % 2 == 0 ? row => row.Label : row => row.Code, new Microsoft.UI.Xaml.GridLength(128)));
        var grid = new Uno.Controls.TreeDataGrid
        {
            Width = 600, Height = 400, Source = source, ElementFactory = new SkiaTextCellElementFactory(),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        try
        {
            page.Content = grid;
            await Settle(grid);
            var scroll = grid.Scroll ?? throw new InvalidOperationException("No scroll viewer.");
            var checkedCells = 0;
            foreach (var (x, y) in new[] { (0d, 0d), (0d, 1200d), (640d, 1200d), (256d, 40000d), (0d, 0d) })
            {
                scroll.ChangeView(x, y, null, true);
                await Settle(grid);
                var cells = grid.RowsPresenter!.RealizedCells;
                Check(cells.Count > 0, "No cells were realized.");
                foreach (var cell in cells)
                {
                    Check(cell is SkiaTextCell, $"The custom factory created {cell.GetType().Name} for a text column.");
                    var model = items[cell.RowIndex];
                    var expected = cell.ColumnIndex % 2 == 0 ? model.Label : model.Code;
                    Check(((SkiaTextCell)cell).RenderedText == expected,
                        $"Recycled custom cell drew '{((SkiaTextCell)cell).RenderedText}' instead of '{expected}'.");
                    ++checkedCells;
                }
            }
#if !__WASM__
            var first = (FrameworkElement)grid.TryGetCell(0, 0)!;
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(first);
            var pixels = (await bitmap.GetPixelsAsync()).ToArray();
            var ink = 0;
            for (var i = 3; i < pixels.Length; i += 4) if (pixels[i] > 0) ++ink;
            Check(ink > 20, "The custom cell drew no text.");
#endif
            grid.ElementFactory = new TreeDataGridElementFactory();
            await Settle(grid);
            Check(grid.RowsPresenter!.RealizedCells.All(cell => cell.GetType() == typeof(TreeDataGridTextCell)),
                "Restoring the default element factory kept custom cells.");
            Console.WriteLine($"UNO_RUNTIME_CUSTOM_CELL_RENDERING_PASSED: custom element factory, {checkedCells} recycled cells drew current text across vertical, horizontal and distant scrolling, factory replacement");
        }
        finally { page.Content = previous; }
    }

    private static async Task Settle(Uno.Controls.TreeDataGrid grid)
    {
        grid.UpdateLayout();
        await Task.Delay(80);
        grid.UpdateLayout();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
