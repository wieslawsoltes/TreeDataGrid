using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Review regressions through loaded rows, public DPs and Core sources.</summary>
internal static class ReviewPresenterRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        var grid = new Uno.Controls.TreeDataGrid { Width = 620, Height = 280 };
        using var oldSource = Create("Old");
        using var replacement = Create("New");
        var registrations = new List<(DependencyObject Owner, DependencyProperty Property, long Token)>();
        try
        {
            page.Content = grid;
            grid.Model = oldSource;
            await Settle();
            var row = grid.TryGetRow(0) ?? throw new InvalidOperationException("No loaded row.");
            var invoked = false;
            Register(row, FrameworkElement.StyleProperty, () =>
            {
                if (invoked) return;
                invoked = true;
                grid.Model = replacement;
                grid.UpdateLayout();
            });
            grid.RowStyle = new Style { TargetType = typeof(TreeDataGridRow) };
            await Settle();
            Check(invoked && ReferenceEquals(grid.Model, replacement), "Style callback did not preserve source replacement.");
            VerifyRows(replacement);
            ClearRegistrations();

            // A newer style request within the same source generation must win.
            var latest = new Style { TargetType = typeof(TreeDataGridRow) };
            invoked = false;
            Register(grid.TryGetRow(0)!, FrameworkElement.StyleProperty, () =>
            {
                if (invoked) return;
                invoked = true;
                grid.RowStyle = latest;
                grid.UpdateLayout();
            });
            grid.RowStyle = new Style { TargetType = typeof(TreeDataGridRow) };
            await Settle();
            Check(invoked && grid.RowsPresenter!.RealizedRows.All(x => ReferenceEquals(x.Style, latest)),
                "An obsolete style request overwrote the nested style.");
            ClearRegistrations();

            // Force selection synchronization to invoke a cell DP callback in
            // the same-presentation column-refresh path, not via reflection.
            var cell = (TreeDataGridCell)grid.TryGetCell(0, 0)!;
            cell.IsSelected = true;
            invoked = false;
            Register(cell, TreeDataGridCell.IsSelectedProperty, () =>
            {
                if (invoked || cell.IsSelected) return;
                invoked = true;
                grid.Model = oldSource;
                grid.UpdateLayout();
            });
            replacement.Columns[0].Width = new TreeDataGridCore.GridLength(180);
            await Settle();
            Check(invoked && ReferenceEquals(grid.Model, oldSource), "Column synchronization lost the callback's new source.");
            VerifyRows(oldSource);
            ClearRegistrations();

            Check(grid.BeginEdit(0, 0), "Recovered grid could not open its editor.");
            grid.EditingCell!.EditingText = "Reviewed writeback";
            Check(grid.CommitEdit() && ((Item)oldSource.Rows[0].Model!).Name == "Reviewed writeback",
                "Recovered editor did not write to the current Core row.");
            grid.Model = null;
            Check(grid.RowsPresenter!.RealizedCells.Count == 0 && oldSource.Rows.Count == 100 && replacement.Rows.Count == 100,
                "Retirement retained native cells or disposed caller-owned data.");
            Console.WriteLine("UNO_RUNTIME_REVIEW_PRESENTER_CALLBACKS_PASSED: loaded style replacement, nested style precedence, same-presentation refresh, new Core identities, editor recovery and retirement");
        }
        finally { ClearRegistrations(); grid.Model = null; page.Content = previous; }
        void Register(DependencyObject owner, DependencyProperty property, Action action)
        {
            var token = owner.RegisterPropertyChangedCallback(property, (_, _) => action());
            registrations.Add((owner, property, token));
        }
        void ClearRegistrations()
        {
            foreach (var item in registrations) item.Owner.UnregisterPropertyChangedCallback(item.Property, item.Token);
            registrations.Clear();
        }
        async Task Settle() { await Task.Delay(120); grid.UpdateLayout(); }
        void VerifyRows(FlatTreeDataGridSource<Item> source)
        {
            Check(grid.RowsPresenter!.RealizedRows.Count > 1, "The native regression requires multiple loaded rows.");
            foreach (var current in grid.RowsPresenter.RealizedCells)
            {
                Check(ReferenceEquals(current.RowModel, source.Rows[current.RowIndex].Model), "A native cell retained an old Core model.");
                Check(ShowcaseRuntimeChecks.Descendants(current).OfType<TextBlock>().Any(x => x.Text == ((Item)current.RowModel!).Name),
                    "The new native cell text did not match its Core model.");
            }
        }
    }
    private static FlatTreeDataGridSource<Item> Create(string prefix)
    {
        var source = new FlatTreeDataGridSource<Item>(new ObservableCollection<Item>(Enumerable.Range(0, 100).Select(i => new Item(prefix + i))));
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name, (x, value) => x.Name = value, width: new(200)));
        return source;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Item(string name) { public string Name { get; set; } = name; }
}
