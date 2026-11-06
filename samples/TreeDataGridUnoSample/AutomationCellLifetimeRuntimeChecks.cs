using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Automation.Peers;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Application callbacks during real native automation provider calls.</summary>
internal static class AutomationCellLifetimeRuntimeChecks
{
    internal static async Task RunAsync(global::Uno.Controls.TreeDataGrid grid)
    {
        var previousModel = grid.Model;
        var previousOptions = grid.PresentationOptions;
        var wasEnabled = grid.IsEnabled;
        var cases = 0;
        grid.Model = null;
        grid.IsEnabled = true;
        try
        {
            foreach (var replace in new[] { false, true })
            {
                await WithCell(false, "text permission retirement, replace=" + replace, c =>
                {
                    c.Value.BeforePermission = () => c.Retire(replace);
                    c.Peer.SetValue("obsolete action");
                    c.AssertNoWrites();
                });
                await WithCell(true, "toggle value retirement, replace=" + replace, c =>
                {
                    c.Value.BeforeValue = () => c.Retire(replace);
                    c.Peer.Toggle();
                    c.AssertNoWrites();
                });
            }
            await WithCell(false, "retired permission read", c =>
            {
                c.Value.BeforePermission = () => c.Retire(false);
                Check(c.Peer.IsReadOnly, "A retired permission getter returned writable.");
            });
            await WithCell(false, "retired display read", c =>
            {
                c.Value.BeforeDisplay = () => c.Retire(false);
                Check(c.Peer.Value == string.Empty, "A retired display getter published old text.");
            });
            await WithCell(true, "retired toggle-state read", c =>
            {
                c.Value.BeforeValue = () => c.Retire(false);
                Check(c.Peer.ToggleState == ToggleState.Indeterminate, "A retired toggle getter published an old state.");
            });
            await WithCell(false, "newer nested text assignment", c =>
            {
                c.Value.BeforePermission = () => c.Peer.SetValue("newer action");
                c.Peer.SetValue("obsolete action");
                Check(c.Original.Text == "newer action" && c.Original.Writes == 1,
                    "An older automation continuation overwrote a newer nested assignment.");
            });
            await WithCell(true, "newer nested toggle", c =>
            {
                c.Value.BeforeValue = c.Peer.Toggle;
                c.Peer.Toggle();
                Check(c.Original.Flag == true && c.Original.Writes == 1,
                    "An older toggle continuation duplicated a newer nested toggle.");
            });
            await WithCell(false, "silent permission change in kind metadata", c =>
            {
                c.Value.BeforeKind = () => c.Value.Writable = false;
                Check(Capture(() => c.Peer.SetValue("forbidden")) is InvalidOperationException,
                    "A custom kind getter's read-only change was ignored.");
                c.AssertNoWrites();
            });
            await WithCell(true, "silent permission change in value metadata", c =>
            {
                c.Value.BeforeValue = () => c.Value.Writable = false;
                Check(Capture(c.Peer.Toggle) is InvalidOperationException,
                    "A custom value getter's read-only change was ignored.");
                c.AssertNoWrites();
            });
            foreach (var checkBox in new[] { false, true })
            {
                await WithCell(checkBox, "disabled inside permission, checkbox=" + checkBox, c =>
                {
                    c.Value.BeforePermission = () => grid.IsEnabled = false;
                    Check(Capture(() => { if (checkBox) c.Peer.Toggle(); else c.Peer.SetValue("disabled"); }) is ElementNotEnabledException,
                        "An automation permission callback disabled the grid but its returning action wrote a value.");
                    c.AssertNoWrites();
                    grid.IsEnabled = true;
                });
            }
            for (var property = 0; property < 3; ++property)
            {
                var p = property;
                await WithCell(false, "getter exception identity and recovery, property=" + p, c =>
                {
                    var expected = new InvalidOperationException("Expected automation getter failure.");
                    Action fail = () => throw expected;
                    if (p == 0) c.Value.BeforePermission = fail;
                    else if (p == 1) c.Value.BeforeKind = fail;
                    else c.Value.BeforeDisplay = fail;
                    var error = Capture(() => { if (p == 2) _ = c.Peer.Value; else c.Peer.SetValue("failed"); });
                    Check(ReferenceEquals(expected, error), "The automation getter lost its original exception.");
                    c.AssertNoWrites();
                    c.Peer.SetValue("recovered");
                    Check(c.Original.Writes == 1 && c.Original.Text == "recovered", "Automation could not recover after a getter exception.");
                });
            }
            await WithCell(true, "healthy nullable toggle cycle", c =>
            {
                c.Peer.Toggle(); Check(c.Original.Flag == true, "False did not toggle to true.");
                c.Peer.Toggle(); Check(c.Original.Flag is null, "True did not toggle to indeterminate.");
                c.Peer.Toggle(); Check(c.Original.Flag == false, "Indeterminate did not toggle to false.");
                Check(c.Original.Writes == 3, "The normal toggle cycle duplicated writes.");
            });
            await WithCell(false, "retirement during peer notification", c =>
            {
                c.Value.BeforeDisplay = () => c.Retire(false);
                c.Value.Notify();
                Check(c.Peer.Value == string.Empty && c.Peer.IsReadOnly && c.Peer.GetPattern(PatternInterface.Value) is null,
                    "A returning peer notification kept a retired cell accessible.");
                c.AssertNoWrites();
            });
            for (var operation = 0; operation < 3; ++operation)
                await WithExpander(operation);
            Console.WriteLine($"UNO_RUNTIME_AUTOMATION_CELL_LIFETIME_PASSED: cases={cases}; captured native identities, permission/value/kind callbacks, row/source retirement, nested actions, disabled/read-only checks, exception recovery, notifications and Core expansion");
        }
        finally
        {
            grid.Model = null;
            grid.IsEnabled = wasEnabled;
            grid.PresentationOptions = previousOptions;
            grid.Model = previousModel;
        }

        async Task WithCell(bool checkBox, string name, Action<Context> test)
        {
            var items = new ObservableCollection<Item>(Enumerable.Range(0, 160).Select(i => new Item { Text = "Row " + i }));
            using var source = new FlatTreeDataGridSource<Item>(items);
            var definition = ValueColumn<Item, object?>.FromDelegate("Value", static x => x, width: new(240));
            definition.PresentationKey = "Automation.Probe";
            source.Columns.Add(definition);
            var values = new List<ProbeValue>();
            var options = new TreeDataGridPresentationOptions();
            options.Columns["Automation.Probe"] = column => new ProbeColumn(column, checkBox, values);
            grid.Model = null;
            try
            {
                grid.IsEnabled = true;
                grid.PresentationOptions = options;
                grid.Model = source;
                grid.Scroll!.ChangeView(0, 0, null, true);
                await Task.Delay(80); grid.UpdateLayout();
                var cell = grid.TryGetCell(0, 0) as TreeDataGridCell ?? throw new InvalidOperationException("No native automation test cell.");
                var value = cell.ViewModel as ProbeValue ?? throw new InvalidOperationException("The automation test did not use its actual custom value.");
                var peer = (TreeDataGridCellAutomationPeer)FrameworkElementAutomationPeer.CreatePeerForElement(cell);
                Check(peer.IsControlElement() && ReferenceEquals(cell.RowModel, items[0]), "The provider fixture was not a live Core-backed cell.");
                test(new(grid, items, cell, value, peer));
            }
            finally { grid.Model = null; grid.IsEnabled = true; }
            Check(source.Rows.Count == 160 && values.All(x => x.Disposals == 1),
                "Automation retirement leaked a value or disposed the caller's source.");
            ++cases;
            Console.WriteLine("UNO_AUTOMATION_CELL_CASE_PASSED: " + name);
        }

        async Task WithExpander(int operation)
        {
            var root = new Item(); root.Children.Add(new());
            using var source = new HierarchicalTreeDataGridSource<Item>([root]);
            source.Columns.Add(new HierarchicalExpanderColumn<Item>(
                ValueColumn<Item, string>.FromDelegate("Tree", static x => x.Text, width: new(180)), static x => x.Children));
            var definition = ValueColumn<Item, object?>.FromDelegate("Expansion", static x => x, width: new(180));
            definition.PresentationKey = "Automation.Expander";
            source.Columns.Add(definition);
            var options = new TreeDataGridPresentationOptions();
            options.Columns["Automation.Expander"] = column => new ExpanderColumn(column);
            grid.Model = null;
            ProbeExpander? value = null;
            try
            {
                grid.PresentationOptions = options;
                grid.Model = source;
                grid.Scroll!.ChangeView(0, 0, null, true);
                await Task.Delay(80); grid.UpdateLayout();
                var cell = (TreeDataGridCell)grid.TryGetCell(1, 0)!;
                value = (ProbeExpander)cell.ViewModel!;
                var peer = (TreeDataGridCellAutomationPeer)FrameworkElementAutomationPeer.CreatePeerForElement(cell);
                Check(peer.ExpandCollapseState == ExpandCollapseState.Collapsed, "The expander fixture has no expandable Core row.");
                if (operation == 2) value.BeforeExpanded = () => grid.Model = null;
                else value.BeforeShow = () => grid.Model = null;
                if (operation == 0) peer.Expand();
                else Check(peer.ExpandCollapseState == ExpandCollapseState.LeafNode,
                    "A retired expansion getter published a live state.");
                Check(value.ExpansionWrites == 0 && source.Rows.Count == 1,
                    "An obsolete automation expansion mutated its borrowed Core hierarchy.");
            }
            finally { grid.Model = null; }
            Check(value is { Disposals: 1 }, "The automation expander value was not released exactly once.");
            source.Expand(0);
            Check(source.Rows.Count == 2, "Automation cleanup retired the caller-owned Core hierarchy.");
            ++cases;
            Console.WriteLine("UNO_AUTOMATION_CELL_CASE_PASSED: expander callback retirement, operation=" + operation);
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static Exception? Capture(Action action) { try { action(); return null; } catch (Exception error) { return error; } }
    private static void InvokeOnce(ref Action? field) { var action = field; field = null; action?.Invoke(); }
    private sealed class Item
    {
        internal string Text = "Original";
        internal bool? Flag = false;
        internal int Writes;
        internal readonly ObservableCollection<Item> Children = new();
    }
    private sealed class Context(global::Uno.Controls.TreeDataGrid grid, ObservableCollection<Item> items,
        TreeDataGridCell cell, ProbeValue value, TreeDataGridCellAutomationPeer peer)
    {
        internal Item Original { get; } = items[0];
        internal Item Replacement { get; } = new() { Text = "Replacement" };
        internal ProbeValue Value => value;
        internal TreeDataGridCellAutomationPeer Peer => peer;
        internal void Retire(bool replace)
        {
            if (!replace) grid.Model = null;
            else
            {
                items[0] = Replacement;
                grid.UpdateLayout();
                Check(ReferenceEquals(grid.TryGetCell(0, 0), cell) && ReferenceEquals(cell.RowModel, Replacement),
                    "The replacement fixture did not retarget the same native container.");
            }
        }
        internal void AssertNoWrites() => Check(Original.Writes == 0 && Replacement.Writes == 0,
            "A returning automation action wrote an old or replacement model.");
    }
    private sealed class ProbeColumn(IColumn model, bool checkBox, List<ProbeValue> values) : CellColumn(model)
    {
        public override CellKind Kind => checkBox ? CellKind.CheckBox : CellKind.Text;
        public override bool IsThreeState => true;
        public override CellValue CreateCell(IRow row)
        {
            var result = new ProbeValue((Item)row.Model!, checkBox);
            values.Add(result);
            return result;
        }
    }
    private sealed class ProbeValue(Item item, bool checkBox) : CellValue
    {
        internal Action? BeforePermission, BeforeValue, BeforeDisplay, BeforeKind;
        internal bool Writable = true;
        internal int Disposals;
        public override CellKind ContentKind { get { var kind = base.ContentKind; InvokeOnce(ref BeforeKind); return kind; } }
        public override object? Value { get { object? value = checkBox ? item.Flag : item.Text; InvokeOnce(ref BeforeValue); return value; } }
        public override string? DisplayText { get { var text = checkBox ? null : item.Text; InvokeOnce(ref BeforeDisplay); return text; } }
        public override bool CanEdit => !checkBox && Writable;
        public override bool CanWrite { get { var allowed = Writable; InvokeOnce(ref BeforePermission); return allowed; } }
        public override bool? IsThreeState => true;
        public override void Write(object? value)
        {
            ++item.Writes;
            if (checkBox) item.Flag = (bool?)value;
            else item.Text = (string?)value ?? string.Empty;
            Notify();
        }
        internal void Notify() => RaisePropertyChanged(nameof(Value));
        public override void Dispose() => ++Disposals;
    }
    private sealed class ExpanderColumn(IColumn model) : CellColumn(model)
    {
        public override CellKind Kind => CellKind.Expander;
        public override CellValue CreateCell(IRow row) => new ProbeExpander((IExpanderRow<Item>)row);
    }
    private sealed class ProbeExpander(IExpanderRow<Item> row) : ExpanderCellValue
    {
        internal Action? BeforeShow, BeforeExpanded;
        internal int Disposals, ExpansionWrites;
        public override CellValue Inner { get; } = new ProbeValue(row.Model, false);
        public override IRow Row => row;
        public override object? Value => Inner.Value;
        public override bool CanEdit => false;
        public override bool CanWrite => false;
        public override void Write(object? value) => throw new NotSupportedException();
        public override bool ShowExpander { get { var show = row.ShowExpander; InvokeOnce(ref BeforeShow); return show; } }
        public override bool IsExpanded
        {
            get { var expanded = row.IsExpanded; InvokeOnce(ref BeforeExpanded); return expanded; }
            set { ++ExpansionWrites; row.IsExpanded = value; }
        }
        public override void Dispose() { ++Disposals; Inner.Dispose(); }
    }
}
