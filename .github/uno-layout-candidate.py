from pathlib import Path
import json

changed = []
def replace(path, old, new, count=1):
    file = Path(path)
    text = file.read_text()
    assert text.count(old) == count, (path, old, text.count(old))
    file.write_text(text.replace(old, new))
    if path not in changed:
        changed.append(path)

row = 'src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridRow.cs'
replace(row, '    internal bool IsResettingCells { get; set; }', '''    internal bool IsResettingCells { get; set; }
    // Set only by the owning presenter during synchronous viewport layout.
    // Public/standalone unrealization and collection removal still hide now.
    internal bool IsRecyclingVisibilityDeferred { get; set; }''')
replace(row, '        var realization = ++RealizationVersion;', '''        IsRecyclingVisibilityDeferred = false;
        var realization = ++RealizationVersion;''')
replace(row, '                Visibility = Visibility.Collapsed;', '''                if (!IsRecyclingVisibilityDeferred) Visibility = Visibility.Collapsed;''')
replace(row, '''    internal void Release()
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? error = null;''', '''    internal void Release()
    {
        IsRecyclingVisibilityDeferred = false;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? error = null;
        try { if (RowIndex < 0) Visibility = Visibility.Collapsed; }
        catch (Exception e) { error = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e); }''')

presenter = 'src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridRowsPresenter.cs'
replace(presenter, '    private readonly List<TreeDataGridRow> _pool = new(32);', '''    private readonly List<TreeDataGridRow> _pool = new(32);
    private readonly HashSet<TreeDataGridRow> _deferredVisibility = new();''')
replace(presenter, '''        row.Unrealize(reason);
    }

    protected override void RecycleElementToFactory''', '''        // Collapsing and immediately showing a whole native subtree produces
        // compositor damage and property propagation even when the same row is
        // reused in this pass. Do not defer across a dispatcher turn, removal,
        // source reset or a caller's public Unrealize invocation.
        row.IsRecyclingVisibilityDeferred = reason == TreeDataGridRowUnrealizeReason.Recycle &&
            IsInLayout && _resetDepth == 0;
        if (row.IsRecyclingVisibilityDeferred) _deferredVisibility.Add(row);
        row.Unrealize(reason);
    }

    protected override bool PreserveRecycledElementVisibility(Control element) =>
        element is TreeDataGridRow { IsRecyclingVisibilityDeferred: true };

    private void FinishDeferredVisibility()
    {
        List<Exception>? errors = null;
        // Remove ownership before setting the DP: visibility callbacks may
        // replace the source or reenter layout. Never keep a stale enumerator
        // across such callbacks. No snapshots/closures on the warmed path.
        while (_deferredVisibility.Count != 0)
        {
            using var iterator = _deferredVisibility.GetEnumerator();
            iterator.MoveNext();
            var row = iterator.Current;
            _deferredVisibility.Remove(row);
            row.IsRecyclingVisibilityDeferred = false;
            if (row.RowIndex >= 0) continue;
            try { row.Visibility = Visibility.Collapsed; }
            catch (Exception error) { (errors ??= new()).Add(error); }
        }
        if (errors is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException(errors);
    }

    protected override void RecycleElementToFactory''')
replace(presenter, '        finally { _measuringRows = false; FinalizeUnrealize(); }', '''        finally
        {
            _measuringRows = false;
            // All unused rows are hidden on every exit, including exceptions
            // and retired generations, before native layout can return/render.
            try { FinishDeferredVisibility(); }
            finally { FinalizeUnrealize(); }
        }''')

sample = 'samples/TreeDataGridUnoSample/LayoutRecyclingRuntimeChecks.cs'
assert not Path(sample).exists()
Path(sample).write_text('''using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using Uno.Controls;
using Uno.Controls.Primitives;
using GridLength = Microsoft.UI.Xaml.GridLength;

namespace TreeDataGridUnoSample;

internal static class LayoutRecyclingRuntimeChecks
{
    public static async Task RunAsync(TreeDataGrid grid)
    {
        grid.Model = null;
        var oldFactory = grid.ElementFactory;
        var oldWidth = grid.Width;
        var oldHeight = grid.Height;
        var oldRowHeight = grid.RowHeight;
        var factory = new Factory();
        var callbacks = new List<(TreeDataGridRow Row, long Token)>();
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 2000).Select(i => new Item($"Row {i:D4}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        for (var i = 0; i < 64; ++i)
            source.WithTextColumn($"Column {i}", item => item.Name,
                options => { options.Width = new GridLength(80); options.IsReadOnly = true; });
        try
        {
            grid.Width = 360;
            grid.Height = 220;
            grid.RowHeight = 28;
            grid.ElementFactory = factory;
            grid.Model = source;
            await Settle();
            grid.Scroll!.ChangeView(0, 28, null, true);
            await Settle();
            // Warm both partial-row boundaries, then use equally aligned windows.
            grid.Scroll.ChangeView(40, 42, null, true);
            await Settle();
            grid.Scroll.ChangeView(0, 28, null, true);
            await Settle();
            var created = factory.Rows.Count;
            var collapses = 0;
            foreach (var row in factory.Rows)
            {
                var token = row.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (sender, _) =>
                {
                    if (((TreeDataGridRow)sender).Visibility == Visibility.Collapsed) ++collapses;
                });
                callbacks.Add((row, token));
            }
            var parents = factory.Rows.ToDictionary(row => row, row => row.Parent);
            foreach (var point in new[] { (X: 0d, Y: 2800d), (X: 1280d, Y: 8400d), (X: 3200d, Y: 16800d), (X: 0d, Y: 28d) })
            {
                grid.Scroll.ChangeView(point.X, point.Y, null, true);
                await Settle();
                Check(factory.Rows.Count == created, "A disjoint viewport allocated new rows after boundary priming.");
                Check(collapses == 0, $"Synchronous viewport recycling collapsed {collapses} reusable rows.");
                Verify();
                Check(parents.All(pair => ReferenceEquals(pair.Key.Parent, pair.Value)), "A recycled row changed parent.");
            }
            grid.Height = 100;
            await Settle();
            Verify();
            Check(factory.Rows.Where(row => row.RowIndex < 0).All(row => row.Visibility == Visibility.Collapsed),
                "Surplus rows remained visible after shrinking the viewport.");
            grid.Model = null;
            Check(factory.Rows.All(row => row.RowIndex < 0 && row.Visibility == Visibility.Collapsed && row.Model is null),
                "Source retirement retained a visible/bound row.");
            // Direct public lifetime methods must not inherit a layout-only policy.
            var standalone = new TreeDataGridRow();
            standalone.Realize(new TreeDataGridElementFactory(), null, null, null, 0);
            standalone.Unrealize();
            Check(standalone.RowIndex == -1 && standalone.Visibility == Visibility.Collapsed,
                "Public standalone Unrealize stopped hiding synchronously.");
            Console.WriteLine($"UNO_RUNTIME_LAYOUT_RECYCLING_PASSED: rows={created}; aligned disjoint/diagonal/reverse viewports; no row hide/show; retained parents; surplus/source/public cleanup");
        }
        finally
        {
            foreach (var item in callbacks) item.Row.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, item.Token);
            grid.Model = null;
            grid.ElementFactory = oldFactory;
            grid.RowHeight = oldRowHeight;
            grid.Width = oldWidth;
            grid.Height = oldHeight;
        }
        void Verify()
        {
            var rows = grid.RowsPresenter!.RealizedRows;
            Check(rows.Count is > 0 and < 16, "Layout recycling exceeded its viewport row budget.");
            foreach (var row in rows)
            {
                Check(row.Visibility == Visibility.Visible && ReferenceEquals(row.Model, items[row.RowIndex]), "Recycled row identity/visibility is stale.");
                foreach (var cell in row.CellsPresenter!.RealizedCells)
                    Check(cell.Visibility == Visibility.Visible && ReferenceEquals(cell.RowModel, items[row.RowIndex]), "Recycled cell identity/visibility is stale.");
            }
        }
        async Task Settle() { await Task.Delay(100); grid.UpdateLayout(); }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed record Item(string Name);
    private sealed class Factory : TreeDataGridElementFactory
    {
        public List<TreeDataGridRow> Rows { get; } = new();
        protected override Control CreateElement(object? data)
        {
            var element = base.CreateElement(data);
            if (element is TreeDataGridRow row) Rows.Add(row);
            return element;
        }
    }
}
''')
changed.append(sample)
replace('samples/TreeDataGridUnoSample/App.Validation.cs',
        '            case "row-recycling-visibility": return RowRecyclingVisibilityRuntimeChecks.RunAsync(page.Grid);',
        '            case "row-recycling-visibility": return RowRecyclingVisibilityRuntimeChecks.RunAsync(page.Grid);\n            case "layout-recycling": return LayoutRecyclingRuntimeChecks.RunAsync(page.Grid);')

Path('artifacts/layout-candidate').mkdir(parents=True, exist_ok=True)
Path('artifacts/layout-candidate/files.json').write_text(json.dumps(changed))
