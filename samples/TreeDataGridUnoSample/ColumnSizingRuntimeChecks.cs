using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using TreeDataGridCore;
using TreeDataGridCore.Models;

namespace TreeDataGridUnoSample;

internal static class ColumnSizingRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        var previousWidth = grid.Width;
        grid.Width = 620;
        var item = new Item("A");
        using var source = new FlatTreeDataGridSource<Item>(new ObservableCollection<Item>([item]));
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name));
        source.Columns.Add(new TextColumn<Item, string>("Capped star", x => x.Name, width: GridLength.Star, options: new() { MaxWidth = new(80) }));
        source.Columns.Add(new TextColumn<Item, string>("Remaining star", x => x.Name, width: GridLength.Star));
        try
        {
            grid.Model = source;
            grid.Scroll.ChangeView(0, 0, null, true);
            await Task.Delay(250);
            var original = Width(grid, 0);
            Check(original >= 30 && original < 150, "Auto sizing still uses an estimate instead of native content.");
            Check(Math.Abs(Width(grid, 1) - 80) < 1, "Star maximum was not applied.");
            Check(Math.Abs(Width(grid, 0) + Width(grid, 1) + Width(grid, 2) - grid.Scroll.ViewportWidth) < 1,
                "Constrained star sizing did not redistribute the remaining viewport width.");
            item.Name = "This text expands the measured auto column";
            await Task.Delay(150);
            var expanded = Width(grid, 0);
            Check(expanded > original + 100, "A live bound text update did not increase the auto width.");
            Check(Math.Abs(Width(grid, 0) + Width(grid, 1) + Width(grid, 2) - grid.Scroll.ViewportWidth) < 1,
                $"Auto growth did not update the star columns in the same geometry: {Width(grid, 0)}, {Width(grid, 1)}, {Width(grid, 2)}; viewport={grid.Scroll.ViewportWidth}.");
            item.Name = "A";
            await Task.Delay(100);
            Check(Math.Abs(Width(grid, 0) - expanded) < 1, "Auto sizing shrank while scrolling/mutating within the same presentation.");
            grid.Width = 800;
            await Task.Delay(150);
            Check(Math.Abs(Width(grid, 0) + Width(grid, 1) + Width(grid, 2) - grid.Scroll.ViewportWidth) < 1,
                "Resizing the native viewport left stale star widths.");

            using var constraints = new FlatTreeDataGridSource<Item>([new("Measured minimum and maximum")]);
            constraints.Columns.Add(new TextColumn<Item, string>("Auto constraints", x => x.Name, width: GridLength.Star,
                options: new() { MinWidth = GridLength.Auto, MaxWidth = GridLength.Auto }));
            constraints.Columns.Add(new TextColumn<Item, string>("Remainder", x => x.Name, width: GridLength.Star));
            grid.Model = constraints;
            await Task.Delay(200);
            Check(Width(grid, 0) > 150 && Width(grid, 0) < 400, "Auto min/max did not initialize from measured native content.");
            Check(Math.Abs(Width(grid, 0) + Width(grid, 1) - grid.Scroll.ViewportWidth) < 1, "Auto constraint measurement did not redistribute stars.");

            using var hierarchy = new HierarchicalTreeDataGridSource<Item>([new("Expander with measured constraints")]);
            hierarchy.Columns.Add(new HierarchicalExpanderColumn<Item>(new TextColumn<Item, string>("Hierarchy", x => x.Name,
                width: GridLength.Star, options: new() { MinWidth = GridLength.Auto, MaxWidth = GridLength.Auto }), _ => Array.Empty<Item>()));
            grid.Model = hierarchy;
            await Task.Delay(200);
            Check(Width(grid, 0) > 150, "Expander did not forward width measurements to its inner presentation.");
            grid.Model = null;
        }
        finally { grid.Model = null; grid.Width = previousWidth; }
        Console.WriteLine("UNO_RUNTIME_COLUMN_SIZING_PASSED: native auto/header measurement, live text growth, monotonic widths, min/max redistribution, viewport resize, Auto constraints, expander inner sizing");
    }
    private static double Width(Uno.Controls.TreeDataGrid grid, int column) =>
        grid.RowsPresenter.RealizedCells.First(x => x.ColumnIndex == column).ActualWidth;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
