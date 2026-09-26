using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using Uno.Controls.Primitives;
using Core = TreeDataGridCore.Models;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Public presenter query contracts exercised by native and trimmed package consumers.</summary>
internal static class PresenterIndexRuntimeChecks
{
    internal static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        VerifyStandalone();
        var options = grid.PresentationOptions;
        var showHeaders = grid.ShowColumnHeaders;
        grid.Model = null;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 200).Select(i => new Item($"Index {i:000}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        for (var i = 0; i < 12; ++i)
            source.Columns.Add(new Core.TextColumn<Item, string>($"C{i}", x => x.Name, width: new(128)));
        try
        {
            grid.PresentationOptions = null;
            grid.ShowColumnHeaders = true;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            Verify(200, 12);
            var oldRows = grid.RowsPresenter!.RealizedRows.ToArray();
            var oldCells = grid.RowsPresenter.RealizedCells.ToArray();
            var headers = Headers();
            Check(grid.BringCellIntoView(150, 9), "Presenter index query fixture could not reach an offscreen cell.");
            await Settle();
            Verify(200, 12);
            Check(oldRows.Any(row => row.RowIndex < 0 || row.RowIndex >= 100), "The fixture did not recycle or retire old rows.");
            source.Columns[2].IsVisible = false;
            source.Columns.Move(0, 10);
            items.RemoveAt(0);
            await Settle();
            Verify(199, 11);
            source.SortBy((Core.IColumn<Item>)source.Columns[0], ListSortDirection.Descending);
            await Settle();
            Verify(199, 11);
            var rowsPresenter = grid.RowsPresenter!;
            var beforeRows = rowsPresenter.RealizedRows.Count;
            var beforeCells = rowsPresenter.RealizedCells.Count;
            for (var i = 0; i < 4096; ++i)
            {
                Check(rowsPresenter.TryGetTotalCount(out var rows) && rows == 199, "Warm total-row count changed.");
                Check(headers.TryGetTotalCount(out var columns) && columns == 11, "Warm total-column count changed.");
            }
            Check(beforeRows == rowsPresenter.RealizedRows.Count && beforeCells == rowsPresenter.RealizedCells.Count,
                "Index/count queries realized additional native elements.");
            var currentRows = rowsPresenter.RealizedRows.ToArray();
            var currentCells = rowsPresenter.RealizedCells.ToArray();
            var currentHeaders = headers.RealizedHeaders.ToArray();
            grid.Model = null;
            Check(!rowsPresenter.TryGetTotalCount(out var removedRows) && removedRows == 0 &&
                !headers.TryGetTotalCount(out var removedColumns) && removedColumns == 0,
                "Retired source retained presenter count state.");
            foreach (var row in currentRows.Concat(oldRows)) Check(rowsPresenter.GetChildIndex(row) == -1, "A retired row retained its index.");
            foreach (var cell in currentCells.Concat(oldCells)) Check(cell.ColumnIndex == -1, "A retired cell retained its index.");
            foreach (var header in currentHeaders) Check(headers.GetChildIndex(header) == -1, "A retired header retained its index.");
            Check(source.Rows.Count == 199, "Native query lifetime disposed or changed the caller-owned source.");
            Console.WriteLine("UNO_RUNTIME_PRESENTER_INDEX_QUERIES_PASSED: absent/empty/current counts, no enumeration, loaded indices, scrolling/reuse, hidden/reordered columns, sorting, no query realization and retirement");
        }
        finally
        {
            grid.Model = null;
            grid.PresentationOptions = options;
            grid.ShowColumnHeaders = showHeaders;
        }

        TreeDataGridColumnHeadersPresenter Headers() => ShowcaseRuntimeChecks.Descendants(grid)
            .OfType<TreeDataGridColumnHeadersPresenter>().First();
        async Task Settle() { await Task.Delay(120); grid.UpdateLayout(); }
        void Verify(int rowCount, int columnCount)
        {
            var rows = grid.RowsPresenter!;
            var headers = Headers();
            Check(rows.TryGetTotalCount(out var actualRows) && actualRows == rowCount && actualRows > rows.RealizedRows.Count,
                "Row count was confused with the virtualized viewport.");
            Check(headers.TryGetTotalCount(out var actualColumns) && actualColumns == columnCount,
                "Header count did not follow the visible column projection.");
            foreach (var row in rows.RealizedRows)
            {
                Check(rows.GetChildIndex(row) == row.RowIndex && ReferenceEquals(row.Model, source.Rows[row.RowIndex].Model),
                    "Row query lost current Core/model index identity.");
                var cells = row.CellsPresenter!;
                Check(cells.TryGetTotalCount(out var count) && count == columnCount, "Cell presenter used row count instead of column count.");
                foreach (var cell in cells.RealizedCells)
                    Check(cells.GetChildIndex(cell) == cell.ColumnIndex && ReferenceEquals(cell.RowModel, row.Model),
                        "Cell index query does not follow recycled native cells.");
            }
            foreach (var header in headers.RealizedHeaders)
                Check(headers.GetChildIndex(header) == header.ColumnIndex && header.ColumnIndex < columnCount,
                    "Header index did not track projection changes.");
        }
    }

    private static void VerifyStandalone()
    {
        var rows = new TreeDataGridRowsPresenter();
        var cells = new TreeDataGridCellsPresenter();
        var headers = new TreeDataGridColumnHeadersPresenter();
        Check(!rows.TryGetTotalCount(out var count) && count == 0, "Absent rows must report false/zero.");
        Check(!cells.TryGetTotalCount(out count) && count == 0, "Absent cells must report false/zero.");
        Check(!headers.TryGetTotalCount(out count) && count == 0, "Absent headers must report false/zero.");
        rows.Items = Array.Empty<Core.IRow>();
        cells.Items = Array.Empty<UI.IColumn>();
        headers.Items = Array.Empty<UI.IColumn>();
        Check(rows.TryGetTotalCount(out count) && count == 0 && cells.TryGetTotalCount(out count) && count == 0 &&
            headers.TryGetTotalCount(out count) && count == 0, "Attached empty lists must report true/zero.");
        var countOnly = new CountOnlyList<UI.IColumn>(int.MaxValue);
        headers.Items = countOnly;
        // Native Items assignment may inspect Count; measure only the query.
        countOnly.Reads = 0;
        Check(headers.TryGetTotalCount(out count) && count == int.MaxValue && countOnly.Reads == 1,
            "Count query must read Count once, without enumeration or realization.");
        var failure = new InvalidOperationException("Expected Count failure");
        countOnly.Error = failure;
        Exception? observed = null;
        try { headers.TryGetTotalCount(out _); }
        catch (Exception error) { observed = error; }
        Check(ReferenceEquals(failure, observed), "Count query changed custom getter exception identity.");
        countOnly.Error = null;
        headers.Items = null;
        rows.Items = null;
        cells.Items = null;
        var other = new Border();
        Check(rows.GetChildIndex(other) == -1 && cells.GetChildIndex(other) == -1 && headers.GetChildIndex(other) == -1,
            "Non-container child must report -1.");
        Check(rows.GetChildIndex(new TreeDataGridRow()) == -1 && cells.GetChildIndex(new TreeDataGridCell()) == -1 &&
            headers.GetChildIndex(new TreeDataGridColumnHeader()) == -1, "Unrealized containers must report -1.");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Item(string name) { public string Name => name; }
    private sealed class CountOnlyList<T>(int count) : IReadOnlyList<T>
    {
        internal int Reads;
        internal Exception? Error;
        public int Count { get { ++Reads; if (Error is { } error) throw error; return count; } }
        public T this[int index] => throw new InvalidOperationException("Count query must not access an item.");
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("Count query must not enumerate.");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
