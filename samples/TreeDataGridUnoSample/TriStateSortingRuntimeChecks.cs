using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using UI = Uno.Controls.Models.TreeDataGrid;
using Grid = Uno.Controls.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Header activation on a loaded native grid, also consumed by the trimmed browser showcase.</summary>
internal static class TriStateSortingRuntimeChecks
{
    internal static async Task RunAsync(Grid grid)
    {
        var previousModel = grid.Model;
        var previousOptions = grid.PresentationOptions;
        var previousPermission = grid.CanUserSortColumns;
        grid.Model = null;
        var items = new ObservableCollection<Item>([new("c"), new("a"), new("b"), new("e"), new("d")]);
        using var source = new FlatTreeDataGridSource<Item>(items);
        var definition = new TextColumn<Item, string>("Name", x => x.Name, width: new(180)) { PresentationKey = "sort-cycle" };
        source.Columns.Add(definition);
        source.Columns.Add(new TextColumn<Item, string>("Other", x => x.Name, width: new(180)));
        var policy = new UI.TextColumnOptions<Item>();
        PolicyColumn? column = null;
        var factories = new TreeDataGridPresentationOptions<Item>();
        factories.Columns.Add("sort-cycle", _ => column = new(definition, policy));
        var sorted = 0;
        source.Sorted += () => ++sorted;
        var checks = 0;
        try
        {
            grid.CanUserSortColumns = true;
            grid.PresentationOptions = factories;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Ready();
            var selection = source.RowSelection!;
            selection.SelectedIndex = new IndexPath(2);
            var selected = selection.SelectedItem;
            var rows = source.Rows;

            await Activate(); CheckSort(ListSortDirection.Ascending, items[1]);
            await Activate(); CheckSort(ListSortDirection.Descending, items[3]);
            await Activate(); CheckSort(ListSortDirection.Ascending, items[1]);
            Case("default two-state cycle");

            await Activate();
            policy.AllowTriStateSorting = true;
            var before = sorted;
            await Activate();
            CheckSort(null, items[0]);
            Check(sorted == before + 1 && ReferenceEquals(rows, source.Rows) && ReferenceEquals(selection, source.RowSelection) &&
                ReferenceEquals(selected, selection.SelectedItem), "Clearing sort replaced Core rows/selection or raised more than one Sorted event.");
            Check(source.Rows.Select(row => row.Model).SequenceEqual(items), "The third click did not restore source order.");
            Case("live opt-in restores source order and selection");

            await Activate(); CheckSort(ListSortDirection.Ascending, items[1]);
            await Activate(); CheckSort(ListSortDirection.Descending, items[3]);
            policy.AllowTriStateSorting = false;
            await Activate(); CheckSort(ListSortDirection.Ascending, items[1]);
            Case("live opt-out preserves two-state behavior");

            await Activate();
            policy.AllowTriStateSorting = true;
            policy.CanUserSortColumn = false;
            before = sorted;
            await Activate();
            Check(sorted == before && definition.SortDirection == ListSortDirection.Descending, "A denied column cleared the source sort.");
            policy.CanUserSortColumn = true;
            grid.CanUserSortColumns = false;
            await Activate();
            Check(sorted == before && source.IsSorted, "A grid-level sorting veto cleared the source sort.");
            grid.CanUserSortColumns = true;
            Case("column and grid permissions");

            column!.OnPolicy = () => policy.CanUserSortColumn = false;
            await Activate();
            Check(sorted == before && source.IsSorted, "Policy callback permission revocation was ignored.");
            policy.CanUserSortColumn = true;
            Case("permission revoked inside cycle getter");

            column.OnPolicy = () => source.SortBy(definition, ListSortDirection.Ascending);
            await Activate();
            Check(sorted == before + 1 && definition.SortDirection == ListSortDirection.Ascending,
                "An obsolete outer third click overrode a newer sort from its policy callback.");
            Case("nested source sort wins");

            source.SortBy(definition, ListSortDirection.Descending);
            source.Columns.Move(0, 1);
            await Ready(1);
            await Activate(1);
            CheckSort(null, items[0], 1);
            Check(source.Columns.All(value => value.SortDirection is null), "Clearing a reordered header left a stale indicator.");
            source.Columns[0].IsVisible = false;
            await Ready();
            await Activate();
            await Activate();
            await Activate();
            CheckSort(null, items[0]);
            Case("reordered and hidden column identity");

            source.SortBy(definition, ListSortDirection.Descending);
            items.Move(0, 4);
            items.Insert(1, new("f"));
            await Activate();
            Check(!source.IsSorted && source.Rows.Select(row => row.Model).SequenceEqual(items),
                "Clearing sort restored an obsolete copy instead of the current collection order.");
            Case("mutations during sorting");

            source.SortBy(definition, ListSortDirection.Descending);
            using var replacement = new FlatTreeDataGridSource<Item>([new("z"), new("x")]);
            replacement.Columns.Add(new TextColumn<Item, string>("Replacement", x => x.Name, width: new(180)));
            replacement.SortBy(replacement.Columns[0], ListSortDirection.Ascending);
            column.OnPolicy = () => grid.Model = replacement;
            await Activate();
            await Ready();
            Check(ReferenceEquals(grid.Model, replacement) && replacement.IsSorted && source.IsSorted,
                "An old header's third click cleared the retired or replacement source.");
            Case("source replacement inside cycle getter");

            grid.Model = null;
            grid.PresentationOptions = null;
            var parent = new Item("c");
            parent.Children.Add(new("z")); parent.Children.Add(new("x"));
            using var hierarchy = new HierarchicalTreeDataGridSource<Item>([parent, new("a")]);
            hierarchy.WithHierarchicalExpanderTextColumn(x => x.Name, x => x.Children,
                value => { value.AllowTriStateSorting = true; value.Width = new(200); });
            var expanded = (IExpanderRow<Item>)hierarchy.Rows[0];
            expanded.IsExpanded = true;
            hierarchy.RowSelection!.SelectedIndex = new IndexPath(0, 1);
            var selectedChild = hierarchy.RowSelection.SelectedItem;
            grid.Model = hierarchy;
            await Ready();
            await Activate();
            Check(hierarchy.Rows.Select(row => ((Item)row.Model!).Name).SequenceEqual(["a", "c", "x", "z"]),
                "The expander header did not sort root and child rows.");
            await Activate();
            await Activate();
            Check(!hierarchy.IsSorted && expanded.IsExpanded && ReferenceEquals(selectedChild, hierarchy.RowSelection.SelectedItem) &&
                hierarchy.Rows.Select(row => ((Item)row.Model!).Name).SequenceEqual(["c", "z", "x", "a"]),
                "Hierarchical third-click reset lost expansion, child selection or root/child source order.");
            Case("hierarchical order expansion and selection");
            grid.Model = null;
        }
        finally
        {
            grid.Model = null;
            grid.CanUserSortColumns = previousPermission;
            grid.PresentationOptions = previousOptions;
            grid.Model = previousModel;
        }
        Console.WriteLine($"UNO_RUNTIME_TRISTATE_SORTING_PASSED: cases={checks}; loaded native header Invoke, default/opt-in cycles, live policy, Core identity, selection, reordered/hidden columns, current source order, callback retirement and hierarchy");

        void Case(string name) { ++checks; Console.WriteLine("UNO_TRISTATE_SORT_CASE_PASSED: " + name); }
        async Task Ready(int index = 0) => await Until(() => grid.ColumnHeadersPresenter?.TryGetElement(index) is { IsLoaded: true, ActualWidth: > 0 });
        async Task Activate(int index = 0)
        {
            await Ready(index);
            var header = grid.ColumnHeadersPresenter!.TryGetElement(index)!;
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(header);
            var invoke = peer?.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            Check(invoke is not null, "A loaded native header did not expose its Invoke pattern.");
            var clicks = 0;
            RoutedEventHandler clicked = (_, _) => ++clicks;
            header.Click += clicked;
            try { invoke!.Invoke(); await Until(() => clicks > 0); }
            finally { header.Click -= clicked; }
            Check(clicks == 1, "One header invocation delivered multiple clicks.");
            grid.UpdateLayout();
        }
        async Task Until(Func<bool> ready)
        {
            var watch = Stopwatch.StartNew();
            while (true)
            {
                grid.UpdateLayout();
                if (ready()) return;
                if (watch.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Native sort-cycle activation/layout did not settle.");
                await Task.Delay(20);
            }
        }
        void CheckSort(ListSortDirection? direction, Item first, int headerIndex = 0)
        {
            Check(source.IsSorted == direction.HasValue && definition.SortDirection == direction &&
                ReferenceEquals(source.Rows[0].Model, first), "The native header selected the wrong sort state or Core row order.");
            Check(grid.ColumnHeadersPresenter!.TryGetElement(headerIndex)!.SortDirection == direction, "The visible sort glyph did not follow Core.");
            foreach (var cell in grid.RowsPresenter!.RealizedCells)
                Check(ReferenceEquals(cell.RowModel, source.Rows[cell.RowIndex].Model), "A sorted realized cell retained an obsolete Core model.");
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class PolicyColumn(ValueColumn<Item, string> definition, UI.TextColumnOptions<Item> options)
        : ValueCellColumn<Item, string>(definition, CellKind.Text, viewOptions: options)
    {
        internal Action? OnPolicy;
        public override bool AllowTriStateSorting
        {
            get
            {
                var result = options.AllowTriStateSorting;
                var callback = OnPolicy; OnPolicy = null; callback?.Invoke();
                return result;
            }
        }
    }
    private sealed class Item(string name)
    {
        public string Name { get; set; } = name;
        public ObservableCollection<Item> Children { get; } = new();
    }
}
