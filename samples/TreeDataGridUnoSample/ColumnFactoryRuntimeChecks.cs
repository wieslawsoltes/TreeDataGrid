using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using Uno.Controls;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Column factory transactions through the public native package consumer.</summary>
internal static class ColumnFactoryRuntimeChecks
{
    internal static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 160).Select(i => new Item($"Factory row {i:D3}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        for (var i = 0; i < 10; ++i) source.Columns.Add(Definition($"C{i}"));
        var first = source.Columns[0];
        var made = new List<TrackedColumn>();
        Action? callback = null;
        var depth = 0; var maximumDepth = 0;
        var options = new Options();
        options.Inner.Columns["Old"] = model => Create(model, "Old");
        options.Inner.Columns["New"] = model => Create(model, "New");
        var expectedFailure = new InvalidOperationException("Expected native column factory failure.");
        options.Inner.Columns["Fail"] = _ => throw expectedFailure;
        var previousOptions = grid.PresentationOptions;
        var previousSelection = grid.SelectionMode;
        grid.Model = null;
        try
        {
            grid.PresentationOptions = options;
            grid.SelectionMode = TreeDataGridSelectionMode.Cell;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            callback = () =>
            {
                first.PresentationKey = "New";
                source.Columns.Move(0, 2);
                source.Columns[4].IsVisible = false;
                source.Columns.Add(Definition("Added"));
            };
            first.PresentationKey = "Old";
            await Settle();
            var view = options.Last!;
            var expected = source.Columns.Where(x => x.IsVisible).ToArray();
            Check(view.Columns.Count == expected.Length && expected.Select((model, i) =>
                ReferenceEquals(((CellColumn)view.Columns[i]).Model, model)).All(x => x),
                "Native columns did not converge to the latest Core order and visibility.");
            Check(made.Count == 2 && made[0].Key == "Old" && made[0].Disposals == 1 && made[1].Key == "New" &&
                made[1].Disposals == 0 && maximumDepth == 1, "An obsolete factory survived or synchronization recursed.");
            var visible = Array.IndexOf(expected, first);
            Check(grid.BringCellIntoView(10, visible), "The remapped column could not be brought into view.");
            await Settle();
            CheckCell(10, visible);
            Check(grid.SelectCell(10, visible) && source.Selection is ITreeDataGridCellSelectionModel<Item> selection &&
                selection.SelectedIndex.ColumnIndex == source.Columns.IndexOf(first) && selection.SelectedIndex.RowIndex == new TreeDataGridCore.IndexPath(10),
                "Native selection did not map the visible column to its current Core index.");
            Check(grid.BeginEdit(10, visible), "The remapped column did not open a native editor.");
            grid.EditingCell!.EditingText = "Edited after factory replacement";
            Check(grid.CommitEdit() && items[10].Name == "Edited after factory replacement",
                "Native editing wrote to an obsolete column or row.");
            first.Width = new(147);
            await Settle();
            CheckCell(10, visible);
            Check(Math.Abs(((TreeDataGridCell)grid.TryGetCell(visible, 10)!).ActualWidth - 147) < 0.5,
                "The committed replacement lost subsequent width updates.");

            var other = source.Columns[0];
            Exception? failure = null;
            try { other.PresentationKey = "Fail"; }
            catch (Exception error) { failure = error; }
            Check(ReferenceEquals(failure, expectedFailure), "Native factory failure did not preserve exception identity.");
            other.PresentationKey = "New";
            Check(grid.BringCellIntoView(120, visible), "A repaired factory prevented distant navigation.");
            await Settle();
            CheckCell(120, visible);
            Check(grid.RowsPresenter!.RealizedCells.Count < 2048, "Column replacement disabled bounded realization.");

            callback = () => grid.Model = null;
            first.PresentationKey = "Old";
            await Settle();
            Check(grid.Model is null && view.Columns.Count == 0 && grid.RowsPresenter!.RealizedCells.Count == 0 &&
                made.All(x => x.Disposals == 1), "A returning native factory resurrected a retired view or escaped cleanup.");
            Check(items.All(x => x.Subscribers == 0) && source.Rows.Count == 160,
                "Source retirement leaked bindings or disposed the caller-owned Core data.");
            Console.WriteLine("UNO_RUNTIME_COLUMN_FACTORY_TRANSACTIONS_PASSED: latest key/order/visibility, serial factories, native text/edit/selection/resize, failed-factory recovery, distant virtualization and source-retirement ownership");
        }
        finally
        {
            grid.Model = null;
            grid.PresentationOptions = previousOptions;
            grid.SelectionMode = previousSelection;
        }

        CellColumn Create(IColumn model, string key)
        {
            ++depth; maximumDepth = Math.Max(maximumDepth, depth);
            try
            {
                var result = new TrackedColumn((ValueColumn<Item, string>)model, key);
                made.Add(result);
                var action = callback; callback = null; action?.Invoke();
                return result;
            }
            finally { --depth; }
        }
        void CheckCell(int row, int column)
        {
            var cell = grid.TryGetCell(column, row) as TreeDataGridCell;
            Check(cell is not null && ReferenceEquals(cell.RowModel, items[row]) &&
                ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().Any(x => x.Text == items[row].Name),
                "A replacement column rendered an obsolete model or native text value.");
        }
        async Task Settle() { await Task.Delay(120); grid.UpdateLayout(); }
    }

    private static ValueColumn<Item, string> Definition(string header) => ValueColumn<Item, string>.FromDelegate(
        header, static item => item.Name, propertyName: nameof(Item.Name), setter: static (item, value) => item.Name = value, width: new(128));
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Options : ITreeDataGridPresentationOptions
    {
        internal readonly TreeDataGridPresentationOptions Inner = new();
        internal TreeDataGridPresentation? Last;
        public TreeDataGridPresentation Create(ITreeDataGridSource model) => Last = Inner.Create(model);
    }
    private sealed class TrackedColumn(ValueColumn<Item, string> model, string key) : ValueCellColumn<Item, string>(model, CellKind.Text)
    {
        internal readonly string Key = key;
        internal int Disposals;
        public override void Dispose() { ++Disposals; base.Dispose(); }
    }
    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        private PropertyChangedEventHandler? _handlers;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public string Name { get => _name; set { _name = value; _handlers?.Invoke(this, new(nameof(Name))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add => _handlers += value; remove => _handlers -= value; }
    }
}
