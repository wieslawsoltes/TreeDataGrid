using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Native buffered realization checks for the post-implementation validation pass.</summary>
internal static class ViewportCacheRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        grid.Model = null;
        var previousHeight = grid.Height;
        var previousRowHeight = grid.RowHeight;
        var presenter = grid.RowsPresenter!;
        var previousCache = presenter.CacheLength;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 200).Select(i => new Item($"Row {i:000}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name, width: new(200)));
        try
        {
            grid.Height = 232;
            grid.RowHeight = 20;
            presenter.CacheLength = 0.5;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            var height = grid.Scroll!.ViewportHeight;
            Check(height > 0, "The cache fixture has no viewport.");
            var initial = presenter.RealizedRows.ToHashSet();
            VerifyQueries();
            Check(initial.Count == (int)Math.Ceiling(2 * height / 20), "The top cache did not redistribute its missing leading buffer.");
            var unloads = 0;
            foreach (var row in initial) row.Unloaded += (_, _) => ++unloads;
            var parents = initial.ToDictionary(row => row, row => row.Parent);
            var cells = presenter.RealizedCells.ToHashSet();
            foreach (var cell in cells) cell.Unloaded += (_, _) => ++unloads;

            grid.Scroll!.ChangeView(null, height / 4, null, true);
            await Settle();
            Check(initial.SetEquals(presenter.RealizedRows) && cells.SetEquals(presenter.RealizedCells),
                "A small buffered scroll replaced realized containers.");
            Check(unloads == 0, "A small buffered scroll unloaded retained controls.");

            grid.Scroll!.ChangeView(null, 0, null, true);
            await Settle();
            presenter.CacheLength = 0;
            await Settle();
            VerifyQueries();
            Check(presenter.RealizedRows.Count == (int)Math.Ceiling(height / 20), "Zero cache still realizes guard/buffer rows.");
            presenter.CacheLength = 0.5;
            await Settle();
            Check(initial.SetEquals(presenter.RealizedRows) && cells.SetEquals(presenter.RealizedCells) && unloads == 0 &&
                parents.All(pair => ReferenceEquals(pair.Key.Parent, pair.Value)),
                "Shrinking/growing the cache detached or replaced compatible pooled controls.");

            foreach (var invalid in new[] { -1d, 2.1, double.NaN, double.PositiveInfinity })
            {
                var rejected = false;
                try { presenter.SetValue(TreeDataGridRowsPresenter.CacheLengthProperty, invalid); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected && presenter.CacheLength == 0.5, "Invalid native cache assignment was not rolled back.");
            }
            grid.Scroll!.ChangeView(null, 2 * height, null, true);
            await Settle();
            VerifyQueries();
            Check(presenter.RealizedRows.Min(row => row.RowIndex) > 0 && presenter.RealizedRows.Count <= Math.Ceiling(2 * height / 20) + 1,
                "Crossing the cache boundary failed to recenter a bounded row window.");

            // Width changes must not be skipped by the viewport cache, including fixed-height mode.
            source.Columns[0].Width = new(120);
            await Settle();
            Check(presenter.RealizedCells.All(cell => Math.Abs(cell.ActualWidth - 120) < 1),
                "A cached fixed-height viewport ignored changed column geometry.");
            items.Clear();
            await Settle();
            Check(presenter.RealizedRows.Count == 0 && presenter.RealizedCells.Count == 0, "Emptying the source retained buffered realizations.");
            items.Add(new("Restored row"));
            await Settle();
            Check(presenter.RealizedRows.Count == 1 && ReferenceEquals(grid.TryGetRow(0)?.Model, items[0]),
                "The cache did not recover after the source became empty.");
            grid.Model = null;
            Check(presenter.RealizedCells.Count == 0, "Source removal retained cached cells.");
            Check(!presenter.GetRealizedElements().Any() && !grid.ColumnHeadersPresenter!.GetRealizedElements().Any(),
                "Public presenter queries returned pooled or retired containers.");
            Console.WriteLine("UNO_RUNTIME_VIEWPORT_CACHE_PASSED: edge buffer, small-scroll retention, cache shrink/grow, DP rollback, recentering, fixed-height resize, empty/reset");
        }
        finally
        {
            grid.Model = null;
            presenter.CacheLength = previousCache;
            grid.RowHeight = previousRowHeight;
            grid.Height = previousHeight;
        }
        async Task Settle() { grid.UpdateLayout(); await Task.Delay(100); grid.UpdateLayout(); }
        void VerifyQueries()
        {
            var rows = presenter.GetRealizedElements().Cast<TreeDataGridRow>().ToArray();
            Check(rows.Select(row => row.RowIndex).SequenceEqual(presenter.RealizedRows.Select(row => row.RowIndex).OrderBy(index => index)),
                "The public row enumeration is not in displayed-index order.");
            foreach (var row in rows)
            {
                var cellsPresenter = row.CellsPresenter!;
                var cells = cellsPresenter.GetRealizedElements().Cast<TreeDataGridCell>().ToArray();
                Check(cells.Select(cell => cell.ColumnIndex).SequenceEqual(cellsPresenter.RealizedCells.Select(cell => cell.ColumnIndex).OrderBy(index => index)) &&
                    cells.All(cell => ReferenceEquals(cellsPresenter.TryGetElement(cell.ColumnIndex), cell)),
                    "The public cell query returned unordered or retired cells.");
            }
            var headers = grid.ColumnHeadersPresenter!.GetRealizedElements().Cast<TreeDataGridColumnHeader>().ToArray();
            Check(headers.Select(header => header.ColumnIndex).SequenceEqual(headers.Select(header => header.ColumnIndex).OrderBy(index => index)) &&
                headers.All(header => ReferenceEquals(grid.ColumnHeadersPresenter!.TryGetElement(header.ColumnIndex), header)) &&
                grid.ColumnHeadersPresenter!.TryGetElement(-1) is null && grid.ColumnHeadersPresenter!.TryGetElement(int.MaxValue) is null,
                "Header enumeration/index lookup is inconsistent.");
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed record Item(string Name);
}
