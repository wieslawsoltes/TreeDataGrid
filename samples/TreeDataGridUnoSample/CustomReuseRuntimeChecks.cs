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

/// <summary>Deferred native retained-model/reuse and subscription-lifetime checks.</summary>
internal static class CustomReuseRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        grid.Model = null;
        var previousOptions = grid.PresentationOptions;
        var previousFactory = grid.ElementFactory;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 200).Select(i => new Item($"Row {i:000}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TextColumn<Item, string>("Custom", x => x.Name, width: new(240)) { PresentationKey = "Reuse" });
        var column = new Column();
        var options = new TreeDataGridPresentationOptions<Item>();
        options.Columns["Reuse"] = _ => column;
        grid.PresentationOptions = options;
        try
        {
            grid.ElementFactory = new Factory();
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(150);
            var control = (CustomTextCell)grid.TryGetCell(0, 0)!;
            var model = (Cell)control.Model!;
            var notifications = control.Notifications;
            items[0].Name = "Original model callback";
            Check(control.Notifications == notifications + 1 && ReferenceEquals(control.LastSender, model) &&
                control.Value == "Original model callback",
                "The custom model notification hook lost original identity or subscribed more than once.");
            var parent = control.Parent;
            var original = items[0];
            var replacement = new Item("Retargeted", TextAlignment.Right);
            items[0] = replacement;
            grid.UpdateLayout();
            Check(ReferenceEquals(control, grid.TryGetCell(0, 0)) && ReferenceEquals(model, control.Model),
                "Custom TryReuseCell did not retain both the native control and original model.");
            Check(ReferenceEquals(control.Parent, parent) && control.Value == "Retargeted" && control.TextAlignment == TextAlignment.Right,
                "Retargeting lost parent identity, value or refreshed text options.");
            Check(original.Subscribers == 0 && replacement.Subscribers == 1 && model.Disposals == 0,
                "Retargeting retained the previous row subscription or disposed the reused model.");
            original.Name = "Obsolete notification";
            Check(control.Value == "Retargeted", "An old row updated its retargeted cell.");
            notifications = control.Notifications;
            replacement.Name = "Current notification";
            Check(control.Notifications == notifications + 1 && ReferenceEquals(control.LastSender, model) &&
                control.Value == "Current notification", "Rebinding lost or duplicated the custom control subscription.");

            column.RejectReuse = true;
            items[0] = new("Rejected reuse");
            grid.UpdateLayout();
            Check(ReferenceEquals(control, grid.TryGetCell(0, 0)) && !ReferenceEquals(model, control.Model) && model.Disposals == 1,
                "Rejected model reuse did not replace/dispose the model while retaining the compatible control.");
            column.RejectReuse = false;

            grid.Scroll!.ChangeView(null, 1600, null, true);
            await Task.Delay(100);
            grid.UpdateLayout();
            var visible = grid.RowsPresenter!.RealizedRows.Select(row => row.Model).ToHashSet(ReferenceEqualityComparer.Instance);
            Check(items.Where(item => !visible.Contains(item)).All(item => item.Subscribers == 0),
                "End-of-pass finalization retained subscriptions for non-realized custom rows.");
            grid.Model = null;
            Check(items.All(item => item.Subscribers == 0) && column.Cells.All(cell => cell.Disposals == 1),
                "Source removal leaked or double-disposed a retained custom cell.");

            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(100);
            column.OnReuse = () => grid.Model = null;
            items[0] = new("Removed inside reuse");
            grid.UpdateLayout();
            Check(grid.Model is null && grid.RowsPresenter!.RealizedCells.Count == 0 && items.All(item => item.Subscribers == 0),
                "A source change inside TryReuseCell retained a stale model/control.");
            column.OnReuse = null;

            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(100);
            column.ThrowOnReuse = true;
            var threw = false;
            try { items[0] = new("Throwing reuse"); grid.UpdateLayout(); }
            catch (InvalidOperationException) { threw = true; }
            finally { grid.Model = null; }
            Check(threw && items.All(item => item.Subscribers == 0) && column.Cells.All(cell => cell.Disposals == 1),
                "A throwing reuse callback leaked or double-disposed its in-flight model.");
            Console.WriteLine("UNO_RUNTIME_CUSTOM_REUSE_PASSED: original model/Core row retarget, metadata, old subscriptions, rejected/throwing reuse, scrolling finalization, source removal/reentrancy");
        }
        finally { grid.Model = null; grid.PresentationOptions = previousOptions; grid.ElementFactory = previousFactory; }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Factory : TreeDataGridElementFactory
    {
        protected override Microsoft.UI.Xaml.Controls.Control CreateElement(object? data) =>
            data is UI.ITextCell ? new CustomTextCell() : base.CreateElement(data);
        protected override string GetDataRecycleKey(object? data) =>
            data is UI.ITextCell ? nameof(CustomTextCell) : base.GetDataRecycleKey(data);
        protected override string GetElementRecycleKey(Microsoft.UI.Xaml.Controls.Control element) =>
            element is CustomTextCell ? nameof(CustomTextCell) : base.GetElementRecycleKey(element);
    }
    private sealed partial class CustomTextCell : TreeDataGridTextCell
    {
        public int Notifications { get; private set; }
        public object? LastSender { get; private set; }
        public override void Realize(CellColumn column, CellValue value, IRow row, int columnIndex, int rowIndex,
            DataTemplate? template, DataTemplate? editingTemplate = null)
        {
            base.Realize(column, value, row, columnIndex, rowIndex, template, editingTemplate);
            SubscribeToModelChanges();
            SubscribeToModelChanges();
        }
        protected override void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ++Notifications;
            LastSender = sender;
            Check(ReferenceEquals(sender, Model), "A custom cell received its adapter instead of the original model.");
            base.OnModelPropertyChanged(sender, e);
        }
    }
    private sealed class Column : NotifyingBase, ICellColumn<Item>
    {
        public List<Cell> Cells { get; } = new();
        public bool RejectReuse;
        public bool ThrowOnReuse;
        public Action? OnReuse;
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
            if (ThrowOnReuse) throw new InvalidOperationException("Reuse test failure.");
            if (RejectReuse) return false;
            ((Cell)cell).Retarget(row.Model);
            OnReuse?.Invoke();
            return true;
        }
    }
    private sealed class Cell : NotifyingBase, UI.ITextCell, IDisposable
    {
        private Item? _row;
        public Cell(Item row) => Retarget(row);
        public int Disposals { get; private set; }
        public bool CanEdit => false;
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.None;
        public object? Value => _row?.Name;
        public string? Text { get => _row?.Name; set => throw new NotSupportedException(); }
        public TextAlignment TextAlignment => _row?.Alignment ?? TextAlignment.Left;
        public TextWrapping TextWrapping => TextWrapping.NoWrap;
        public TextTrimming TextTrimming => TextTrimming.CharacterEllipsis;
        public void Retarget(Item row)
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
    private sealed class Item(string name, TextAlignment alignment = TextAlignment.Left) : INotifyPropertyChanged
    {
        private string _name = name;
        private PropertyChangedEventHandler? _changed;
        public string Name { get => _name; set { if (_name != value) { _name = value; _changed?.Invoke(this, new(nameof(Name))); } } }
        public TextAlignment Alignment => alignment;
        public int Subscribers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
}
