using System;
using System.Linq;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using Windows.Foundation;

namespace TreeDataGridUnoSample;

/// <summary>Verifies the hosted extent path without timing-dependent assertions.</summary>
internal static class CommittedExtentRuntimeChecks
{
    internal static void Run()
    {
        Run(2);
        Run(1024);
        Console.WriteLine("UNO_RUNTIME_COMMITTED_EXTENT_PASSED: 2/1024 columns, zero estimator calls for hosted geometry, changed commits, cardinality/identity guards, standalone fallback and allocation-free warm queries");
    }

    private static void Run(int count)
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        for (var index = 0; index < count; ++index)
            source.Columns.Add(new TreeDataGridCore.Models.TextColumn<Item, string>("Name", item => item.Name, width: new(60)));
        using var presentation = TreeDataGridPresentation.Create(source);
        var columns = new CountingColumns();
        columns.AddRange(presentation.NativeColumns);
        var widths = Enumerable.Repeat(60d, count).ToArray();
        var parent = new TreeDataGridRowsPresenter { Columns = columns };
        var row = new TreeDataGridRow();
        var cells = new ExtentProbe();
        try
        {
            // A standalone public presenter must still invoke the caller's
            // estimator. Its result is deliberately distinct from real widths.
            cells.Items = columns;
            Check(cells.Extent(321) == CountingColumns.Sentinel && columns.Estimates == 1,
                "Standalone extent calculation bypassed the public estimator.");
            cells.Reset();

            parent.Geometry.CommitSpan(widths);
            row.Realize(parent, new(), null, columns, presentation.Rows, 0);
            cells.Attach(row);
            var calls = columns.Estimates;
            Check(cells.Extent(double.PositiveInfinity) == count * 60d && columns.Estimates == calls,
                "A hosted cell presenter rescanned its columns instead of using committed geometry.");
            widths[^1] = 97;
            parent.Geometry.CommitSpan(widths);
            Check(cells.Extent(19) == count * 60d + 37 && columns.Estimates == calls,
                "A hosted extent query retained an obsolete width or used the clipping constraint.");

            // Warm the exact production query before measuring thread allocation.
            for (var index = 0; index < 1024; ++index) cells.Extent(19);
            var sum = 0d;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 4096; ++index) sum += cells.Extent(19);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0 && columns.Estimates == calls && sum == 4096 * (count * 60d + 37),
                $"Hosted extent queries allocated {allocated} bytes or evaluated an estimator.");

            parent.Geometry.CommitSpan(widths.AsSpan(0, count - 1));
            Check(cells.Extent(99) == CountingColumns.Sentinel && columns.Estimates == ++calls,
                "A column-count mismatch reused old committed geometry.");
            parent.Geometry.CommitSpan(widths);
            var different = new CountingColumns();
            different.AddRange(presentation.NativeColumns);
            try
            {
                cells.Items = different;
                Check(cells.Extent(99) == CountingColumns.Sentinel && different.Estimates == 1,
                    "An unrelated column collection borrowed the parent's extent.");
                cells.Items = columns;
                Check(cells.Extent(99) == count * 60d + 37 && columns.Estimates == calls,
                    "Restoring the matching collection did not restore committed extent lookup.");
            }
            finally { different.Clear(); }
            cells.Reset();
            Check(cells.Extent(99) == 0, "A retired presenter retained its old extent.");
        }
        finally
        {
            cells.Reset();
            row.Unrealize();
            row.Release();
            parent.Reset();
            columns.Clear();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Item { public string Name => "Item"; }
    private sealed class ExtentProbe : TreeDataGridCellsPresenter
    {
        internal double Extent(double constraint) => CalculateSizeU(new Size(constraint, 40));
    }
    private sealed class CountingColumns : ColumnListBase<CellColumn>, IColumns
    {
        internal const double Sentinel = 777;
        internal int Estimates;
        double IColumns.GetEstimatedWidth(double constraint) { ++Estimates; return Sentinel; }
    }
}
