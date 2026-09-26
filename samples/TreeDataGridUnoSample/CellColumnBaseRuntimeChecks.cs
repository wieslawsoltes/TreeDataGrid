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
using Uno.Controls;
using Uno.Controls.Presentation;
using UI = Uno.Controls.Models.TreeDataGrid;
using NativeLength = Microsoft.UI.Xaml.GridLength;

namespace TreeDataGridUnoSample;

internal static class CellColumnBaseRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        var previousOptions = grid.PresentationOptions;
        grid.Model = null;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 200).Select(index => new Item($"Custom row {index:D3}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        var auto = new TreeDataGridCore.Models.TextColumn<Item, string>("Auto", item => item.Name) { PresentationKey = "Custom" };
        source.Columns.Add(auto);
        var views = new List<ProbeColumn>();
        var options = new TreeDataGridPresentationOptions<Item>();
        options.Columns.Add("Custom", _ =>
        {
            var view = new ProbeColumn(new CellColumnOptions { MinWidth = NativeLength.Auto, MaxWidth = NativeLength.Auto });
            views.Add(view);
            return view;
        });
        try
        {
            grid.PresentationOptions = options;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(150);
            grid.UpdateLayout();
            var view = views.Single();
            Check(ReferenceEquals(grid.Presentation!.Model, source), "The custom base copied its Core source.");
            Check(double.IsFinite(view.ActualWidth) && view.ActualWidth > 1, "Auto constraints never completed natural width discovery.");
            Check(Math.Abs(view.ActualWidth - grid.Presentation.Columns[0].ActualWidth) < .01, "Public custom width differs from the native adapter.");
            VerifyRows();

            var first = grid.TryGetCell(0, 0) ?? throw new InvalidOperationException("No custom first cell.");
            Check(grid.BeginEdit(0, 0), "A custom base column could not start editing.");
            grid.EditingCell!.EditingText = "Edited through custom base";
            Check(grid.CommitEdit() && items[0].Name == "Edited through custom base", "Custom cell writeback did not reach the original model.");
            items[0].Name = "Externally updated custom row";
            grid.UpdateLayout();
            VerifyRows();

            ((UI.IUpdateColumnLayout)view).SetWidth(new NativeLength(250));
            grid.Presentation.Columns.SetColumnWidth(0, new NativeLength(250));
            grid.UpdateLayout();
            Check(auto.Width.Value == 250, "Native resizing did not update the Core definition.");
            Check(double.IsFinite(view.ActualWidth), "Custom fixed width became invalid.");
            view.Header = "Updated custom header";
            grid.UpdateLayout();
            Check(Equals(grid.ColumnHeadersPresenter!.TryGetElement(0)?.Content, view.Header), "Custom header changes did not propagate.");

            Check(grid.BringCellIntoView(170, 0), "Custom-column distant navigation failed.");
            await Task.Delay(100);
            grid.UpdateLayout();
            Check(grid.TryGetCell(0, 170) is not null, "The requested custom row was not realized.");
            VerifyRows();
            source.SortBy(auto, ListSortDirection.Descending);
            grid.Scroll.ChangeView(0, 0, null, true);
            await Task.Delay(100);
            grid.UpdateLayout();
            VerifyRows();
            Check(view.LiveCells < 64 && view.LiveCells > 0, "The custom column lost bounded realization.");
            Check(view.ReuseCount > 0, "The custom base's public reuse contract was not called.");
            grid.Model = null;
            Check(view.LiveCells == 0 && view.Disposed, "Retiring the custom base leaked cell models or its view.");
            Check(items.All(item => item.ObserverCount == 0), "Retired custom cells retained model subscriptions.");
        }
        finally
        {
            grid.Model = null;
            grid.PresentationOptions = previousOptions;
        }
        Console.WriteLine("UNO_RUNTIME_CUSTOM_COLUMN_BASE_PASSED: Auto constraint discovery, native/public width agreement, shared Core identity, editing, notifications, header changes, distant navigation, sort, public reuse and exact lifetime cleanup");

        void VerifyRows()
        {
            foreach (var cell in grid.RowsPresenter!.RealizedCells)
            {
                var model = (Item)source.Rows[cell.RowIndex].Model!;
                Check(ReferenceEquals(cell.RowModel, model), "A custom column retained a different Core model.");
                Check(ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().Any(text => text.Text == model.Name),
                    "A custom cell retained stale display text.");
            }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        private PropertyChangedEventHandler? _changed;
        public string Name { get => _name; set { if (_name == value) return; _name = value; _changed?.Invoke(this, new(nameof(Name))); } }
        public int ObserverCount => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }

    private sealed class ProbeColumn(CellColumnOptions options) : CellColumnBase<Item>("Auto", null, options), ICellColumn<Item>, IDisposable
    {
        public int LiveCells { get; private set; }
        public int ReuseCount { get; private set; }
        public bool Disposed { get; private set; }
        public override UI.ICell CreateCell(IRow<Item> row)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            ++LiveCells;
            // Capture the model, never Core's mutable anonymous row object.
            return new BoundCell(this, row.Model);
        }
        public bool TryReuseCell(UI.ICell cell, IRow<Item> row)
        {
            if (cell is not BoundCell bound || !ReferenceEquals(bound.Owner, this) || Disposed) return false;
            bound.Retarget(row.Model);
            ++ReuseCount;
            return true;
        }
        public void Dispose() => Disposed = true;

        private sealed class BoundCell : NotifyingBase, UI.ITextCell, IDisposable
        {
            private Item? _model;
            public BoundCell(ProbeColumn owner, Item model) { Owner = owner; Retarget(model); }
            public ProbeColumn Owner { get; }
            public object? Value => _model?.Name;
            public bool CanEdit => _model is not null;
            public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.Default;
            public string? Text { get => _model?.Name; set { if (_model is not null) _model.Name = value ?? string.Empty; } }
            public TextTrimming TextTrimming => TextTrimming.CharacterEllipsis;
            public TextWrapping TextWrapping => TextWrapping.NoWrap;
            public TextAlignment TextAlignment => TextAlignment.Left;
            public void Retarget(Item model)
            {
                if (_model is not null) _model.PropertyChanged -= Changed;
                _model = model;
                _model.PropertyChanged += Changed;
            }
            private void Changed(object? sender, PropertyChangedEventArgs args) => RaisePropertyChanged(nameof(Value));
            public void Dispose()
            {
                if (_model is null) return;
                _model.PropertyChanged -= Changed;
                _model = null;
                --Owner.LiveCells;
            }
        }
    }
}
