using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Public value-column derivation consumed by native and trimmed browser controls.</summary>
internal static class ValueColumnBaseRuntimeChecks
{
    internal static async Task RunAsync(global::Uno.Controls.TreeDataGrid grid)
    {
        var previousOptions = grid.PresentationOptions;
        grid.Model = null;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 200).Select(i => new Item($"Value row {i:D3}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        var definition = new TreeDataGridCore.Models.TextColumn<Item, string>("Name", x => x.Detail.Name,
            (x, value) => x.Detail.Name = value, width: new(230)) { PresentationKey = "ValueBase.Text" };
        var checkDefinition = new TreeDataGridCore.Models.CheckBoxColumn<Item>("Enabled", x => x.Detail.Enabled,
            (x, value) => x.Detail.Enabled = value, width: new(120)) { PresentationKey = "ValueBase.Check" };
        source.Columns.Add(definition);
        source.Columns.Add(checkDefinition);
        var textOptions = new UI.TextColumnOptions<Item> { MinWidth = new(80) };
        var views = new List<IDisposable>();
        TextValueColumn? textView = null;
        var presentationOptions = new TreeDataGridPresentationOptions<Item>();
        presentationOptions.Columns.Add("ValueBase.Text", _ =>
        {
            var result = new TextValueColumn(textOptions);
            views.Add(result);
            textView = result;
            return result;
        });
        presentationOptions.Columns.Add("ValueBase.Check", _ =>
        {
            var result = new CheckValueColumn();
            views.Add(result);
            return result;
        });
        var replaced = items[0];
        var originalDetail = replaced.Detail;
        try
        {
            grid.PresentationOptions = presentationOptions;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            Check(ReferenceEquals(grid.Presentation!.Model, source), "A custom value column copied the shared Core source.");
            Check(textView is not null && ReferenceEquals(textView.Options, textOptions), "Column options lost caller identity.");
            VerifyVisibleRows();
            var first = grid.TryGetCell(0, 0) ?? throw new InvalidOperationException("No first value-column control was realized.");
            Check(grid.BeginEdit(0, 0), "The derived value-column TextCell did not enter native editing.");
            grid.EditingCell!.EditingText = "Edited through typed value base";
            Check(grid.CommitEdit() && items[0].Detail.Name == "Edited through typed value base",
                "The protected binding factory did not write through the native editor to the original model.");
            VerifyVisibleRows();
            items[0].Detail = new Detail("Replacement child");
            grid.UpdateLayout();
            Check(originalDetail.Subscribers == 0, "Replacing a nested owner left an old typed binding attached.");
            Check(ReferenceEquals(first, grid.TryGetCell(0, 0)), "A nested owner change replaced the native cell control.");
            items[0].Detail.Name = "Live nested update";
            grid.UpdateLayout();
            VerifyVisibleRows();
            var checkbox = grid.TryGetCell(1, 0) as TreeDataGridCheckBoxCell ??
                throw new InvalidOperationException("A typed checkbox model did not create the native checkbox control.");
            Check(checkbox.IsThreeState && !checkbox.IsReadOnly, "Typed checkbox metadata was lost by the native adapter.");
            checkbox.Value = true;
            Check(items[0].Detail.Enabled == true, "Native checkbox true did not reach the nested model.");
            checkbox.Value = null;
            Check(items[0].Detail.Enabled is null, "Native checkbox null did not reach the nested model.");
            items[0].Detail.Enabled = false;
            Check(checkbox.Value == false, "An external typed checkbox update was not rendered.");
            items[0] = new Item("Replaced whole row");
            await Settle();
            Check(replaced.Subscribers == 0 && replaced.Detail.Subscribers == 0,
                "Replacing a source row retained the original typed-binding owners.");
            VerifyVisibleRows();
            textView!.Header = "Changed value header";
            grid.Presentation.Columns.SetColumnWidth(0, new Microsoft.UI.Xaml.GridLength(270));
            await Settle();
            Check(Equals(grid.ColumnHeadersPresenter!.TryGetElement(0)?.Content, textView.Header),
                "The compatibility base's header did not reach its native header control.");
            Check(definition.Width.Value == 270 && Math.Abs(grid.Presentation.Columns[0].ActualWidth - textView.ActualWidth) < .01,
                "Native resizing diverged from the shared Core definition or the custom layout base.");
            Check(textView.GetComparison(ListSortDirection.Ascending)!(items[1], items[2]) < 0,
                "The custom value selector did not supply the expected comparison.");
            Check(grid.BringCellIntoView(170, 0), "Distant navigation failed for the derived value column.");
            await Settle();
            Check(grid.TryGetCell(0, 170) is not null, "Distant custom binding content was not realized.");
            VerifyVisibleRows();
            source.SortBy(definition, ListSortDirection.Descending);
            grid.Scroll.ChangeView(0, 0, null, true);
            await Settle();
            VerifyVisibleRows();
            Check(grid.RowsPresenter!.RealizedRows.Count is > 0 and < 128,
                "The custom value-column path lost bounded row realization.");
            grid.Model = null;
            Check(views.All(view => view switch { TextValueColumn text => text.Disposed, CheckValueColumn check => check.Disposed, _ => false }),
                "Retiring the grid did not release its custom view columns.");
            Check(items.All(item => item.Subscribers == 0 && item.Detail.Subscribers == 0) &&
                replaced.Subscribers == 0 && replaced.Detail.Subscribers == 0 && originalDetail.Subscribers == 0,
                "Retiring the native typed-cell consumers leaked source or child subscriptions.");
            Check(source.Rows.Count == items.Count, "View retirement disposed or changed the borrowed Core source.");
        }
        finally
        {
            grid.Model = null;
            grid.PresentationOptions = previousOptions;
        }
        Console.WriteLine("UNO_RUNTIME_VALUE_COLUMN_BASE_PASSED: public derivation, real Core rows, native text edit, nested owner replacement, typed three-state checkbox, source-row replacement, header/width propagation, distant/sorted rendering, bounded realization and subscription cleanup");

        async Task Settle() { await Task.Delay(120); grid.UpdateLayout(); }
        void VerifyVisibleRows()
        {
            var count = 0;
            foreach (var cell in grid.RowsPresenter!.RealizedCells.Where(cell => cell.ColumnIndex == 0))
            {
                var model = (Item)source.Rows[cell.RowIndex].Model!;
                Check(ReferenceEquals(cell.RowModel, model), "A native custom value cell lost Core model identity.");
                Check(ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().Any(text => text.Text == model.Detail.Name),
                    $"Custom typed display is stale at row {cell.RowIndex}.");
                ++count;
            }
            Check(count > 0, "No custom value cells were inspected.");
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class TextValueColumn(UI.TextColumnOptions<Item> options)
        : UI.ColumnBase<Item, string>("Name", x => x.Detail.Name, (x, value) => x.Detail.Name = value ?? string.Empty,
            new Microsoft.UI.Xaml.GridLength(230), options), IDisposable
    {
        public bool Disposed { get; private set; }
        public override UI.ICell CreateCell(IRow<Item> row)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            // The same public pattern used by a reference custom value column.
            return new UI.TextCell<string?>(CreateBindingExpression(row.Model), false, (UI.TextColumnOptions<Item>)Options);
        }
        public void Dispose() => Disposed = true;
    }
    private sealed class CheckValueColumn()
        : UI.ColumnBase<Item, bool?>("Enabled", x => x.Detail.Enabled, (x, value) => x.Detail.Enabled = value,
            new Microsoft.UI.Xaml.GridLength(120), new()), IDisposable
    {
        public bool Disposed { get; private set; }
        public override UI.ICell CreateCell(IRow<Item> row)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            return new UI.CheckBoxCell(CreateBindingExpression(row.Model), false, true);
        }
        public void Dispose() => Disposed = true;
    }
    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private Detail _detail = new(name);
        private PropertyChangedEventHandler? _changed;
        public Detail Detail { get => _detail; set { _detail = value; _changed?.Invoke(this, new(nameof(Detail))); } }
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
    private sealed class Detail(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        private bool? _enabled = false;
        private PropertyChangedEventHandler? _changed;
        public string Name { get => _name; set { _name = value; _changed?.Invoke(this, new(nameof(Name))); } }
        public bool? Enabled { get => _enabled; set { _enabled = value; _changed?.Invoke(this, new(nameof(Enabled))); } }
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
