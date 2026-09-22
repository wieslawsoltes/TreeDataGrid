using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using Windows.Foundation;

namespace TreeDataGridUnoSample;

/// <summary>Native deferred-viewport, target-rectangle and request-retirement regressions.</summary>
internal static class BringIntoViewRuntimeChecks
{
    public static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 240)
            .Select(index => new Item($"Item {index}", index == 10 ? 480 : 28 + index % 5 * 9)));
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TreeDataGridCore.Models.TextColumn<Item, string>("Name", item => item.Name, width: new(360)));
        using var view = TreeDataGridPresentation.Create(source);
        var rows = new TreeDataGridRowsPresenter { Items = view.Rows, Columns = view.Columns, ElementFactory = new Factory() };
        var scroll = new ScrollViewer
        {
            Width = 300, Height = 180, Content = rows,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        try
        {
            page.Content = scroll;
            await Settle();
            foreach (var index in new[] { 160, 239, 3, 180 })
            {
                Check(rows.BringIntoView(index) is TreeDataGridRow, $"No container returned for row {index}.");
                await Settle();
                Verify(index);
            }

            // An oversized row is intentionally not wholly visible. Its explicit
            // local rectangle, rather than its estimated row bounds, must fit.
            var target = new Rect(0, 320, 80, 40);
            Check(rows.BringIntoView(10, target) is TreeDataGridRow, "No tall-row container returned.");
            await Settle();
            Check(ReferenceEquals(rows.TryGetElement(10)?.Model, items[10]), "The tall target was recycled away.");
            var top = rows.GetRowStart(10) + target.Y;
            Check(top >= scroll.VerticalOffset - 1 && top + target.Height <= scroll.VerticalOffset + scroll.ViewportHeight + 1,
                "The explicit rectangle inside a tall row was not brought into view.");

            // Both requests occur before either queued correction can run.
            rows.BringIntoView(220);
            rows.BringIntoView(2);
            await Settle();
            Verify(2);
            Check(rows.TryGetElement(220) is null, "An obsolete deferred request overrode the newer target.");

            rows.BringIntoView(210);
            rows.Items = null;
            scroll.ChangeView(0, 0, null, true);
            await Settle();
            Check(rows.RealizedRows.Count == 0 && rows.RealizedCells.Count == 0 && scroll.VerticalOffset == 0,
                "A deferred request survived source retirement.");

            rows.Items = view.Rows;
            await Settle();
            rows.BringIntoView(200);
            scroll.Content = null;
            await Settle();
            Check(rows.RealizedRows.Count == 0 && rows.RealizedCells.Count == 0,
                "Unloading a pending bring request retained active rows or cells.");
            Check(rows.BringIntoView(20) is null, "An unloaded presenter accepted a new bring request.");

            scroll.Content = rows;
            scroll.ChangeView(0, 0, null, true);
            await Settle();
            Check(rows.BringIntoView(40) is not null, "The presenter did not recover after reattachment.");
            await Settle();
            Verify(40);
            Console.WriteLine("UNO_RUNTIME_BRING_INTO_VIEW_PASSED: measured distant/last/reverse targets, tall-row TargetRect, superseded requests, source retirement, unload and reattach");
        }
        finally
        {
            rows.Items = null;
            rows.Columns = null;
            scroll.Content = null;
            page.Content = previous;
        }

        void Verify(int index)
        {
            Check(ReferenceEquals(rows.TryGetElement(index)?.Model, items[index]), $"Row {index} lost its current model.");
            var top = rows.GetRowStart(index);
            var bottom = top + rows.GetRowHeight(index);
            Check(top >= scroll.VerticalOffset - 1 && bottom <= scroll.VerticalOffset + scroll.ViewportHeight + 1,
                $"Row {index} is outside the measured viewport: row={top}+{bottom - top}, view={scroll.VerticalOffset}+{scroll.ViewportHeight}.");
            Check(rows.RealizedRows.Count < 32 && rows.RealizedCells.Count < 32,
                "BringIntoView realized the intervening source instead of a bounded viewport.");
            foreach (var row in rows.RealizedRows)
                Check(ReferenceEquals(row.Model, items[row.RowIndex]), "A reused row retained the wrong source model.");
        }
        async Task Settle()
        {
            // Exercise native dispatcher/layout delivery, without issuing a
            // second BringIntoView from the test or relaxing final assertions.
            await Task.Delay(100);
            scroll.UpdateLayout();
            await Task.Delay(100);
            scroll.UpdateLayout();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed record Item(string Name, double Height);
    private sealed partial class VariableRow : TreeDataGridRow
    {
        protected override void OnRealized(int rowIndex)
        {
            Height = ((Item)Model!).Height;
            base.OnRealized(rowIndex);
        }
    }
    private sealed class Factory : TreeDataGridElementFactory
    {
        protected override Control CreateElement(object? data) => data is TreeDataGridCore.Models.IRow
            ? new VariableRow() : base.CreateElement(data);
        protected override string GetDataRecycleKey(object? data) => data is TreeDataGridCore.Models.IRow
            ? typeof(VariableRow).FullName! : base.GetDataRecycleKey(data);
    }
}
