using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls;
using Uno.Controls.Automation.Peers;

namespace TreeDataGridUnoSample;

/// <summary>Native peer/provider gates, authored for the post-implementation validation pass.</summary>
internal static class AutomationRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        var previousModel = grid.Model;
        var previousOptions = grid.PresentationOptions;
        var previousMode = grid.SelectionMode;
        var root = new Item("Parent");
        root.Children.Add(new("Child"));
        var other = new Item("Other");
        using var source = new HierarchicalTreeDataGridSource<Item>([root, other]);
        source.Columns.Add(new HierarchicalExpanderColumn<Item>(
            new TextColumn<Item, string>("Name", x => x.Name, (x, value) => x.Name = value, width: new(240)),
            x => x.Children));
        source.Columns.Add(new CheckBoxColumn<Item>("Checked", x => x.Checked, (x, value) => x.Checked = value, width: new(140)));
        grid.Model = null;
        grid.PresentationOptions = null;
        grid.SelectionMode = TreeDataGridSelectionMode.MultipleRows;
        try
        {
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(150);
            Check(grid.TryGetCell(0, 0) is global::Uno.Controls.Primitives.TreeDataGridExpanderCell { Indent: 0, IsExpanded: false, ShowExpander: true },
                "The default factory did not expose the expander control's scalar contract.");
            var gridPeer = (TreeDataGridAutomationPeer)FrameworkElementAutomationPeer.CreatePeerForElement(grid);
            Check(gridPeer.GetAutomationControlType() == AutomationControlType.DataGrid, "Grid automation role differs from Avalonia.");
            var selection = (ISelectionProvider)gridPeer.GetPattern(PatternInterface.Selection);
            Check(selection.CanSelectMultiple && !selection.IsSelectionRequired, "Row selection capabilities are incorrect.");
            var rowPeer = RowPeer(0);
            Check(rowPeer.GetAutomationControlType() == AutomationControlType.TreeItem && rowPeer.IsReadOnly, "Row automation contract is incorrect.");
            rowPeer.Select();
            Check(source.RowSelection!.SelectedIndex == new IndexPath(0) && rowPeer.IsSelected && selection.GetSelection().Length == 1,
                "Automation selection did not update actual Core row selection.");
            RowPeer(1).AddToSelection();
            Check(source.RowSelection.Count == 2 && selection.GetSelection().Length == 2, "AddToSelection lost an existing selection.");
            rowPeer.RemoveFromSelection();
            Check(!rowPeer.IsSelected && source.RowSelection.Count == 1, "RemoveFromSelection did not remove only its row.");
            rowPeer.Expand();
            await Task.Delay(100);
            rowPeer = RowPeer(0);
            Check(source.Rows.Count == 3 && rowPeer.ExpandCollapseState == ExpandCollapseState.Expanded && rowPeer.ToggleState == ToggleState.On,
                "Row expansion did not update shared Core rows.");
            Check(RowPeer(1).GetPattern(PatternInterface.ExpandCollapse) is null, "Leaf rows expose an expansion pattern.");
            Check(grid.TryGetCell(0, 0) is global::Uno.Controls.Primitives.TreeDataGridExpanderCell { IsExpanded: true } &&
                grid.TryGetCell(0, 1) is global::Uno.Controls.Primitives.TreeDataGridExpanderCell { Indent: 1, ShowExpander: false },
                "Core expansion did not update public expander properties/child indentation.");
            var expanderControl = (global::Uno.Controls.Primitives.TreeDataGridExpanderCell)grid.TryGetCell(0, 0)!;
            expanderControl.IsExpanded = false;
            Check(source.Rows.Count == 2 && !expanderControl.IsExpanded, "Public IsExpanded did not collapse the shared Core row.");
            expanderControl.IsExpanded = true;
            grid.UpdateLayout();
            Check(source.Rows.Count == 3 && expanderControl.IsExpanded, "Public IsExpanded did not expand the shared Core row.");
            var text = (IValueProvider)CellPeer(0).GetPattern(PatternInterface.Value);
            text.SetValue("Changed by automation");
            Check(root.Name == "Changed by automation" && text.Value == root.Name, "Text automation value did not use the live model.");
            var checkbox = (IToggleProvider)CellPeer(1).GetPattern(PatternInterface.Toggle);
            checkbox.Toggle();
            Check(root.Checked == true && checkbox.ToggleState == ToggleState.On, "Checkbox automation did not write true.");
            checkbox.Toggle();
            Check(root.Checked is null && checkbox.ToggleState == ToggleState.Indeterminate, "Nullable checkbox lost its third state.");
            grid.IsEnabled = false;
            Throws<ElementNotEnabledException>(() => rowPeer.Select(), "Disabled row accepted an automation action.");
            Throws<ElementNotEnabledException>(() => checkbox.Toggle(), "Disabled cell accepted an automation action.");
            grid.IsEnabled = true;

            grid.SelectionMode = TreeDataGridSelectionMode.MultipleCells;
            Check(gridPeer.GetPattern(PatternInterface.Selection) is null && rowPeer.GetPattern(PatternInterface.SelectionItem) is null,
                "Row automation misrepresented rectangular cell selection.");
            grid.SelectionMode = TreeDataGridSelectionMode.MultipleRows;
            var headers = FrameworkElementAutomationPeer.CreatePeerForElement(grid.ColumnHeadersPresenter!);
            Check(headers.GetAutomationControlType() == AutomationControlType.Header && !headers.IsContentElement(), "Header presenter role is incorrect.");
            Check(headers.GetChildren().All(x => x.GetAutomationControlType() == AutomationControlType.HeaderItem), "Header children expose pooled/non-header controls.");
            var children = rowPeer.GetChildren().Cast<TreeDataGridCellAutomationPeer>().ToArray();
            Check(children.Select(x => x.Owner.ColumnIndex).SequenceEqual(children.Select(x => x.Owner.ColumnIndex).OrderBy(x => x)),
                "Accessible row cells are not in column order.");

            // A user callback can retire a container before the provider mutates
            // selection. The provider must not act on the old or replacement row.
            source.RowSelection!.Clear();
            void Replace(object? sender, CancelEventArgs args) => grid.Model = null;
            grid.SelectionChanging += Replace;
            try { rowPeer.Select(); }
            finally { grid.SelectionChanging -= Replace; }
            Check(source.RowSelection.Count == 0 && grid.Presentation is null, "A stale row provider mutated selection after source replacement.");
            Check(!rowPeer.IsControlElement() && rowPeer.GetChildren() is null && rowPeer.GetPattern(PatternInterface.Value) is null,
                "Retired row remained accessible.");
            Throws<InvalidOperationException>(() => rowPeer.Expand(), "Retired row provider accepted expansion.");
            Throws<InvalidOperationException>(() => text.SetValue("stale"), "Retired cell provider wrote its previous model.");
            Check(selection.GetSelection().Length == 0 && gridPeer.GetPattern(PatternInterface.Selection) is null,
                "Grid peer retained the previous source selection.");
            Console.WriteLine("UNO_RUNTIME_AUTOMATION_PASSED: roles, shared row selection, expansion, text/nullable checkbox values, disabled actions, ordered children, cell-selection exclusion, stale providers and reentrant replacement");
        }
        finally
        {
            grid.IsEnabled = true;
            grid.Model = null;
            grid.PresentationOptions = previousOptions;
            grid.SelectionMode = previousMode;
            grid.Model = previousModel;
        }
        TreeDataGridRowAutomationPeer RowPeer(int row) => (TreeDataGridRowAutomationPeer)
            FrameworkElementAutomationPeer.CreatePeerForElement(grid.TryGetRow(row)!);
        TreeDataGridCellAutomationPeer CellPeer(int column) => (TreeDataGridCellAutomationPeer)
            FrameworkElementAutomationPeer.CreatePeerForElement(grid.TryGetCell(column, 0)!);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException(message);
    }
    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        private bool? _checked = false;
        public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public bool? Checked { get => _checked; set { _checked = value; PropertyChanged?.Invoke(this, new(nameof(Checked))); } }
        public ObservableCollection<Item> Children { get; } = new();
        public event PropertyChangedEventHandler? PropertyChanged;
        public override string ToString() => Name;
    }
}
