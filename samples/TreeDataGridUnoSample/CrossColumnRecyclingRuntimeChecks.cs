using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using UI = Uno.Controls.Models.TreeDataGrid;
using GridLength = Microsoft.UI.Xaml.GridLength;

namespace TreeDataGridUnoSample;

/// <summary>Verifies horizontal recycling without amortizing creation across every source column.</summary>
internal static class CrossColumnRecyclingRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        grid.Model = null;
        var oldFactory = grid.ElementFactory;
        var oldWidth = grid.Width;
        var oldHeight = grid.Height;
        var oldRowHeight = grid.RowHeight;
        var factory = new CountingFactory();
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 100).Select(index => new Item($"Row {index}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        for (var column = 0; column < 128; ++column)
        {
            var format = $"Column {column}: {{0}}";
            Uno.Controls.TreeDataGridSourceExtensions.WithTextColumn(source, $"Column {column}", item => item.Name,
                options => { options.Width = new GridLength(80); options.StringFormat = format; options.IsReadOnly = true; });
        }
        try
        {
            grid.Width = 340;
            grid.Height = 200;
            grid.RowHeight = 28;
            grid.ElementFactory = factory;
            grid.Model = source;
            await Settle();
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            Check(grid.RowsPresenter!.RealizedCells.Count > 0, "The horizontal recycling fixture has no cells.");
            // Prime the extra boundary slot, not every source column/window.
            grid.Scroll.ChangeView(40, null, null, true);
            await Settle();
            grid.Scroll.ChangeView(0, null, null, true);
            await Settle();
            var created = factory.CreatedCells;
            var controls = factory.Cells.ToHashSet();
            var parents = controls.ToDictionary(cell => cell, cell => cell.Parent);
            var unloads = 0;
            foreach (var cell in controls) cell.Unloaded += (_, _) => ++unloads;

            foreach (var x in new[] { 16 * 80d, 40 * 80d, 96 * 80d, 0d, 72 * 80d, 8 * 80d })
            {
                grid.Scroll.ChangeView(x, null, null, true);
                await Settle();
                VerifyValues();
                Check(factory.CreatedCells == created,
                    $"A compatible horizontal window created new template controls: before={created}, after={factory.CreatedCells}, offset={x}.");
                Check(unloads == 0 && parents.All(pair => ReferenceEquals(pair.Key.Parent, pair.Value)),
                    "Cross-column reuse detached native controls from their retained row.");
            }

            items[0] = new("Replacement");
            await Settle();
            VerifyValues();
            Check(factory.CreatedCells == created, "Replacing a visible row discarded compatible cross-column controls.");
            grid.Model = null;
            Check(grid.RowsPresenter.RealizedCells.Count == 0 && factory.Cells.All(cell => cell.Model is null && cell.RowModel is null),
                "Source retirement retained a cross-column cell model.");
            Console.WriteLine($"UNO_RUNTIME_CROSS_COLUMN_RECYCLING_PASSED: columns=128; createdCells={created}; six disjoint/reverse windows; zero new controls/unloads; correct formats/models; source cleanup");
        }
        finally
        {
            grid.Model = null;
            grid.ElementFactory = oldFactory;
            grid.RowHeight = oldRowHeight;
            grid.Width = oldWidth;
            grid.Height = oldHeight;
        }

        void VerifyValues()
        {
            var cells = grid.RowsPresenter!.RealizedCells;
            Check(cells.Count is > 0 and < 100, "Horizontal realization did not remain bounded by the viewport.");
            foreach (var cell in cells)
            {
                var item = items[cell.RowIndex];
                Check(ReferenceEquals(cell.RowModel, item), "A cross-column control retained the wrong row model.");
                var expected = $"Column {cell.ColumnIndex}: {item.Name}";
                var text = ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>()
                    .FirstOrDefault(value => value.Name == "PART_Text");
                Check(text?.Text == expected, $"Cross-column reuse retained wrong format/value: expected '{expected}', actual '{text?.Text}'.");
            }
        }
        async Task Settle() { await Task.Delay(100); grid.UpdateLayout(); }
    }

    private sealed record Item(string Name);
    private sealed class CountingFactory : TreeDataGridElementFactory
    {
        public int CreatedCells { get; private set; }
        public List<TreeDataGridCell> Cells { get; } = new();
        protected override Control CreateElement(object? data)
        {
            var element = base.CreateElement(data);
            if (data is UI.ICell && element is TreeDataGridCell cell)
            {
                ++CreatedCells;
                Cells.Add(cell);
            }
            return element;
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
