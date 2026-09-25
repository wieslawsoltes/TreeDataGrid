using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Consumes the public built-in selector/comparison APIs in real native and trimmed browser grids.</summary>
internal static class BuiltInColumnComparisonRuntimeChecks
{
    internal static async Task RunAsync(global::Uno.Controls.TreeDataGrid grid)
    {
        var previousOptions = grid.PresentationOptions;
        grid.Model = null;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 160).Select(i => new Item($"Name {159 - i:D3}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        var definition = ValueColumn<Item, string?>.FromDelegate("Name", static item => item.Name,
            propertyName: nameof(Item.Name), setter: static (item, value) => item.Name = value ?? string.Empty, width: new(280));
        definition.PresentationKey = "BuiltIn.Comparison";
        source.Columns.Add(definition);
        Comparison<Item?> viewAscending = static (first, second) => StringComparer.Ordinal.Compare(second?.Name, first?.Name);
        var options = new UI.TextColumnOptions<Item>
        {
            StringFormat = "Display: {0}", CompareAscending = viewAscending,
        };
        UI.TextColumn<Item, string?>? view = null;
        var presentationOptions = new TreeDataGridPresentationOptions<Item>();
        presentationOptions.Columns.Add("BuiltIn.Comparison", _ => view = new(definition, options));
        try
        {
            grid.PresentationOptions = presentationOptions;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            Check(view is not null && ReferenceEquals(view.Model, definition) && ReferenceEquals(grid.Presentation!.Model, source),
                "The built-in public comparison path replaced the original Core definition or source.");
            TypedColumnContractRuntimeChecks.Run(view!, (IRow<Item>)source.Rows[0]);
            Check(ReferenceEquals(view!.ValueSelector, definition.Getter) && ReferenceEquals(view.ValueSelector(items[0]), items[0].Name),
                "The public selector did not return the raw model value through its cached Core accessor.");
            Check(ReferenceEquals(view.GetComparison(ListSortDirection.Ascending), viewAscending),
                "The configured public comparison delegate lost identity.");
            Check(view.GetComparison(ListSortDirection.Ascending)!(items[0], items[1]) < 0,
                "The view fixture must deliberately disagree with ascending Core sorting.");
            options.CompareAscending = static (_, _) => 0;
            options.CanUserSortColumn = false;
            Check(ReferenceEquals(view.GetComparison(ListSortDirection.Ascending), viewAscending),
                "A mutable view option replaced the captured value-comparison policy.");
            Check(view.GetComparison((ListSortDirection)42) is null, "An invalid direction selected a comparator.");

            // The view's public comparator is an explicit utility/extension API.
            // Core sorting must not be silently replaced with the view's policy.
            source.SortBy(definition, ListSortDirection.Ascending);
            grid.Scroll.ChangeView(0, 0, null, true);
            await Settle();
            Check(((Item)source.Rows[0].Model!).Name == "Name 000", "Public view comparison changed the Core source's sort policy.");
            VerifyRenderedRows();
            var edited = (Item)source.Rows[0].Model!;
            Check(grid.BeginEdit(0, 0), "The public comparison implementation broke native text editing.");
            grid.EditingCell!.EditingText = "ZZ edited";
            Check(grid.CommitEdit() && edited.Name == "ZZ edited", "Native writeback stopped targeting the original sorted model.");
            Check(view.ValueSelector(edited) == "ZZ edited", "The public selector retained a stale model value.");
            source.SortBy(definition, ListSortDirection.Descending);
            grid.Scroll.ChangeView(0, 0, null, true);
            await Settle();
            Check(ReferenceEquals(source.Rows[0].Model, edited), "Descending Core sort failed after native writeback.");
            VerifyRenderedRows();
            Check(grid.BringCellIntoView(130, 0), "The comparison-enabled view lost distant navigation.");
            await Settle();
            VerifyRenderedRows();
            Check(grid.RowsPresenter!.RealizedRows.Count is > 0 and < 128, "Comparison APIs changed bounded native realization.");

            // A key-only template comparison must not resolve a template or
            // fall back to comparing noncomparable model objects.
            var templateOptions = new UI.TemplateColumnOptions<Item> { CanUserSortColumn = false };
            using var template = new UI.TemplateColumn<Item>("Template", (object)"Unused.Template.Key", options: templateOptions);
            Check(template.GetComparison(ListSortDirection.Ascending) is null, "A template without an explicit comparison supplied a fallback.");
            templateOptions.CompareAscending = viewAscending;
            Check(ReferenceEquals(template.GetComparison(ListSortDirection.Ascending), viewAscending),
                "Template comparison did not read its live delegate independently of UI sorting policy.");
            templateOptions.CompareAscending = null;
            Check(template.GetComparison(ListSortDirection.Ascending) is null, "A removed template comparer stayed cached.");
        }
        finally
        {
            grid.Model = null;
            grid.PresentationOptions = previousOptions;
        }
        Check(items.All(item => item.Subscribers == 0), "Public comparison use retained bindings after the grid source was removed.");
        Check(source.Rows.Count == items.Count, "View retirement disposed the borrowed Core source.");
        await TriStateSortingRuntimeChecks.RunAsync(grid);
        Console.WriteLine("UNO_RUNTIME_BUILTIN_COLUMN_COMPARISON_PASSED: cached raw selector, captured value policy, live template policy, independent Core sorting, native edit/re-sort, distant rendering, bounded realization and source cleanup");

        async Task Settle() { await Task.Delay(120); grid.UpdateLayout(); }
        void VerifyRenderedRows()
        {
            var count = 0;
            foreach (var cell in grid.RowsPresenter!.RealizedCells.Where(cell => cell.ColumnIndex == 0))
            {
                var model = (Item)source.Rows[cell.RowIndex].Model!;
                Check(ReferenceEquals(cell.RowModel, model), "A sorted native cell references a different Core model.");
                Check(ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().Any(text => text.Text == "Display: " + model.Name),
                    "The public raw selector replaced formatted native display or left obsolete text.");
                ++count;
            }
            Check(count > 0, "No rendered rows were inspected.");
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        private PropertyChangedEventHandler? _changed;
        public string Name { get => _name; set { _name = value; _changed?.Invoke(this, new(nameof(Name))); } }
        internal int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
