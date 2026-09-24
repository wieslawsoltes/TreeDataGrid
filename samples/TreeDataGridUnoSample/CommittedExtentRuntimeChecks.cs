using System;
using System.ComponentModel;
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
        ValidateStandaloneEstimates();
        await RunAsync(page, 2);
        await RunAsync(page, 1024);
        Console.WriteLine("UNO_RUNTIME_COMMITTED_EXTENT_PASSED: 2/1024 columns, zero estimator calls for hosted geometry, changed commits, cardinality/identity guards, standalone fallback and allocation-free warm queries");
    }

    private static void ValidateStandaloneEstimates()
    {
        var first = new EstimateColumn(20);
        var last = new EstimateColumn(30);
        var replacement = new EstimateColumn(123);
        var star = new EstimateColumn(999, true);
        var columns = new ColumnListBase<EstimateColumn> { first, last };
        var probe = new CommittedExtentProbe { Items = columns };
        try
        {
            Check(probe.Extent(double.PositiveInfinity) == 50 && first.ActualReads == 1 && last.ActualReads == 1,
                "Standalone native estimation read a measured width more than once.");
            first.OnActual = () =>
            {
                columns[0] = replacement;
                Check(probe.Extent(double.PositiveInfinity) == 153,
                    "A nested native extent query did not use the replacement column.");
            };
            Check(probe.Extent(double.PositiveInfinity) == 153,
                "The returning native extent query published retired column geometry.");
            last.OnActual = () => replacement.SetActual(223);
            Check(probe.Extent(double.PositiveInfinity) == 253,
                "A later getter invalidated an earlier width without restarting native estimation.");
            columns.Clear();
            columns.Add(star);
            star.OnMinimum = columns.Clear;
            Check(probe.Extent(500) == 0 && columns.Count == 0,
                "Clearing a star column during native estimation fabricated a viewport extent.");
        }
        finally
        {
            probe.Items = null;
            columns.Clear();
        }
        Check(first.Subscribers == 0 && last.Subscribers == 0 && replacement.Subscribers == 0 && star.Subscribers == 0,
            "Standalone estimate callbacks retained column subscriptions after cleanup.");
        Console.WriteLine("UNO_RUNTIME_STANDALONE_ESTIMATES_PASSED: single reads, nested same-count replacement, earlier-width invalidation, star retirement and subscription cleanup through the native presenter");
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

    private sealed class EstimateColumn(double actual, bool star = false) : IUpdateColumnLayout
    {
        private static readonly PropertyChangedEventArgs ActualChanged = new(nameof(ActualWidth));
        private double _actual = actual;
        private PropertyChangedEventHandler? _changed;
        internal Action? OnActual, OnMinimum;
        internal int ActualReads, Subscribers;
        public Microsoft.UI.Xaml.GridLength Width => star ? new(1, Microsoft.UI.Xaml.GridUnitType.Star) : Microsoft.UI.Xaml.GridLength.Auto;
        public double ActualWidth { get { ++ActualReads; var value = _actual; Invoke(ref OnActual); return value; } }
        public double MinActualWidth { get { Invoke(ref OnMinimum); return 10; } }
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public bool? CanUserResize => true;
        public object? Header => null;
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
        public double CellMeasured(double width, int rowIndex) => throw new InvalidOperationException("Estimate fixture cannot measure.");
        public bool CommitActualWidth() => throw new InvalidOperationException("Estimate fixture cannot commit.");
        public void CalculateStarWidth(double availableWidth, double totalStars) => throw new InvalidOperationException("Estimate fixture cannot solve layout.");
        public void SetWidth(Microsoft.UI.Xaml.GridLength width) => throw new InvalidOperationException("Estimate fixture cannot change policy.");
        internal void SetActual(double value) { _actual = value; _changed?.Invoke(this, ActualChanged); }
        private static void Invoke(ref Action? callback)
        {
            var current = callback;
            callback = null;
            current?.Invoke();
        }
    }
}

/// <summary>Public only for compiled sample XAML; exercises the protected presenter contract.</summary>
public sealed partial class CommittedExtentProbe : TreeDataGridCellsPresenter
{
    internal double Extent(double constraint) => CalculateSizeU(new Size(constraint, 40));
}
