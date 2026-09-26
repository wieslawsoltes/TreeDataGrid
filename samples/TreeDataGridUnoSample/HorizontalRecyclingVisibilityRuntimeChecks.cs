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
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Loaded native cells must not collapse between same-pass horizontal reuse.</summary>
internal static class HorizontalRecyclingVisibilityRuntimeChecks
{
    internal static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        grid.Model = null;
        var oldFactory = grid.ElementFactory;
        var oldWidth = grid.Width;
        var oldHeight = grid.Height;
        var oldRowHeight = grid.RowHeight;
        var oldCache = grid.RowsPresenter!.CacheLength;
        var factory = new Factory();
        var items = new ObservableCollection<Model>(Enumerable.Range(0, 100).Select(i => new Model("Row " + i)));
        using var source = new FlatTreeDataGridSource<Model>(items);
        for (var i = 0; i < 96; ++i)
            source.Columns.Add(new TextColumn<Model, string>("C" + i, row => row.Text, width: new(80)));
        var cancelOnClearing = false;
        grid.CellClearing += OnClearing;
        try
        {
            grid.Width = 340;
            grid.Height = 200;
            grid.RowHeight = 28;
            grid.RowsPresenter.CacheLength = 0;
            grid.ElementFactory = factory;
            grid.Model = source;
            await Settle();
            grid.Scroll!.ChangeView(40, 0, null, true);
            await Settle();
            grid.Scroll.ChangeView(0, 0, null, true);
            await Settle();
            var created = factory.Cells.Count;
            foreach (var cell in factory.Cells) cell.ResetCounters();
            foreach (var x in new[] { 16 * 80d, 40 * 80d, 72 * 80d, 0d, 8 * 80d })
            {
                grid.Scroll.ChangeView(x, 0, null, true);
                await Settle();
                VerifyCurrentCells();
                Check(factory.Cells.Count == created, "Horizontal reuse created replacement native template controls.");
                Check(factory.Cells.All(cell => cell.ReusedWithCollapse == 0),
                    "A cell collapsed inside a successful same-row rebind instead of retaining its native visibility.");
            }
            Check(factory.Cells.Sum(cell => cell.SuccessfulRebinds) > 0,
                "The visibility fixture did not exercise retained horizontal native controls.");

            // Unused controls cannot remain visible when the synchronous measure
            // finishes, even when the viewport shrinks and no successor uses them.
            grid.Width = 160;
            await Settle();
            VerifyCurrentCells();
            Check(factory.Cells.Where(cell => cell.RowIndex < 0).All(cell => cell.Visibility == Visibility.Collapsed),
                "A surplus recycled cell remained visible after measure completion.");

            // Source retirement from application lifecycle code must invalidate
            // the old measure and finish every pending visual decision now.
            cancelOnClearing = true;
            grid.Scroll.ChangeView(64 * 80, 0, null, true);
            await Settle();
            Check(!cancelOnClearing && grid.Model is null, "The source-retirement callback was not executed.");
            Check(factory.Cells.All(cell => cell.Model is null && cell.RowModel is null && cell.Visibility == Visibility.Collapsed),
                "A cancelled horizontal measure retained old model content or visible cells.");
            Check(source.Rows.Count == 100, "Native retirement disposed the borrowed shared Core source.");

            grid.Model = source;
            grid.Scroll.ChangeView(0, 0, null, true);
            await Settle();
            VerifyCurrentCells();
            grid.Model = null;
            Check(factory.Cells.All(cell => cell.Model is null && cell.Visibility == Visibility.Collapsed),
                "Horizontal visibility optimization prevented normal source cleanup after recovery.");
            Console.WriteLine("UNO_RUNTIME_HORIZONTAL_RECYCLING_VISIBILITY_PASSED: five disjoint/reverse windows, stable controls, no same-row rebind collapse, current Core models/text, bounded realization, surplus hide, callback retirement and recovery");
        }
        finally
        {
            grid.CellClearing -= OnClearing;
            grid.Model = null;
            grid.ElementFactory = oldFactory;
            grid.RowsPresenter.CacheLength = oldCache;
            grid.RowHeight = oldRowHeight;
            grid.Width = oldWidth;
            grid.Height = oldHeight;
            foreach (var cell in factory.Cells) cell.StopObserving();
        }

        void OnClearing(object? sender, Uno.Controls.TreeDataGridCellEventArgs args)
        {
            if (!cancelOnClearing) return;
            cancelOnClearing = false;
            grid.Model = null;
        }
        void VerifyCurrentCells()
        {
            var cells = grid.RowsPresenter!.RealizedCells;
            Check(cells.Count is > 0 and < 100, "The optimized native viewport was empty or unbounded.");
            foreach (var cell in cells)
            {
                Check(cell.Visibility == Visibility.Visible && ReferenceEquals(cell.RowModel, items[cell.RowIndex]),
                    "Retained visibility exposed a wrong Core row or hidden realized cell.");
                Check(ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().Any(text => text.Text == items[cell.RowIndex].Text),
                    "Reused native cell rendered a stale text value.");
            }
        }
        async Task Settle() { await Task.Delay(100); grid.UpdateLayout(); }
    }

    private sealed record Model(string Text);
    private sealed class Factory : TreeDataGridElementFactory
    {
        internal readonly List<ObservedCell> Cells = new();
        protected override Control CreateElement(object? data)
        {
            if (data is not U.ICell) return base.CreateElement(data);
            var result = new ObservedCell();
            Cells.Add(result);
            return result;
        }
        protected override string GetElementRecycleKey(Control element) => element is ObservedCell
            ? typeof(TreeDataGridTextCell).FullName! : base.GetElementRecycleKey(element);
    }
    private sealed class ObservedCell : TreeDataGridTextCell
    {
        private readonly long _token;
        private int _collapses;
        private int _before;
        private int _oldRow;
        private bool _wasVisible;
        internal int SuccessfulRebinds;
        internal int ReusedWithCollapse;
        internal ObservedCell() => _token = RegisterPropertyChangedCallback(VisibilityProperty, (sender, _) =>
        {
            if (((ObservedCell)sender).Visibility == Visibility.Collapsed) ++_collapses;
        });
        public override void BeginRebind()
        {
            _before = _collapses;
            _oldRow = RowIndex;
            _wasVisible = Visibility == Visibility.Visible;
            base.BeginRebind();
        }
        public override void EndRebind(bool realized)
        {
            if (realized && _wasVisible && _oldRow == RowIndex)
            {
                ++SuccessfulRebinds;
                if (_before != _collapses) ++ReusedWithCollapse;
            }
            base.EndRebind(realized);
        }
        internal void ResetCounters() { SuccessfulRebinds = ReusedWithCollapse = 0; }
        internal void StopObserving() => UnregisterPropertyChangedCallback(VisibilityProperty, _token);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
