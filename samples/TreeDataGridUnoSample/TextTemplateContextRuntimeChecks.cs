using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using Uno.Controls;
using Uno.Controls.Primitives;
using GridLength = Microsoft.UI.Xaml.GridLength;

namespace TreeDataGridUnoSample;

/// <summary>Default template internals must not change the public binding/lifetime contract.</summary>
internal static class TextTemplateContextRuntimeChecks
{
    public static async Task RunAsync(TreeDataGrid grid)
    {
        grid.Model = null;
        var width = grid.Width;
        var height = grid.Height;
        var rowHeight = grid.RowHeight;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 200).Select(i => new Item($"Row {i:D4}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        for (var i = 0; i < 32; ++i)
            source.WithTextColumn($"Column {i}", item => item.Name, options => options.Width = new GridLength(96));
        var observers = new List<(FrameworkElement Element, long Token)>();
        var templateContextChanges = 0;
        var publicContextChanges = 0;
        try
        {
            grid.Width = 360;
            grid.Height = 220;
            grid.RowHeight = 28;
            grid.Model = source;
            await Settle();
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            var original = grid.TryGetCell(0, 0) ?? throw new InvalidOperationException("Text fixture did not realize its first cell.");
            foreach (var cell in grid.RowsPresenter!.RealizedCells)
            {
                var border = ShowcaseRuntimeChecks.Descendants(cell).OfType<Border>().First(x => x.Name == "CellBorder");
                Check(border.DataContext is null, "The private scalar template did not isolate unused inherited context.");
                observers.Add((border, border.RegisterPropertyChangedCallback(FrameworkElement.DataContextProperty,
                    (_, _) => ++templateContextChanges)));
                observers.Add((cell, cell.RegisterPropertyChangedCallback(FrameworkElement.DataContextProperty,
                    (_, _) => ++publicContextChanges)));
            }
            Verify();
            items[0] = new("Replacement");
            await Settle();
            Check(ReferenceEquals(original, grid.TryGetCell(0, 0)), "Row replacement rebuilt the public cell.");
            Verify();
            source.SortBy(source.Columns[0], ListSortDirection.Descending);
            await Settle();
            Verify();
            grid.Scroll.ChangeView(768, 1400, null, true);
            await Settle();
            Verify();
            grid.Scroll.ChangeView(0, 0, null, true);
            await Settle();
            Verify();
            Check(publicContextChanges > 0, "Public cell DataContext notifications were suppressed.");
            Check(templateContextChanges == 0, $"Unused scalar template contexts changed {templateContextChanges} times.");
            grid.Model = null;
            Check(observers.Where(x => x.Element is TreeDataGridCell).All(x => x.Element.DataContext is null),
                "Source retirement retained public cell contexts.");
            Console.WriteLine($"UNO_RUNTIME_TEXT_TEMPLATE_CONTEXT_PASSED: inherited public model contexts; replacement/sort/two-axis reuse; template context changes={templateContextChanges}; public context changes={publicContextChanges}; source cleanup");
        }
        finally
        {
            foreach (var observer in observers)
                observer.Element.UnregisterPropertyChangedCallback(FrameworkElement.DataContextProperty, observer.Token);
            grid.Model = null;
            grid.Width = width;
            grid.Height = height;
            grid.RowHeight = rowHeight;
        }
        void Verify()
        {
            Check(grid.RowsPresenter!.RealizedCells.Count is > 0 and < 100, "Text-template fixture lost bounded virtualization.");
            foreach (var row in grid.RowsPresenter.RealizedRows)
                Check(ReferenceEquals(row.DataContext, source.Rows[row.RowIndex].Model), "Public row DataContext is stale.");
            foreach (var cell in grid.RowsPresenter.RealizedCells)
            {
                var model = (Item)source.Rows[cell.RowIndex].Model!;
                Check(ReferenceEquals(cell.RowModel, model) && ReferenceEquals(cell.DataContext, model),
                    "Public cell RowModel or inherited DataContext is stale.");
                Check(ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().Any(x => x.Text == model.Name),
                    "Scalar template text is stale.");
            }
        }
        async Task Settle() { await Task.Delay(80); grid.UpdateLayout(); }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed record Item(string Name);
}
