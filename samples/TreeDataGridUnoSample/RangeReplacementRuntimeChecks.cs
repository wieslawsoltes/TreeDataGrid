using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using IndexPath = TreeDataGridCore.IndexPath;
using C = TreeDataGridCore.Models;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Loaded native acceptance for the public custom Core-row provider contract.</summary>
internal static class RangeReplacementRuntimeChecks
{
    public static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        var rows = new RangeRows(CreateItems("Original", 100));
        var replacement = new RangeRows(CreateItems("Replacement", 80));
        using var column = new U.TextColumn<Item, string>("Name", item => item.Name,
            (item, value) => item.Name = value ?? string.Empty, width: new(180));
        var columns = new U.ColumnList<Item> { column };
        var presenter = new ProbeRows
        {
            Items = rows, Columns = columns, ElementFactory = new TreeDataGridElementFactory(),
        };
        var scroll = new ScrollViewer
        {
            Width = 300, Height = 220, Content = presenter,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        try
        {
            page.Content = scroll;
            await Settle();
            Verify(rows);
            var retained = presenter.TryGetElement(3) ?? throw new InvalidOperationException("Missing initial retained row.");
            var retainedModel = (Item)retained.Model!;
            var retainedCell = retained.TryGetCell(0) ?? throw new InvalidOperationException("Missing initial retained cell.");
            retainedCell.IsTabStop = true;
            Check(retainedCell.Focus(FocusState.Keyboard), "The range test could not focus a loaded native cell.");
            Check(ContainsFocus(retained), "Native focus did not enter the retained row.");

            rows.ReplaceRange(1, 1, CreateItems("Small", 2));
            Check(rows.Count == 101 && retained.RowIndex == 4 &&
                ReferenceEquals(presenter.TryGetElement(4), retained) && ReferenceEquals(retained.Model, retainedModel),
                "Growing a range replaced a surviving container or applied an intermediate index.");
            Check(ContainsFocus(retained), "Growing a range discarded native focus.");
            await Settle();
            Verify(rows);

            // Do not allocate a source-sized sparse realized range. A displaced
            // focused survivor is kept by the presenter's separate focus lease.
            rows.ReplaceRange(1, 1, CreateItems("Large", 4000));
            Check(rows.Count == 4100 && retained.RowIndex == 4003 && ReferenceEquals(retained.Model, retainedModel),
                "A large replacement lost or misindexed the focused suffix.");
            Check(ContainsFocus(retained) && ReferenceEquals(retained.Parent, presenter),
                "A large replacement detached the focused survivor's native tree.");
            await Settle();
            Verify(rows);
            Check(presenter.Children.OfType<TreeDataGridRow>().Count() < 100,
                "A source-sized insertion allocated an unbounded native realization range.");

            rows.ReplaceRange(1, 4000, CreateItems("Restored", 1));
            Check(rows.Count == 101 && retained.RowIndex == 4 && ReferenceEquals(retained.Model, retainedModel),
                "Shrinking a range failed to remap a retained focus lease exactly once.");
            scroll.ChangeView(null, 0, null, true);
            presenter.BringIntoView(4);
            await Settle();
            Verify(rows);
            Check(ReferenceEquals(presenter.TryGetElement(4), retained),
                "Bringing the remapped focus lease back into view replaced its container.");
            var editingCell = retained.TryGetCell(0)!;
            Check(editingCell.BeginEdit(), "Editing did not recover after range growth/shrink.");
            editingCell.EditingText = "Edited retained range model";
            Check(editingCell.CommitEdit() && retainedModel.Name == "Edited retained range model",
                "A remapped cell wrote to an obsolete model or failed to commit.");

            rows.ReplaceRange(0, rows.Count, Array.Empty<Item>());
            await Settle();
            Check(presenter.RealizedRows.Count == 0 && presenter.RealizedCells.Count == 0 &&
                presenter.GetRowStart(0) == 0 && rows.AllItems.All(item => item.Subscribers == 0),
                "Replacing the complete range with an empty range retained geometry, focus, or bindings.");
            rows.ReplaceRange(0, 0, CreateItems("Repopulated", 100));
            scroll.ChangeView(null, 0, null, true);
            await Settle();
            Verify(rows);

            // Application index hooks can synchronously replace the provider and
            // enter native layout. The returning outer change must not continue
            // walking its retired suffix or erase newer row state.
            var called = false;
            presenter.AfterIndexUpdate = () =>
            {
                presenter.AfterIndexUpdate = null;
                called = true;
                presenter.Items = replacement;
                scroll.UpdateLayout();
            };
            rows.ReplaceRange(0, 1, CreateItems("Reentrant", 3));
            await Settle();
            Check(called, "The reentrant replacement did not exercise a native index callback.");
            Verify(replacement);
            Check(rows.Subscriptions == 0 && rows.AllItems.All(item => item.Subscribers == 0),
                "A retired range provider retained model or collection subscriptions.");
            var currentCell = presenter.RealizedRows.First().TryGetCell(0)!;
            var currentModel = (Item)presenter.RealizedRows.First().Model!;
            Check(currentCell.BeginEdit(), "The replacement provider's native editor did not recover.");
            currentCell.EditingText = "Edited replacement provider";
            Check(currentCell.CommitEdit() && currentModel.Name == "Edited replacement provider",
                "Reentrant provider replacement corrupted editor writeback.");
        }
        finally
        {
            presenter.AfterIndexUpdate = null;
            try { presenter.Items = null; }
            finally
            {
                try { presenter.Columns = null; scroll.Content = null; }
                finally { page.Content = previous; }
            }
        }
        Check(rows.Subscriptions == 0 && replacement.Subscriptions == 0 &&
            rows.AllItems.Concat(replacement.AllItems).All(item => item.Subscribers == 0),
            "Range replacement final retirement retained provider or model leases.");
        Console.WriteLine("UNO_RUNTIME_RANGE_REPLACEMENT_PASSED: variable-height ranges, direct final indices, bounded realization, focused suffix retention, growth/shrink, empty/repopulate, reentrant provider replacement, writeback and cleanup");

        async Task Settle() { await Task.Delay(100); scroll.UpdateLayout(); }
        void Verify(RangeRows expected)
        {
            Check(presenter.RealizedRows.Count is > 0 and < 60 &&
                double.IsFinite(presenter.GetRowStart(expected.Count)),
                "Range geometry has an invalid count/extent or did not virtualize.");
            foreach (var row in presenter.RealizedRows)
            {
                Check(row.RowIndex >= 0 && row.RowIndex < expected.Count &&
                    ReferenceEquals(row.Model, expected[row.RowIndex].Model), "A range row has the wrong current model.");
                Check(row.CellsPresenter?.RealizedCells.All(cell => ReferenceEquals(cell.RowModel, row.Model)) == true,
                    "A range cell retained a previous model.");
                Check(Math.Abs(Microsoft.UI.Xaml.Controls.Primitives.LayoutInformation.GetLayoutSlot(row).Y -
                    presenter.GetRowStart(row.RowIndex)) < 1, "Range arrangement ignored the remapped height geometry.");
            }
        }
    }

    private static IEnumerable<Item> CreateItems(string prefix, int count) =>
        Enumerable.Range(0, count).Select(index => new Item($"{prefix} {index}", 32 + index % 3 * 8));
    private static bool ContainsFocus(FrameworkElement owner)
    {
        var focused = owner.XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null;
        for (var current = focused; current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, owner)) return true;
        return false;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed partial class ProbeRows : TreeDataGridRowsPresenter
    {
        public Action? AfterIndexUpdate { get; set; }
        protected override void RealizeElement(Control element, C.IRow item, int index)
        {
            base.RealizeElement(element, item, index);
            element.Height = ((Item)((TreeDataGridRow)element).Model!).Height;
        }
        protected override void UpdateElementIndex(Control element, int oldIndex, int newIndex)
        {
            base.UpdateElementIndex(element, oldIndex, newIndex);
            AfterIndexUpdate?.Invoke();
        }
    }

    private sealed class RangeRows : U.ITreeDataGridRows
    {
        private readonly List<Row> _rows = new();
        private NotifyCollectionChangedEventHandler? _changed;
        public List<Item> AllItems { get; } = new();
        public int Subscriptions { get; private set; }
        public RangeRows(IEnumerable<Item> items)
        {
            foreach (var item in items)
            {
                var row = new Row(item);
                row.UpdateModelIndex(_rows.Count);
                _rows.Add(row);
                AllItems.Add(item);
            }
        }
        public int Count => _rows.Count;
        public C.IRow this[int index] => _rows[index];
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { _changed += value; ++Subscriptions; }
            remove { _changed -= value; --Subscriptions; }
        }
        public void ReplaceRange(int index, int oldCount, IEnumerable<Item> items)
        {
            var added = items.Select(item => new Row(item)).ToArray();
            var removed = _rows.GetRange(index, oldCount).ToArray();
            _rows.RemoveRange(index, oldCount);
            _rows.InsertRange(index, added);
            for (var i = index; i < _rows.Count; ++i) _rows[i].UpdateModelIndex(i);
            AllItems.AddRange(added.Select(row => row.Model));
            _changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,
                (IList)added, (IList)removed, index));
        }
        public int ModelIndexToRowIndex(IndexPath index) => index.Count == 1 && (uint)index[0] < (uint)Count ? index[0] : -1;
        public IndexPath RowIndexToModelIndex(int index) => (uint)index < (uint)Count ? new IndexPath(index) : default;
        public (int index, double y) GetRowAt(double y)
        {
            var start = 0d;
            for (var index = 0; index < Count; ++index)
            {
                if (y < start + _rows[index].Model.Height) return (index, start);
                start += _rows[index].Model.Height;
            }
            return (Count, start);
        }
        public U.ICell RealizeCell(U.IColumn column, int columnIndex, int rowIndex) =>
            ((ICellColumn<Item>)column).CreateCell(_rows[rowIndex]);
        public void UnrealizeCell(U.ICell cell, int columnIndex, int rowIndex) => (cell as IDisposable)?.Dispose();
        public IEnumerator<C.IRow> GetEnumerator() => ((IEnumerable<C.IRow>)_rows).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class Row(Item model) : C.IRow<Item>
    {
        public Item Model { get; } = model;
        object? C.IRow.Model => Model;
        public int ModelIndex { get; private set; }
        public void UpdateModelIndex(int index) => ModelIndex = index;
        public object? Header => Model.Name;
        public TreeDataGridCore.GridLength Height { get; set; } = TreeDataGridCore.GridLength.Auto;
    }
    private sealed class Item(string name, double height) : INotifyPropertyChanged
    {
        private string _name = name;
        private PropertyChangedEventHandler? _changed;
        public int Subscribers { get; private set; }
        public double Height { get; } = height;
        public string Name
        {
            get => _name;
            set { if (_name == value) return; _name = value; _changed?.Invoke(this, new(nameof(Name))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
}
