using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Consumed descriptors on real built-in controls over shared Core definitions.</summary>
internal static class BuiltInBindingRuntimeChecks
{
    internal static async Task RunAsync(global::Uno.Controls.TreeDataGrid grid)
    {
        var previousOptions = grid.PresentationOptions;
        grid.Model = null;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 160).Select(i => new Item(i)));
        using var source = new FlatTreeDataGridSource<Item>(items);
        // Core remains read-only. Native descriptor writeback must reach the
        // alternate field without replacing the source's selector or sort policy.
        var textDefinition = new TreeDataGridCore.Models.TextColumn<Item, string?>("Primary", x => x.Detail.Primary,
            width: new(260)) { PresentationKey = "Binding.Text" };
        var checkDefinition = new TreeDataGridCore.Models.CheckBoxColumn<Item>("Flag", x => x.Detail.Flag,
            width: new(110)) { PresentationKey = "Binding.Check" };
        source.Columns.Add(textDefinition);
        source.Columns.Add(checkDefinition);
        U.TextColumn<Item, string?>? textView = null;
        var options = new TreeDataGridPresentationOptions<Item>();
        options.Columns.Add("Binding.Text", _ =>
        {
            textView = new(textDefinition, new() { StringFormat = "[alias {0}]" });
            textView.Binding.Read = static x => x.Detail.Alias;
            textView.Binding.Write = static (x, value) => x.Detail.Alias = value ?? string.Empty;
            textView.Binding.Links = [static x => x, static x => x.Detail];
            return textView;
        });
        options.Columns.Add("Binding.Check", _ =>
        {
            var view = new U.CheckBoxColumn<Item>(checkDefinition);
            view.Binding.Read = static x => x.Detail.AlternateFlag;
            view.Binding.Write = static (x, value) => x.Detail.AlternateFlag = value;
            view.Binding.Links = [static x => x, static x => x.Detail];
            return view;
        });
        var originalChild = items[0].Detail;
        try
        {
            grid.PresentationOptions = options;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            Check(ReferenceEquals(grid.Presentation!.Model, source), "Descriptor cells copied the shared source.");
            VerifyText();
            var primary = items[0].Detail.Primary;
            var firstControl = grid.TryGetCell(0, 0);
            Check(grid.BeginEdit(0, 0), "The descriptor writer did not enable the actual native text editor.");
            grid.EditingCell!.EditingText = "Native alias edit";
            Check(grid.CommitEdit() && items[0].Detail.Alias == "Native alias edit", "Native descriptor editing did not reach the alternate field.");
            Check(items[0].Detail.Primary == primary && textDefinition.Setter is null,
                "View descriptor editing changed the independent Core definition.");
            var checkbox = grid.TryGetCell(1, 0) as TreeDataGridCheckBoxCell ?? throw new InvalidOperationException("No native checkbox.");
            Check(checkbox.IsThreeState && !checkbox.IsReadOnly, "Descriptor checkbox permissions were not consumed.");
            checkbox.Value = true;
            Check(items[0].Detail.AlternateFlag == true && items[0].Detail.Flag == false, "Checkbox wrote the wrong field.");
            checkbox.Value = null;
            Check(items[0].Detail.AlternateFlag is null, "Nullable descriptor writeback was lost.");
            items[0].Detail = new Detail("Replacement primary", "Replacement alias");
            grid.UpdateLayout();
            Check(originalChild.Subscribers == 0, "Replacing a nested owner retained old descriptor subscriptions.");
            Check(ReferenceEquals(firstControl, grid.TryGetCell(0, 0)), "Nested descriptor refresh replaced the native text control.");
            VerifyText();
            Check(grid.BringCellIntoView(130, 0), "Descriptor cells could not navigate to a distant row.");
            await Settle(); VerifyText();
            source.SortBy(textDefinition, ListSortDirection.Descending);
            grid.Scroll.ChangeView(0, 0, null, true);
            await Settle(); VerifyText();
            Check(grid.RowsPresenter!.RealizedRows.Count is > 0 and < 128, "Descriptor cells lost bounded realization.");
            var current = (Item)source.Rows[0].Model!;
            Check(textView!.GetSearchText(current) == current.Detail.Primary,
                "Changing Binding.Read changed raw incremental-search text.");
            var oldControl = grid.TryGetCell(0, 0) as TreeDataGridTextCell ?? throw new InvalidOperationException("No current text control.");
            textView.Binding.Read = static x => x.Detail.Primary;
            textView.Binding.Write = static (x, value) => x.Detail.Primary = value ?? string.Empty;
            current.Detail.Alias = "Captured alias after descriptor edit";
            grid.UpdateLayout();
            Check(oldControl.Value == "[alias Captured alias after descriptor edit]", "A descriptor edit rewrote an existing cell's captured instructions.");
            using (var fresh = textView.CreateCell(source.Rows[0]))
                Check(Equals(fresh.Value, current.Detail.Primary), "A newly created cell ignored the edited descriptor.");
            Check(grid.BeginEdit(0, 0), "The retained descriptor editor stopped working.");
            var retainedPrimary = current.Detail.Primary;
            grid.EditingCell!.EditingText = "Retained alias writer";
            Check(grid.CommitEdit() && current.Detail.Alias == "Retained alias writer" && current.Detail.Primary == retainedPrimary,
                "An existing cell used a writer from a newer descriptor snapshot.");
            grid.Model = null;
            Check(items.All(x => x.Subscribers == 0 && x.Detail.Subscribers == 0) && originalChild.Subscribers == 0,
                "Descriptor retirement leaked Core model subscriptions.");
            Check(source.Rows.Count == items.Count, "View cleanup disposed or changed the borrowed source.");
        }
        finally { grid.Model = null; grid.PresentationOptions = previousOptions; }
        Console.WriteLine("UNO_RUNTIME_BUILTIN_BINDING_PASSED: consumed mutable descriptors, native text and nullable checkbox writes, independent Core selection/sorting, nested owner cleanup, retained/fresh snapshots, raw search, bounded distant rendering and final subscription cleanup");

        async Task Settle() { await Task.Delay(120); grid.UpdateLayout(); }
        void VerifyText()
        {
            var checkedCells = 0;
            foreach (var cell in grid.RowsPresenter!.RealizedCells.Where(x => x.ColumnIndex == 0))
            {
                var item = (Item)source.Rows[cell.RowIndex].Model!;
                Check(ReferenceEquals(cell.RowModel, item), "Descriptor cell lost Core row identity.");
                Check(ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().Any(x => x.Text == "[alias " + item.Detail.Alias + "]"),
                    "Rendered descriptor text is stale.");
                ++checkedCells;
            }
            Check(checkedCells > 0, "No descriptor-backed native text cells were inspected.");
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Item(int index) : INotifyPropertyChanged
    {
        private Detail _detail = new($"Primary {index:D3}", $"Alias {index:D3}");
        private PropertyChangedEventHandler? _changed;
        public Detail Detail { get => _detail; set { _detail = value; _changed?.Invoke(this, new(nameof(Detail))); } }
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
    private sealed class Detail(string primary, string alias) : INotifyPropertyChanged
    {
        private string _primary = primary;
        private string _alias = alias;
        private bool? _alternateFlag;
        private PropertyChangedEventHandler? _changed;
        public string Primary { get => _primary; set { _primary = value; _changed?.Invoke(this, new(nameof(Primary))); } }
        public string Alias { get => _alias; set { _alias = value; _changed?.Invoke(this, new(nameof(Alias))); } }
        public bool? Flag => false;
        public bool? AlternateFlag { get => _alternateFlag; set { _alternateFlag = value; _changed?.Invoke(this, new(nameof(AlternateFlag))); } }
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
