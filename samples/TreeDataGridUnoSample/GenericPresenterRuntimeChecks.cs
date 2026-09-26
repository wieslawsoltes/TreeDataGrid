using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using Windows.Foundation;
using UIColumn = Uno.Controls.Models.TreeDataGrid.IColumn;

namespace TreeDataGridUnoSample;

/// <summary>Deferred native checks of reference presenter subclass contracts.</summary>
internal static class GenericPresenterRuntimeChecks
{
    public static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 200).Select(i => new Item($"Item {i}", 24 + i % 3 * 8)));
        var factory = new Factory();
        var presenter = new ItemPresenter { Items = items, ElementFactory = factory };
        var scroll = new ScrollViewer
        {
            Width = 260, Height = 180, Content = presenter,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        using var source = new FlatTreeDataGridSource<Item>(items);
        for (var i = 0; i < 20; ++i)
            source.Columns.Add(new TreeDataGridCore.Models.TextColumn<Item, string>($"Column {i}", x => x.Name, width: new(100)));
        using var view = TreeDataGridPresentation.Create(source);
        var columns = new ColumnPresenter { Items = view.Columns, ElementFactory = factory };
        var headers = new HeaderPresenter { Items = view.Columns, ElementFactory = new TreeDataGridElementFactory() };
        var cells = new CellsPresenter { Items = view.Columns, Rows = view.Rows, ElementFactory = new TreeDataGridElementFactory() };
        var rows = new RowsPresenter { Items = view.Rows, Columns = view.Columns, ElementFactory = new TreeDataGridElementFactory() };
        try
        {
            page.Content = scroll;
            await Settle();
            Check(presenter.GetRealizedElements().Count() is > 0 and < 200 && presenter.Realizations > 0 &&
                presenter.MeasureCalls > 0 && presenter.ArrangeCalls > 0,
                "The generic presenter did not virtualize or dispatch reference realization/layout overrides.");
            CheckModels();
            var first = presenter.TryGetElement(0)!;
            var firstModel = first.DataContext;
            items.Insert(0, new Item("Inserted", 40));
            Check(ReferenceEquals(presenter.TryGetElement(1), first) && ReferenceEquals(first.DataContext, firstModel) && presenter.IndexUpdates > 0,
                "Collection insertion failed to preserve and reindex an existing control.");
            items.Move(1, 2);
            Check(ReferenceEquals(presenter.TryGetElement(2), first), "Collection movement replaced an existing item container.");
            items.RemoveAt(0);
            await Settle();
            CheckModels();

            var initial = presenter.GetRealizedElements().ToArray();
            var unloads = 0;
            foreach (var element in initial) element.Unloaded += (_, _) => ++unloads;
            scroll.ChangeView(null, 1_200, null, true);
            await Settle();
            Check(presenter.GetRealizedElements().Any(element => (int)element.Tag! >= 20) &&
                presenter.GetRealizedElements().Count() < 30, "The generic presenter ignored the effective vertical viewport.");
            Check(unloads == 0 && initial.All(element => ReferenceEquals(element.Parent, presenter)),
                "Ordinary generic-presenter recycling detached retained native controls.");
            CheckModels();

            var brought = presenter.BringIntoView(160);
            await Settle();
            Check(brought is not null && ReferenceEquals(presenter.TryGetElement(160)?.DataContext, items[160]),
                "The generic bring-into-view contract failed for an unrealized variable-height item.");
            CheckModels();

            scroll.Content = null;
            await Settle();
            Check(!presenter.GetRealizedElements().Any() && presenter.Children.OfType<Control>().All(element => element.DataContext is null),
                "Unloading a generic presenter retained item realizations/models.");
            items.Add(new Item("Added while unloaded", 28));
            scroll.Content = presenter;
            await Settle();
            CheckModels();

            scroll.Content = columns;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.ChangeView(0, 0, null, true);
            await Settle();
            Check(columns.GetRealizedElements().Count() is > 0 and < 20 && columns.ColumnMeasures > 0,
                "The columnar base did not route measurement through public IColumns.");
            view.Columns.SetColumnWidth(0, new Microsoft.UI.Xaml.GridLength(150));
            await Settle();
            Check(Math.Abs(columns.TryGetElement(0)!.ActualWidth - 150) < 1,
                "A public column width change did not invalidate/arrange the derived columnar presenter.");
            scroll.ChangeView(1_200, null, null, true);
            await Settle();
            Check(columns.GetRealizedElements().Any(element => (int)element.Tag! >= 10) && columns.GetRealizedElements().Count() < 10,
                "The columnar base did not follow the horizontal viewport.");

            scroll.Content = headers;
            scroll.ChangeView(0, null, null, true);
            await Settle();
            Check(headers.RealizedCount is > 0 and < 20 && headers.Realizations > 0 && headers.Measurements > 0,
                "The built-in header presenter bypassed the shared base's subclass hooks.");
            var header = headers.TryGetElement(0)!;
            var headerColumn = (CellColumn)view.Columns[0];
            headerColumn.Header = "Observed header";
            Check(Equals(header.Header, "Observed header"), "A standalone header did not observe its public column model.");
            headerColumn.SortDirection = System.ComponentModel.ListSortDirection.Ascending;
            Check(header.SortDirection == System.ComponentModel.ListSortDirection.Ascending,
                "A standalone header did not observe its sort direction.");
            headers.Items = null;
            Check(header.ColumnIndex == -1 && header.Header is null && headers.RealizedCount == 0,
                "Retiring public header items retained header content or realization state.");
            headerColumn.Header = "After release";
            Check(header.Header is null, "An unrealized header retained its column observation.");

            headers.AfterRealize = () => { headers.AfterRealize = null; headers.Items = null; };
            headers.Items = view.Columns;
            headers.Measure(new Size(259, 40));
            Check(headers.RealizedCount == 0 && !headers.GetRealizedElements().Any() &&
                headers.Children.OfType<TreeDataGridColumnHeader>().All(value => value.ColumnIndex == -1 && value.Header is null),
                "Source replacement inside header realization published a stale container or retained its model.");

            cells.Realize(0);
            scroll.Content = cells;
            scroll.ChangeView(0, null, null, true);
            await Settle();
            Check(cells.RealizedCells.Count is > 0 and < 20 && cells.Realizations > 0 && cells.Measurements > 0 &&
                cells.RealizedCells.All(cell => ReferenceEquals(cell.RowModel, items[0])),
                "The built-in cells presenter bypassed the shared lifecycle/layout hooks or original Core models.");
            var retainedCells = cells.RealizedCells.ToArray();
            var cellUnloads = 0;
            foreach (var cell in retainedCells) cell.Unloaded += (_, _) => ++cellUnloads;
            cells.Unrealize();
            cells.Realize(1);
            await Settle();
            Check(cellUnloads == 0 && retainedCells.All(cell => ReferenceEquals(cell.Parent, cells)) &&
                cells.RealizedCells.All(cell => ReferenceEquals(cell.RowModel, items[1])),
                "Shared cell layout detached recycled controls or retained the previous Core row model.");
            cells.Unrealize();
            cells.AfterRealize = () => { cells.AfterRealize = null; cells.Rows = null; };
            cells.Realize(0);
            cells.Measure(new Size(258, 40));
            Check(cells.RealizedCells.Count == 0 && cells.Children.OfType<TreeDataGridCell>().All(cell => cell.Model is null),
                "Replacing Rows inside the compatible presenter hook retained an in-flight cell model.");

            scroll.Content = rows;
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.ChangeView(0, 0, null, true);
            await Settle();
            Check(rows.RealizedRows.Count is > 0 and < 30 && rows.Realizations > 0 && rows.Measurements > 0 && rows.Arrangements > 0,
                "The built-in rows presenter bypassed the shared lifecycle/layout hooks.");
            CheckRows();
            var retainedRow = rows.TryGetElement(0)!;
            var retainedModel = retainedRow.Model;
            items.Insert(0, new Item("Row insert", 36));
            Check(ReferenceEquals(rows.TryGetElement(1), retainedRow) && ReferenceEquals(retainedRow.Model, retainedModel),
                "The built-in rows presenter processed insertion more than once or replaced a retained container.");
            items.Move(1, 2);
            Check(ReferenceEquals(rows.TryGetElement(2), retainedRow), "Moving a row did not preserve its compatible container.");
            items.RemoveAt(0);
            await Settle();
            CheckRows();
            var retainedRows = rows.RealizedRows.ToArray();
            var rowUnloads = 0;
            foreach (var row in retainedRows) row.Unloaded += (_, _) => ++rowUnloads;
            scroll.ChangeView(null, 1_200, null, true);
            await Settle();
            Check(rows.RealizedRows.Any(row => row.RowIndex >= 20) && rowUnloads == 0 &&
                retainedRows.All(row => ReferenceEquals(row.Parent, rows)),
                "Ordinary built-in row recycling detached controls or ignored the vertical viewport.");
            CheckRows();
            Check(rows.Children.OfType<TreeDataGridRow>().Count(row => row.RowIndex < 0) <= 32 &&
                rows.Children.OfType<TreeDataGridRow>().Sum(row => row.CellsPresenter?.Children.OfType<TreeDataGridCell>().Count(cell => cell.RowIndex < 0) ?? 0) <= 256,
                "Shared row layout exceeded the existing row/cell pool bounds.");
            var broughtRow = rows.BringIntoView(160);
            await Settle();
            Check(broughtRow is TreeDataGridRow && ReferenceEquals(rows.TryGetElement(160)?.Model, items[160]),
                "The built-in rows presenter could not bring an unrealized variable-height row into view.");
            CheckRows();
            rows.Items = null;
            rows.AfterRealize = () => { rows.AfterRealize = null; rows.Items = null; };
            rows.Items = view.Rows;
            rows.Measure(new Size(258, 178));
            Check(rows.RealizedRows.Count == 0 && rows.RealizedCells.Count == 0 &&
                rows.Children.OfType<TreeDataGridRow>().All(row => row.RowIndex == -1 && row.Model is null),
                "Replacing Items inside row realization retained an in-flight row or cell model.");

            presenter.BeforeMeasure = () => { presenter.BeforeMeasure = null; presenter.Items = null; };
            presenter.Measure(new Size(259, 179));
            Check(!presenter.GetRealizedElements().Any() && presenter.Children.OfType<Control>().All(value => value.DataContext is null),
                "Collection replacement during a generic measure pass retained an in-flight model.");
            Console.WriteLine("UNO_RUNTIME_GENERIC_PRESENTERS_PASSED: reference subclass hooks, variable-height virtualization, collection remapping, parent retention, bring-into-view, unload/reload, column measurement, built-in header/cell/row adoption, bounded pools and in-layout source retirement");
        }
        finally
        {
            presenter.Items = null;
            columns.Items = null;
            headers.Items = null;
            cells.Unrealize();
            cells.Rows = null;
            cells.Items = null;
            rows.Items = null;
            rows.Columns = null;
            page.Content = previous;
        }

        void CheckModels()
        {
            foreach (var element in presenter.GetRealizedElements())
                Check(ReferenceEquals(element.DataContext, items[(int)element.Tag!]), "A realized index points to the wrong item after collection/viewport changes.");
        }
        void CheckRows()
        {
            foreach (var row in rows.RealizedRows)
            {
                Check(ReferenceEquals(row.Model, items[row.RowIndex]) && row.CellsPresenter?.RealizedCells.All(cell =>
                    ReferenceEquals(cell.RowModel, items[row.RowIndex])) == true,
                    "A built-in row/cell retained the wrong Core model after an index/viewport change.");
                Check(Math.Abs(Microsoft.UI.Xaml.Controls.Primitives.LayoutInformation.GetLayoutSlot(row).Y - rows.GetRowStart(row.RowIndex)) < 1,
                    "Row arrangement did not use the sparse variable-height geometry.");
            }
        }
        async Task Settle() { await Task.Delay(100); scroll.UpdateLayout(); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed record Item(string Name, double Height);

    private sealed partial class RowsPresenter : TreeDataGridRowsPresenter
    {
        public int Realizations { get; private set; }
        public int Measurements { get; private set; }
        public int Arrangements { get; private set; }
        public Action? AfterRealize { get; set; }
        protected override void RealizeElement(Control element, TreeDataGridCore.Models.IRow item, int index)
        {
            ++Realizations;
            base.RealizeElement(element, item, index);
            element.Height = ((Item)((TreeDataGridRow)element).Model!).Height;
            AfterRealize?.Invoke();
        }
        protected override Size MeasureElement(int index, Control element, Size availableSize)
        {
            ++Measurements;
            return base.MeasureElement(index, element, availableSize);
        }
        protected override Rect ArrangeElement(int index, Control element, Rect rect)
        {
            ++Arrangements;
            return base.ArrangeElement(index, element, rect);
        }
    }

    private sealed class Factory : TreeDataGridElementFactory
    {
        protected override Control CreateElement(object? data) => data is Item or UIColumn
            ? new ContentControl { IsTabStop = true } : base.CreateElement(data);
        protected override string GetDataRecycleKey(object? data) => data is Item or UIColumn
            ? typeof(ContentControl).FullName! : base.GetDataRecycleKey(data);
    }

    private sealed partial class ItemPresenter : TreeDataGridPresenterBase<Item>
    {
        public int Realizations { get; private set; }
        public int IndexUpdates { get; private set; }
        public int MeasureCalls { get; private set; }
        public int ArrangeCalls { get; private set; }
        public Action? BeforeMeasure { get; set; }
        protected override Orientation Orientation => Orientation.Vertical;
        protected override void RealizeElement(Control element, Item item, int index)
        {
            ++Realizations;
            element.DataContext = item;
            element.Tag = index;
            element.Height = item.Height;
            ((ContentControl)element).Content = item.Name;
        }
        protected override void UpdateElementIndex(Control element, int oldIndex, int newIndex) { ++IndexUpdates; element.Tag = newIndex; }
        protected override void UnrealizeElement(Control element)
        {
            element.DataContext = null;
            element.Tag = -1;
            ((ContentControl)element).Content = null;
        }
        protected override Size MeasureElement(int index, Control element, Size availableSize)
        {
            ++MeasureCalls;
            BeforeMeasure?.Invoke();
            return base.MeasureElement(index, element, availableSize);
        }
        protected override Rect ArrangeElement(int index, Control element, Rect rect)
        {
            ++ArrangeCalls;
            return base.ArrangeElement(index, element, rect);
        }
    }

    private sealed partial class ColumnPresenter : TreeDataGridColumnarPresenterBase<UIColumn>
    {
        public int ColumnMeasures { get; private set; }
        protected override Orientation Orientation => Orientation.Horizontal;
        protected override void RealizeElement(Control element, UIColumn item, int index)
        {
            element.DataContext = item;
            element.Tag = index;
            element.Height = 32;
            ((ContentControl)element).Content = item.Header;
        }
        protected override void UpdateElementIndex(Control element, int oldIndex, int newIndex) => element.Tag = newIndex;
        protected override void UnrealizeElement(Control element) { element.DataContext = null; ((ContentControl)element).Content = null; }
        protected override Size MeasureElement(int index, Control element, Size availableSize)
        {
            ++ColumnMeasures;
            return MeasureColumnElement(index, -1, element, availableSize);
        }
    }

    private sealed partial class HeaderPresenter : TreeDataGridColumnHeadersPresenter
    {
        public int Realizations { get; private set; }
        public int Measurements { get; private set; }
        public Action? AfterRealize { get; set; }
        protected override void RealizeElement(Control element, UIColumn column, int index)
        {
            ++Realizations;
            base.RealizeElement(element, column, index);
            AfterRealize?.Invoke();
        }
        protected override Size MeasureElement(int index, Control element, Size availableSize)
        {
            ++Measurements;
            return base.MeasureElement(index, element, availableSize);
        }
    }

    private sealed partial class CellsPresenter : TreeDataGridCellsPresenter
    {
        public int Realizations { get; private set; }
        public int Measurements { get; private set; }
        public Action? AfterRealize { get; set; }
        protected override void RealizeElement(Control element, UIColumn column, int index)
        {
            ++Realizations;
            base.RealizeElement(element, column, index);
            AfterRealize?.Invoke();
        }
        protected override Size MeasureElement(int index, Control element, Size availableSize)
        {
            ++Measurements;
            return base.MeasureElement(index, element, availableSize);
        }
    }
}
