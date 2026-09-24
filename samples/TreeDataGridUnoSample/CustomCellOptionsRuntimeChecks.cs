using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using UI = Uno.Controls.Models.TreeDataGrid;
using NativeLength = Microsoft.UI.Xaml.GridLength;

namespace TreeDataGridUnoSample;

/// <summary>Live custom text metadata consumed by actual native and published browser controls.</summary>
internal static class CustomCellOptionsRuntimeChecks
{
    internal static async Task RunAsync(global::Uno.Controls.TreeDataGrid grid)
    {
        var previousOptions = grid.PresentationOptions;
        grid.Model = null;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 160).Select(i => new Item(12.5m + i)));
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TreeDataGridCore.Models.TextColumn<Item, decimal>("Amount", x => x.Amount,
            (x, value) => x.Amount = value, width: new(220)) { PresentationKey = "LiveCustomText" });
        var textOptions = new UI.TextColumnOptions<Item>
        {
            Culture = CultureInfo.InvariantCulture, StringFormat = "Amount {0:F2}",
            TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var presentationOptions = new TreeDataGridPresentationOptions<Item>();
        AmountColumn? column = null;
        presentationOptions.Columns.Add("LiveCustomText", _ => column = new AmountColumn(textOptions));
        try
        {
            grid.PresentationOptions = presentationOptions;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            var first = grid.TryGetCell(0, 0) ?? throw new InvalidOperationException("No custom amount control.");
            Check(ReferenceEquals(grid.Presentation!.Model, source), "Custom metadata introduced a different Core source.");
            VerifyVisibleRows();

            textOptions.Culture = CultureInfo.GetCultureInfo("fr-FR");
            textOptions.StringFormat = "Montant {0:F2}";
            textOptions.TextAlignment = TextAlignment.Right;
            textOptions.TextWrapping = TextWrapping.Wrap;
            textOptions.TextTrimming = TextTrimming.WordEllipsis;
            textOptions.BeginEditGestures = UI.BeginEditGestures.None;
            // The reference mutable options object has no change event. A normal
            // model publication must consume the changed style on the same cell.
            items[0].Amount = 13.75m;
            grid.UpdateLayout();
            Check(ReferenceEquals(first, grid.TryGetCell(0, 0)), "Changing custom text options replaced the native cell.");
            VerifyCell(0);
            Check(ShowcaseRuntimeChecks.Descendants(first).OfType<TextBlock>().Any(t => t.Text == "Montant 13,75"),
                "The existing custom control did not consume the new display culture/format.");

            textOptions.BeginEditGestures = UI.BeginEditGestures.Default;
            Check(grid.BeginEdit(0, 0), "The custom amount cell could not enter the native editor.");
            grid.EditingCell!.EditingText = "21,5";
            Check(grid.CommitEdit() && items[0].Amount == 21.5m,
                "The native custom editor did not use the live French conversion culture.");
            VerifyCell(0);
            Check(ReferenceEquals(first, grid.TryGetCell(0, 0)), "Native writeback replaced the existing custom control.");
            // Refresh every currently realized row through the ordinary model
            // notification, without inventing an options-change notification.
            foreach (var item in items) item.Notify();
            grid.UpdateLayout();
            VerifyVisibleRows();

            Check(grid.BringCellIntoView(140, 0), "Distant custom styled row could not be brought into view.");
            await Settle();
            Check(grid.TryGetCell(0, 140) is not null, "The requested distant styled control was not realized.");
            VerifyVisibleRows();
            Check(grid.RowsPresenter!.RealizedRows.Count is > 0 and < 100, "Custom styled rows lost bounded realization.");
            Check(grid.BringCellIntoView(0, 0), "Returning to the first custom row failed.");
            await Settle();
            VerifyCell(0);
            grid.Model = null;
            Check(column?.Disposed == true, "The custom view column was not retired.");
            Check(items.All(item => item.Subscribers == 0), "Styled custom cells retained subscriptions after source removal.");
            Check(source.Rows.Count == items.Count, "View retirement changed or disposed the borrowed source.");
        }
        finally
        {
            grid.Model = null;
            grid.PresentationOptions = previousOptions;
        }
        Console.WriteLine("UNO_RUNTIME_CUSTOM_CELL_OPTIONS_PASSED: live styles/culture/format, same-control refresh, French native edit, distant/return rendering, bounded realization, original Core identity and subscription cleanup");

        async Task Settle() { await Task.Delay(120); grid.UpdateLayout(); }
        void VerifyVisibleRows()
        {
            var checkedRows = 0;
            foreach (var cell in grid.RowsPresenter!.RealizedCells.Where(x => x.ColumnIndex == 0))
            {
                VerifyCell(cell.RowIndex);
                ++checkedRows;
            }
            Check(checkedRows > 0, "No custom styled rows were verified.");
        }
        void VerifyCell(int rowIndex)
        {
            var cell = grid.TryGetCell(0, rowIndex) as global::Uno.Controls.Primitives.TreeDataGridCell ??
                throw new InvalidOperationException("No expected custom styled cell at " + rowIndex);
            var model = (Item)source.Rows[rowIndex].Model!;
            var expected = string.Format(textOptions.Culture, textOptions.StringFormat!, model.Amount);
            Check(ReferenceEquals(cell.RowModel, model), "Custom style refresh changed row model identity.");
            var text = ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().FirstOrDefault(t => t.Text == expected);
            Check(text is not null, $"Stale custom display at row {rowIndex}; expected '{expected}'.");
            Check(text!.TextAlignment == textOptions.TextAlignment && text.TextWrapping == textOptions.TextWrapping &&
                text.TextTrimming == textOptions.TextTrimming, $"Stale custom style at row {rowIndex}.");
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class AmountColumn(UI.TextColumnOptions<Item> options)
        : UI.ColumnBase<Item, decimal>("Amount", x => x.Amount, (x, value) => x.Amount = value,
            new NativeLength(220), options), IDisposable
    {
        internal bool Disposed { get; private set; }
        public override UI.ICell CreateCell(IRow<Item> row)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            return new UI.TextCell<decimal>(CreateBindingExpression(row.Model), false, options);
        }
        public void Dispose() => Disposed = true;
    }
    private sealed class Item(decimal amount) : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs Changed = new(nameof(Amount));
        private decimal _amount = amount;
        private PropertyChangedEventHandler? _changed;
        public decimal Amount { get => _amount; set { _amount = value; Notify(); } }
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        internal void Notify() => _changed?.Invoke(this, Changed);
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
