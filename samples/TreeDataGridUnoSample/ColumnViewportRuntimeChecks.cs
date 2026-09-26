using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Uno.Controls.Primitives;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Exercises the actual native header-presenter anchor dispatch, including its fallback.</summary>
internal static class ColumnViewportRuntimeChecks
{
    internal static void Run()
    {
        var first = new ProbeColumn(double.NaN);
        var second = new ProbeColumn(20);
        var third = new ProbeColumn(40);
        var columns = new U.ColumnListBase<ProbeColumn> { first, second, third };
        var presenter = new AnchorPresenter { Items = columns };
        ProbeColumn? replacementFirst = null, replacementSecond = null;
        try
        {
            Check(presenter.Anchor(1000, 1001, 3) == (2, 60d), "The native fallback lost the measured-width mean.");
            var reads = first.Reads + second.Reads + third.Reads;
            for (var i = 0; i < 4096; ++i)
                Check(presenter.Anchor(1000 + i, 1001 + i, 3) == (2, 60d), "A warm native fallback changed its anchor.");
            Check(first.Reads + second.Reads + third.Reads == reads,
                "The native fallback reread application widths after the hit-test snapshot was already committed.");

            first.Change(10);
            Check(presenter.Anchor(15, 16, 3) == (1, 10d), "A width notification did not replace the native anchor snapshot.");
            first.Change(double.NaN);
            second.Change(double.PositiveInfinity);
            Check(presenter.Anchor(1000, 1001, 3) == (2, 60d),
                "A nonfinite mean replaced the native presenter's last usable estimate.");

            first.Change(10);
            first.OnRead = () =>
            {
                first.OnRead = null;
                columns.Clear();
                columns.Add(replacementFirst = new(120));
                columns.Add(replacementSecond = new(180));
                Check(columns.GetColumnAt(150) == (1, 120d), "The nested geometry query did not commit replacement widths.");
            };
            Check(presenter.Anchor(15, 16, 3) == (0, 0d), "The native presenter published an anchor from a retired width getter.");
            Check(columns.Count == 2 && first.Subscribers + second.Subscribers + third.Subscribers == 0,
                "Replacing columns through a width getter retained old observer ownership.");
            Check(presenter.Anchor(150, 151, 2) == (1, 120d), "Replacement geometry was not reusable by the native presenter.");
            Console.WriteLine("UNO_RUNTIME_COLUMN_VIEWPORT_SNAPSHOT_PASSED: native header-presenter fallback, 4096 warm queries without width rereads, live invalidation, finite estimate recovery and nested replacement geometry");
        }
        finally
        {
            try { presenter.Items = null; }
            finally { columns.Clear(); }
        }
        Check((replacementFirst?.Subscribers ?? 0) + (replacementSecond?.Subscribers ?? 0) == 0,
            "The native viewport check retained replacement observers after cleanup.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // This is a native dependency object and the production protected dispatch
    // route, not a copied estimator. Attachment/rendering remain covered by the
    // enclosing existing consumer suite; this check intentionally isolates lookup.
    private sealed partial class AnchorPresenter : TreeDataGridColumnHeadersPresenter
    {
        internal (int index, double position) Anchor(double start, double end, int count) =>
            GetOrEstimateAnchorElementForViewport(start, end, count);
    }

    private sealed class ProbeColumn(double width) : U.IUpdateColumnLayout
    {
        private PropertyChangedEventHandler? _changed;
        private double _actual = width;
        internal Action? OnRead;
        internal int Reads, Subscribers;
        public double ActualWidth { get { ++Reads; var value = _actual; OnRead?.Invoke(); return value; } }
        public bool? CanUserResize => true;
        public object? Header => "Viewport probe";
        public GridLength Width { get; private set; } = new(80);
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public double CellMeasured(double measured, int rowIndex) => measured;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(GridLength value) => Width = value;
        internal void Change(double value) { _actual = value; _changed?.Invoke(this, new(nameof(ActualWidth))); }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
}
