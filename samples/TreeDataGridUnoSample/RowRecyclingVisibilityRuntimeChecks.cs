using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using GridLength = Microsoft.UI.Xaml.GridLength;
using Uno.Controls;
using Uno.Controls.Primitives;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

internal static class RowRecyclingVisibilityRuntimeChecks
{
    public static async Task RunAsync(TreeDataGrid grid)
    {
        grid.Model = null;
        var oldFactory = grid.ElementFactory;
        var oldWidth = grid.Width;
        var oldHeight = grid.Height;
        var oldRowHeight = grid.RowHeight;
        var factory = new Factory();
        var callbacks = new List<(TreeDataGridCell Cell, long Token)>();
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 100).Select(i => new Item($"Row {i:D4}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        for (var i = 0; i < 32; ++i)
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
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            var row = grid.TryGetRow(0) ?? throw new InvalidOperationException("The fixture did not realize its first row.");
            var cells = row.CellsPresenter!.RealizedCells.ToDictionary(cell => cell.ColumnIndex);
            Check(cells.Count > 0, "The fixture did not realize any first-row cells.");
            var collapses = 0;
            foreach (var cell in cells.Values)
            {
                var token = cell.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (sender, _) =>
                {
                    if (((TreeDataGridCell)sender).Visibility == Visibility.Collapsed) ++collapses;
                });
                callbacks.Add((cell, token));
            }
            items[0] = new("Updated row");
            await Settle();
            VerifyFirstRow();
            Check(collapses == 0, $"Row replacement redundantly collapsed {collapses} retained child cells.");
            foreach (var direction in new[] { ListSortDirection.Ascending, ListSortDirection.Descending })
            {
                source.SortBy(source.Columns[0], direction);
                await Settle();
                VerifyFirstRow();
                Check(collapses == 0, $"Row sorting redundantly collapsed {collapses} retained child cells.");
            }
            grid.Scroll.ChangeView(1600, 0, null, true);
            await Settle();
            Check(collapses > 0, "Horizontal-only recycling incorrectly deferred local cell visibility.");
            Check(grid.RowsPresenter!.RealizedCells.Count is > 0 and < 100, "Recycling exceeded the viewport cell budget.");
            grid.Model = null;
            Check(factory.Cells.All(cell => cell.Model is null && cell.RowModel is null && cell.Visibility == Visibility.Collapsed),
                "Source retirement left a visible or bound recycled cell.");
            Console.WriteLine("UNO_RUNTIME_ROW_RECYCLING_VISIBILITY_PASSED: no child hide/show on row replacement/sorting; retained identity; normal horizontal recycling; source cleanup");

            void VerifyFirstRow()
            {
                Check(ReferenceEquals(grid.TryGetRow(0), row), "Rebinding replaced the retained row container.");
                var expected = source.Rows[0].Model;
                foreach (var pair in cells)
                {
                    Check(ReferenceEquals(row.TryGetCell(pair.Key), pair.Value), "Rebinding replaced the retained cell container.");
                    Check(ReferenceEquals(pair.Value.RowModel, expected) && pair.Value.Visibility == Visibility.Visible,
                        "A retained cell has stale model identity or visibility.");
                }
            }
        }
        finally
        {
            foreach (var callback in callbacks)
                callback.Cell.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, callback.Token);
            grid.Model = null;
            grid.ElementFactory = oldFactory;
            grid.RowHeight = oldRowHeight;
            grid.Width = oldWidth;
            grid.Height = oldHeight;
        }
        async Task Settle() { await Task.Delay(100); grid.UpdateLayout(); }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed record Item(string Name);
    private sealed class Factory : TreeDataGridElementFactory
    {
        public List<TreeDataGridCell> Cells { get; } = new();
        protected override Control CreateElement(object? data)
        {
            var element = base.CreateElement(data);
            if (data is UI.ICell && element is TreeDataGridCell cell) Cells.Add(cell);
            return element;
        }
    }
}
