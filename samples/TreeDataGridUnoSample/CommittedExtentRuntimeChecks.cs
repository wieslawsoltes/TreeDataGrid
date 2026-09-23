using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using Windows.Foundation;

namespace TreeDataGridUnoSample;

/// <summary>Verifies hosted extents through the public, package-consumer presenter contracts.</summary>
internal static class CommittedExtentRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        await RunAsync(page, 2);
        await RunAsync(page, 1024);
        Console.WriteLine("UNO_RUNTIME_COMMITTED_EXTENT_PASSED: 2/1024 columns, zero estimator calls for hosted geometry, changed commits, cardinality/identity guards, standalone fallback and allocation-free warm queries");
    }

    private static async Task RunAsync(MainPage page, int count)
    {
        var previous = page.Content;
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        for (var index = 0; index < count; ++index)
            source.Columns.Add(new TreeDataGridCore.Models.TextColumn<Item, string>("Name", item => item.Name, width: new(60)));
        using var presentation = TreeDataGridPresentation.Create(source);
        var columns = new CountingColumns();
        columns.AddRange(presentation.Columns.Cast<CellColumn>());
        var template = (ControlTemplate)new CommittedExtentResources()["ExtentRowTemplate"];
        var parent = new TreeDataGridRowsPresenter
        {
            Columns = columns, Items = presentation.Rows, ElementFactory = new Factory(template),
        };
        var scroll = new ScrollViewer
        {
            Width = 240, Height = 120, Content = parent,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var standalone = new CommittedExtentProbe { Items = columns };
        try
        {
            Check(standalone.Extent(321) == CountingColumns.Sentinel && columns.Estimates == 1,
                "Standalone extent calculation bypassed the public estimator.");
            standalone.Items = null;
            page.Content = scroll;
            await Settle();
            var row = parent.TryGetElement(0) ?? throw new InvalidOperationException("The public presenter did not realize its first row.");
            var cells = row.CellsPresenter as CommittedExtentProbe ??
                throw new InvalidOperationException("The compiled row template did not supply the extent probe.");
            var calls = columns.Estimates;
            Check(cells.Extent(double.PositiveInfinity) == count * 60d && columns.Estimates == calls,
                "A hosted cell presenter rescanned its columns instead of using committed geometry.");
            columns.SetColumnWidth(count - 1, new Microsoft.UI.Xaml.GridLength(97));
            await Settle();
            Check(ReferenceEquals(parent.TryGetElement(0), row), "A width update replaced its native row.");
            calls = columns.Estimates;
            Check(cells.Extent(19) == count * 60d + 37 && columns.Estimates == calls,
                "A hosted extent query retained an obsolete width or used the clipping constraint.");

            for (var index = 0; index < 1024; ++index) cells.Extent(19);
            var sum = 0d;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 4096; ++index) sum += cells.Extent(19);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0 && columns.Estimates == calls && sum == 4096 * (count * 60d + 37),
                $"Hosted extent queries allocated {allocated} bytes or evaluated an estimator.");

            // Collection notifications precede the next native measure/geometry
            // commit. The public query must not return that older cardinality.
            columns.Add((CellColumn)presentation.Columns[0]);
            calls = columns.Estimates;
            Check(cells.Extent(99) == CountingColumns.Sentinel && columns.Estimates == calls + 1,
                "A column-count mismatch reused old committed geometry.");
            columns.RemoveAt(count);
            await Settle();
            var different = new CountingColumns();
            different.AddRange(presentation.Columns.Cast<CellColumn>());
            try
            {
                cells.Items = different;
                Check(cells.Extent(99) == CountingColumns.Sentinel && different.Estimates == 1,
                    "An unrelated column collection borrowed the parent's extent.");
                cells.Items = columns;
                calls = columns.Estimates;
                Check(cells.Extent(99) == count * 60d + 37 && columns.Estimates == calls,
                    "Restoring the matching collection did not restore committed extent lookup.");
            }
            finally { different.Clear(); }
            cells.Items = null;
            Check(cells.Extent(99) == 0, "An unconfigured presenter retained its old extent.");
        }
        finally
        {
            standalone.Items = null;
            parent.Items = null;
            parent.Columns = null;
            scroll.Content = null;
            columns.Clear();
            page.Content = previous;
        }
        async Task Settle() { await Task.Delay(100); scroll.UpdateLayout(); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Item { public string Name => "Item"; }
    private sealed class Factory(ControlTemplate template) : TreeDataGridElementFactory
    {
        protected override Control CreateElement(object? data) => data is TreeDataGridCore.Models.IRow
            ? new TreeDataGridRow { Template = template } : base.CreateElement(data);
    }
    private sealed class CountingColumns : ColumnListBase<CellColumn>, IColumns
    {
        internal const double Sentinel = 777;
        internal int Estimates;
        double IColumns.GetEstimatedWidth(double constraint) { ++Estimates; return Sentinel; }
    }
}

/// <summary>Public only for compiled sample XAML; exercises the protected presenter contract.</summary>
public sealed partial class CommittedExtentProbe : TreeDataGridCellsPresenter
{
    internal double Extent(double constraint) => CalculateSizeU(new Size(constraint, 40));
}
