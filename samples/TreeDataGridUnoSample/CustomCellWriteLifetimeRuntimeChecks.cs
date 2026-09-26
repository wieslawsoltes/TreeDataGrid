using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Public native/published-consumer checks of the actual custom adapter write path.</summary>
internal static class CustomCellWriteLifetimeRuntimeChecks
{
    internal static async Task RunAsync(global::Uno.Controls.TreeDataGrid grid)
    {
        grid.Model = null;
        var previousOptions = grid.PresentationOptions;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 160).Select(i => new Item($"Row {i:000}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TextColumn<Item, string>("Custom", x => x.Name, width: new(240)) { PresentationKey = "WriteLifetime" });
        var custom = new Column();
        var options = new TreeDataGridPresentationOptions<Item>();
        options.Columns["WriteLifetime"] = _ => custom;
        grid.PresentationOptions = options;
        try
        {
            await Attach();
            var control = Current();
            var adapter = control.ViewModel!;
            var model = (Cell)control.Model!;
            var original = items[0];
            var replacement = new Item("Replacement");
            var beforeReuse = custom.Reuses;
            adapter.Write(new ConvertedText(() =>
            {
                items[0] = replacement;
                grid.UpdateLayout();
                Check(ReferenceEquals(control, Current()) && ReferenceEquals(adapter, Current().ViewModel) &&
                    ReferenceEquals(model, Current().Model) && custom.Reuses > beforeReuse,
                    "Replacement did not exercise retained native control/model/adapter reuse.");
                return "Obsolete conversion";
            }));
            Check(original.Name == "Row 000" && replacement.Name == "Replacement" && model.Writes == 0,
                "An old conversion wrote into either the retired or replacement row.");
            Check(original.Subscribers == 0 && replacement.Subscribers == 1 && model.Disposals == 0,
                "Retained reuse leaked a subscription or disposed the reused model.");
            Check(control.Value == "Replacement", "The retained native control did not render the replacement.");

            adapter.Write(new ConvertedText(() => { adapter.Write("Nested winner"); return "Obsolete outer"; }));
            Check(replacement.Name == "Nested winner" && model.Writes == 1 && control.Value == "Nested winner",
                "An older conversion overwrote a nested assignment or echoed its writeback.");

            var failure = new FormatException("Expected custom conversion failure.");
            Check(ReferenceEquals(Capture(() => adapter.Write(new ConvertedText(() => throw failure))), failure),
                "Custom conversion lost its original exception identity.");
            Check(model.Writes == 1 && replacement.Name == "Nested winner", "A failed conversion wrote a value.");
            Check(Capture(() => adapter.Write(new ConvertedText(() => { model.Writable = false; return "Denied"; })))
                is InvalidOperationException, "Conversion bypassed new read-only state.");
            Check(model.Writes == 1 && replacement.Name == "Nested winner", "Read-only conversion reached the setter.");
            model.Writable = true;

            Check(grid.BeginEdit(0, 0), "Custom writeback did not open the native text editor.");
            grid.EditingCell!.EditingText = "Native editor";
            Check(grid.CommitEdit(), "Native custom edit did not commit.");
            Check(ReferenceEquals(control, Current()) && replacement.Name == "Native editor" && model.Writes == 2,
                "Native writeback lost retained identity or wrote more than once.");

            var writes = model.Writes;
            model.BeforePermission = () => grid.Model = null;
            adapter.Write("Must not write after retirement");
            Check(grid.Model is null && model.Writes == writes && replacement.Name == "Native editor" && model.Disposals == 1,
                "Permission callback retirement allowed a stale setter or repeated disposal.");
            Check(items.All(item => item.Subscribers == 0), "Permission retirement retained row subscriptions.");
            Check(Capture(() => adapter.Write("Retired")) is ObjectDisposedException,
                "A retired public adapter did not reject a later write.");

            await Attach();
            control = Current();
            adapter = control.ViewModel!;
            model = (Cell)control.Model!;
            adapter.Write(new ConvertedText(() => { grid.Model = null; return "Disposed during conversion"; }));
            Check(grid.Model is null && model.Writes == 0 && model.Disposals == 1 && replacement.Name == "Native editor",
                "Conversion callback retirement wrote through a disposed model.");
            Check(items.All(item => item.Subscribers == 0), "Conversion retirement retained row subscriptions.");

            await Attach();
            Check(grid.BringCellIntoView(140, 0), "Distant custom row could not be brought into view.");
            await Settle();
            var distant = grid.TryGetCell(0, 140) as TreeDataGridTextCell ??
                throw new InvalidOperationException("No distant custom native control.");
            Check(distant.Value == items[140].Name && ReferenceEquals(distant.RowModel, items[140]),
                "Distant custom writeback cell lost Core row identity or text.");
            Check(grid.RowsPresenter!.RealizedRows.Count is > 0 and < 100, "Custom cells lost bounded realization.");
            grid.Model = null;
            Check(items.All(item => item.Subscribers == 0) && custom.Cells.All(cell => cell.Disposals == 1),
                "Final retirement leaked subscriptions or double-disposed a custom cell.");
            Check(source.Rows.Count == items.Count, "Adapter cleanup changed or disposed the borrowed Core source.");
        }
        finally
        {
            grid.Model = null;
            grid.PresentationOptions = previousOptions;
        }
        Console.WriteLine("UNO_RUNTIME_CUSTOM_WRITE_LIFETIME_PASSED: retained control/model/adapter reuse during conversion, nested write wins, conversion error identity, changed permissions, native edit, permission/conversion retirement, distant rendering and exact owned cleanup");

        TreeDataGridTextCell Current() => grid.TryGetCell(0, 0) as TreeDataGridTextCell ??
            throw new InvalidOperationException("No custom text control at row zero.");
        async Task Settle() { await Task.Delay(120); grid.UpdateLayout(); }
        async Task Attach()
        {
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            Check(ReferenceEquals(grid.Presentation!.Model, source), "The custom view duplicated its shared Core source.");
        }
    }

    private static Exception? Capture(Action action)
    {
        try { action(); return null; }
        catch (Exception error) { return error; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class ConvertedText(Func<string> convert) : IFormattable
    {
        public string ToString(string? format, IFormatProvider? provider) => convert();
    }
    private sealed class Column : NotifyingBase, ICellColumn<Item>
    {
        internal List<Cell> Cells { get; } = new();
        internal int Reuses;
        public double ActualWidth => 240;
        public bool? CanUserResize => true;
        public object? Header => "Custom";
        public Microsoft.UI.Xaml.GridLength Width { get; private set; } = new(240);
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public double CellMeasured(double width, int rowIndex) => width;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(Microsoft.UI.Xaml.GridLength width) => Width = width;
        public UI.ICell CreateCell(IRow<Item> row) { var cell = new Cell(row.Model); Cells.Add(cell); return cell; }
        public bool TryReuseCell(UI.ICell cell, IRow<Item> row)
        {
            if (cell is not Cell target) return false;
            ++Reuses;
            target.Retarget(row.Model);
            return true;
        }
    }
    private sealed class Cell : NotifyingBase, UI.ITextCell, IDisposable
    {
        private Item? _row;
        internal Action? BeforePermission;
        internal bool Writable = true;
        internal int Writes, Disposals;
        internal Cell(Item row) => Retarget(row);
        public bool CanEdit
        {
            get { var callback = BeforePermission; BeforePermission = null; callback?.Invoke(); return Writable; }
        }
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.Default;
        public object? Value => _row?.Name;
        public string? Text
        {
            get => _row?.Name;
            set { ++Writes; (_row ?? throw new InvalidOperationException("Disposed custom setter")).Name = value ?? string.Empty; }
        }
        public TextAlignment TextAlignment => TextAlignment.Left;
        public TextWrapping TextWrapping => TextWrapping.NoWrap;
        public TextTrimming TextTrimming => TextTrimming.CharacterEllipsis;
        internal void Retarget(Item row)
        {
            if (_row is not null) _row.PropertyChanged -= Changed;
            _row = row;
            row.PropertyChanged += Changed;
            RaisePropertyChanged(nameof(Value));
        }
        public void Dispose()
        {
            ++Disposals;
            if (_row is not null) _row.PropertyChanged -= Changed;
            _row = null;
        }
        private void Changed(object? sender, PropertyChangedEventArgs args) => RaisePropertyChanged(nameof(Value));
    }
    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs Changed = new(nameof(Name));
        private string _name = name;
        private PropertyChangedEventHandler? _changed;
        internal int Subscribers;
        public string Name { get => _name; set { _name = value; _changed?.Invoke(this, Changed); } }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
}
