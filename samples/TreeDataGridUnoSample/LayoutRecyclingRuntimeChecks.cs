using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using Uno.Controls;
using Uno.Controls.Primitives;
using GridLength = Microsoft.UI.Xaml.GridLength;

namespace TreeDataGridUnoSample;

internal static class LayoutRecyclingRuntimeChecks
{
    public static async Task RunAsync(TreeDataGrid grid)
    {
        grid.Model = null;
        var oldFactory = grid.ElementFactory;
        var oldWidth = grid.Width;
        var oldHeight = grid.Height;
        var oldRowHeight = grid.RowHeight;
        var factory = new Factory();
        var callbacks = new List<(TreeDataGridRow Row, long Token)>();
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 2000).Select(i => new Item($"Row {i:D4}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        for (var i = 0; i < 64; ++i)
            source.WithTextColumn($"Column {i}", item => item.Name,
                options => { options.Width = new GridLength(80); options.IsReadOnly = true; });
        try
        {
            grid.Width = 360;
            grid.Height = 220;
            grid.RowHeight = 28;
            grid.ElementFactory = factory;
            grid.Model = source;
            await Settle();
            grid.Scroll!.ChangeView(0, 28, null, true);
            await Settle();
            // Warm both partial-row boundaries, then use equally aligned windows.
            grid.Scroll.ChangeView(40, 42, null, true);
            await Settle();
            grid.Scroll.ChangeView(0, 28, null, true);
            await Settle();
            var created = factory.Rows.Count;
            var collapses = 0;
            foreach (var row in factory.Rows)
            {
                var token = row.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (sender, _) =>
                {
                    if (((TreeDataGridRow)sender).Visibility == Visibility.Collapsed) ++collapses;
                });
                callbacks.Add((row, token));
            }
            var parents = factory.Rows.ToDictionary(row => row, row => row.Parent);
            foreach (var point in new[] { (X: 0d, Y: 2800d), (X: 1280d, Y: 8400d), (X: 3200d, Y: 16800d), (X: 0d, Y: 28d) })
            {
                grid.Scroll.ChangeView(point.X, point.Y, null, true);
                await Settle();
                Check(factory.Rows.Count == created, "A disjoint viewport allocated new rows after boundary priming.");
                Check(collapses == 0, $"Synchronous viewport recycling collapsed {collapses} reusable rows.");
                Verify();
                Check(parents.All(pair => ReferenceEquals(pair.Key.Parent, pair.Value)), "A recycled row changed parent.");
            }
            grid.Height = 100;
            await Settle();
            Verify();
            Check(factory.Rows.Where(row => row.RowIndex < 0).All(row => row.Visibility == Visibility.Collapsed),
                "Surplus rows remained visible after shrinking the viewport.");
            grid.Model = null;
            Check(factory.Rows.All(row => row.RowIndex < 0 && row.Visibility == Visibility.Collapsed && row.Model is null),
                "Source retirement retained a visible/bound row.");
            // Direct public lifetime methods must not inherit a layout-only policy.
            var standalone = new TreeDataGridRow();
            standalone.Realize(new TreeDataGridElementFactory(), null, null, null, 0);
            standalone.Unrealize();
            Check(standalone.RowIndex == -1 && standalone.Visibility == Visibility.Collapsed,
                "Public standalone Unrealize stopped hiding synchronously.");
            Console.WriteLine($"UNO_RUNTIME_LAYOUT_RECYCLING_PASSED: rows={created}; aligned disjoint/diagonal/reverse viewports; no row hide/show; retained parents; surplus/source/public cleanup");
        }
        finally
        {
            foreach (var item in callbacks) item.Row.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, item.Token);
            grid.Model = null;
            grid.ElementFactory = oldFactory;
            grid.RowHeight = oldRowHeight;
            grid.Width = oldWidth;
            grid.Height = oldHeight;
        }
        void Verify()
        {
            var rows = grid.RowsPresenter!.RealizedRows;
            Check(rows.Count is > 0 and < 16, "Layout recycling exceeded its viewport row budget.");
            foreach (var row in rows)
            {
                Check(row.Visibility == Visibility.Visible && ReferenceEquals(row.Model, items[row.RowIndex]), "Recycled row identity/visibility is stale.");
                foreach (var cell in row.CellsPresenter!.RealizedCells)
                    Check(cell.Visibility == Visibility.Visible && ReferenceEquals(cell.RowModel, items[row.RowIndex]), "Recycled cell identity/visibility is stale.");
            }
        }
        async Task Settle() { await Task.Delay(100); grid.UpdateLayout(); }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed record Item(string Name);
    private sealed class Factory : TreeDataGridElementFactory
    {
        public List<TreeDataGridRow> Rows { get; } = new();
        protected override Control CreateElement(object? data)
        {
            var element = base.CreateElement(data);
            if (element is TreeDataGridRow row) Rows.Add(row);
            return element;
        }
    }
}
