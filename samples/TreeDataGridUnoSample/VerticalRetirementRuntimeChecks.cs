using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Primitives;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Loaded vertical recycling preserves controls and retires every old model identity.</summary>
internal static class VerticalRetirementRuntimeChecks
{
    internal static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        grid.Model = null;
        var previousFactory = grid.ElementFactory;
        var previousWidth = grid.Width;
        var previousHeight = grid.Height;
        var previousRowHeight = grid.RowHeight;
        var previousCache = grid.RowsPresenter!.CacheLength;
        var factory = new Factory();
        var items = new ObservableCollection<Model>(Enumerable.Range(0, 240).Select(i => new Model("Row " + i)));
        using var source = new FlatTreeDataGridSource<Model>(items);
        for (var column = 0; column < 32; ++column)
            source.Columns.Add(new TextColumn<Model, string>("C" + column, x => x.Text,
                (x, value) => x.Text = value, width: new(80)));
        var retire = false;
        var prepared = 0;
        var cleared = 0;
        grid.CellClearing += OnClearing;
        grid.CellPrepared += OnPrepared;
        try
        {
            grid.Width = 340;
            grid.Height = 240;
            grid.RowHeight = 28;
            grid.RowsPresenter.CacheLength = 0;
            grid.ElementFactory = factory;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            grid.Scroll.ChangeView(0, 28, null, true);
            await Settle();
            grid.Scroll.ChangeView(0, 0, null, true);
            await Settle();
            var created = factory.Cells.Count;
            foreach (var index in new[] { 30, 80, 150, 20, 0 })
            {
                grid.Scroll.ChangeView(0, index * 28d, null, true);
                await Settle();
                Verify();
                Check(factory.Cells.Count == created, "Vertical recycling replaced compatible native controls.");
            }
            Check(factory.Cells.All(x => x.RetiredIdentityCleared),
                "A recycled column retained its public old row/model identity after Unrealize returned.");

            // A recycled container must subscribe only to its current row.
            var first = (TreeDataGridTextCell)grid.RowsPresenter.RealizedCells.First();
            var current = (Model)first.RowModel!;
            current.Text = "Current notification";
            Check(first.Value == current.Text, "Vertical recycling lost current model notifications.");
            Check(grid.BeginEdit(first.RowIndex, first.ColumnIndex), "Vertical recycling lost native editing.");
            grid.EditingCell!.EditingText = "Current edit";
            Check(grid.CommitEdit() && current.Text == "Current edit", "Reused cell edited an old Core row.");

            // Mixed-axis travel also exercises column compatibility, ordinary
            // native measurement and retirement of a different visible range.
            grid.Scroll.ChangeView(20 * 80, 170 * 28, null, true);
            await Settle();
            Verify();
            grid.Scroll.ChangeView(0, 180 * 28, null, true);
            await Settle();
            Verify();

            // A source change while old cells retire must not let the returning
            // retirement loop publish into the replacement presentation.
            retire = true;
            grid.Scroll.ChangeView(0, 30 * 28, null, true);
            await Settle();
            Check(!retire && grid.Model is null && prepared == cleared,
                "Retirement during vertical clearing lost lifecycle balance.");
            Check(factory.Cells.All(x => x.Model is null && x.RowModel is null && x.Visibility == Visibility.Collapsed),
                "A retired row left a visible or bound cell.");
            Check(items.All(x => x.Subscribers == 0), "Vertical retirement left old model subscriptions: " + string.Join(", ", items.Select((x, i) => $"{i}={x.Subscribers}").Where(x => !x.EndsWith("=0", StringComparison.Ordinal))));
            Check(source.Rows.Count == items.Count, "View retirement disposed the shared Core source.");

            grid.Model = source;
            grid.Scroll.ChangeView(0, 0, null, true);
            await Settle();
            Verify();
            grid.Model = null;
            Check(prepared == cleared && items.All(x => x.Subscribers == 0),
                "Recovered vertical rebind leaked subscriptions or lifecycle notifications.");
            Console.WriteLine("UNO_RUNTIME_VERTICAL_RETIREMENT_PASSED: five disjoint/reverse vertical windows, stable native controls and current values, immediate old model retirement, live updates/edit, mixed-axis fallback, bounded realization, callback retirement and recovery");
        }
        finally
        {
            grid.CellClearing -= OnClearing;
            grid.CellPrepared -= OnPrepared;
            grid.Model = null;
            grid.ElementFactory = previousFactory;
            grid.RowsPresenter.CacheLength = previousCache;
            grid.RowHeight = previousRowHeight;
            grid.Width = previousWidth;
            grid.Height = previousHeight;
        }

        void OnClearing(object? sender, Uno.Controls.TreeDataGridCellEventArgs args)
        {
            ++cleared;
            var cell = (TreeDataGridCell)args.Cell;
            Check(cell.Model is not null && cell.RowModel is not null && cell.RowIndex == args.RowIndex,
                "Clearing notification lost its original model identity.");
            if (retire) { retire = false; grid.Model = null; }
        }
        void OnPrepared(object? sender, Uno.Controls.TreeDataGridCellEventArgs args)
        {
            ++prepared;
            Check(ReferenceEquals(grid.TryGetCell(args.ColumnIndex, args.RowIndex), args.Cell),
                "Prepared notification preceded the current native cell index registration.");
        }
        void Verify()
        {
            var cells = grid.RowsPresenter!.RealizedCells;
            Check(cells.Count is > 0 and < 100, "Vertical realization is empty or unbounded.");
            Check(prepared - cleared == cells.Count, "Vertical lifecycle events are not balanced with current realization.");
            var currentModels = new HashSet<Model>();
            foreach (var cell in cells)
            {
                var model = items[cell.RowIndex];
                currentModels.Add(model);
                Check(ReferenceEquals(cell.RowModel, model) && cell.Visibility == Visibility.Visible &&
                    ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().Any(x => x.Text == model.Text),
                    "Reused native column contains old text, visibility or Core row identity.");
            }
            Check(items.Where(x => !currentModels.Contains(x)).All(x => x.Subscribers == 0),
                "Completed vertical layout retained an offscreen model observation.");
        }
        async Task Settle() { await Task.Delay(100); grid.UpdateLayout(); }
    }

    private sealed class Model(string text) : INotifyPropertyChanged
    {
        private string _text = text;
        private PropertyChangedEventHandler? _handlers;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public string Text { get => _text; set { _text = value; _handlers?.Invoke(this, new(nameof(Text))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add => _handlers += value; remove => _handlers -= value; }
    }
    private sealed class Factory : TreeDataGridElementFactory
    {
        internal readonly List<ObservedCell> Cells = new();
        protected override Control CreateElement(object? data)
        {
            if (data is not U.ICell) return base.CreateElement(data);
            var cell = new ObservedCell();
            Cells.Add(cell);
            return cell;
        }
        protected override string GetElementRecycleKey(Control element) => element is ObservedCell
            ? typeof(TreeDataGridTextCell).FullName! : base.GetElementRecycleKey(element);
    }
    private sealed class ObservedCell : TreeDataGridTextCell
    {
        internal bool RetiredIdentityCleared = true;
        public override void Unrealize()
        {
            base.Unrealize();
            RetiredIdentityCleared &= RowIndex == -1 && ColumnIndex == -1 && Model is null && RowModel is null;
        }
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
