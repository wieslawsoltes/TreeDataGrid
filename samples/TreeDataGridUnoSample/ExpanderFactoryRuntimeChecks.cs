using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Deferred native inner-factory, lifetime and editing regressions.</summary>
internal static class ExpanderFactoryRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid, DataTemplate display, DataTemplate editing)
    {
        grid.Model = null;
        var previousFactory = grid.ElementFactory;
        var previousOptions = grid.PresentationOptions;
        grid.PresentationOptions = null;
        grid.ElementFactory = new Factory();
        grid.CellTemplates["InnerCustom"] = display;
        grid.CellEditingTemplates["InnerCustom"] = editing;
        try
        {
            foreach (var kind in new[] { CellKind.Text, CellKind.CheckBox, CellKind.Template })
            {
                var root = new Item("Original");
                root.Children.Add(new("Child"));
                var items = new ObservableCollection<Item> { root };
                using var source = new HierarchicalTreeDataGridSource<Item>(items);
                IColumn<Item> column = kind switch
                {
                    CellKind.CheckBox => new CheckBoxColumn<Item>("Inner", x => x.Checked, (x, v) => x.Checked = v, width: new(300)),
                    CellKind.Template => new TemplateColumn<Item>("Inner", "InnerCustom", width: new(300)),
                    _ => new TextColumn<Item, string>("Inner", x => x.Name, (x, v) => x.Name = v, width: new(300)),
                };
                source.Columns.Add(new HierarchicalExpanderColumn<Item>(column, x => x.Children));
                grid.Model = source;
                grid.Scroll!.ChangeView(0, 0, null, true);
                await Task.Delay(150);
                grid.UpdateLayout();
                var outer = (TreeDataGridExpanderCell)grid.TryGetCell(0, 0)!;
                var inner = Inner(outer);
                Check(kind switch
                {
                    CellKind.CheckBox => inner is CustomCheck,
                    CellKind.Template => inner is CustomTemplate,
                    _ => inner is CustomText,
                }, "The expander bypassed its custom inner-cell factory.");
                Check(outer.Model is UI.IExpanderCellPresentation contract && ReferenceEquals(contract.Content, inner.Model) &&
                    ReferenceEquals(contract.Row, source.Rows[0]), "The expander presentation contract copied or lost its Core row/inner cell.");
                Check(grid.TryGetCell(inner, out var found) && ReferenceEquals(found, outer),
                    "Visual lookup did not resolve an inner control to its public outer cell.");
                var parent = inner.Parent;
                var unloads = 0;
                inner.Unloaded += (_, _) => ++unloads;
                var valueEvents = 0;
                void ValueChanged(object? sender, TreeDataGridCellEventArgs args)
                {
                    Check(ReferenceEquals(args.Cell, outer), "An inner control published an unregistered public cell event.");
                    ++valueEvents;
                }
                grid.CellValueChanged += ValueChanged;
                try
                {
                    if (kind == CellKind.CheckBox)
                    {
                        ((CustomCheck)inner).Value = true;
                        Check(root.Checked == true && valueEvents == 1, "Inner checkbox writeback published missing/duplicate value events.");
                    }
                    else
                    {
                        Check(grid.BeginEdit(0, 0) && outer.IsEditing && ReferenceEquals(grid.EditingCell, inner) &&
                            (bool)outer.GetValue(TreeDataGridCell.IsEditingProperty), "Editing did not target/synchronize the factory-created inner control.");
                        if (kind == CellKind.Text) inner.EditingText = "Edited";
                        else ShowcaseRuntimeChecks.Descendants(inner).OfType<TextBox>().Single().Text = "Edited";
                        Check(valueEvents == 0, "An inner editor published a committed value event before commit.");
                        Check(grid.CommitEdit() && root.Name == "Edited" && !outer.IsEditing && valueEvents == 1,
                            "Inner edit commit lost model state or published duplicate value events.");
                        Check(grid.BeginEdit(0, 0), "The inner edit did not restart.");
                        if (kind == CellKind.Text) inner.EditingText = "Cancelled";
                        else ShowcaseRuntimeChecks.Descendants(inner).OfType<TextBox>().Single().Text = "Cancelled";
                        grid.CancelEdit();
                        Check(root.Name == "Edited" && !outer.IsEditing && valueEvents == 1, "Inner cancellation changed committed state.");
                    }
                }
                finally { grid.CellValueChanged -= ValueChanged; }

                items[0] = new Item("Replacement");
                grid.UpdateLayout();
                await Task.Delay(50);
                Check(ReferenceEquals(outer, grid.TryGetCell(0, 0)) && ReferenceEquals(inner, Inner(outer)) &&
                    ReferenceEquals(parent, inner.Parent) && unloads == 0, "Compatible inner recycling detached or replaced the retained control.");
                Check(ReferenceEquals(inner.RowModel, items[0]), "A recycled inner cell retained its old row identity.");
                if (kind == CellKind.Text)
                {
                    items[0] = new Item("Alternate");
                    grid.UpdateLayout();
                    await Task.Delay(50);
                    Check(ReferenceEquals(outer, grid.TryGetCell(0, 0)) && Inner(outer) is AlternateText && inner.Parent is null,
                        "An incompatible custom inner key was reused or retained its old parent.");
                }
                var localRow = grid.TryGetRow(0)!;
                var innerFactoryCalls = 0;
                localRow.ElementFactory = new Factory { CreatingInner = () => ++innerFactoryCalls };
                grid.UpdateLayout();
                await Task.Delay(50);
                outer = (TreeDataGridExpanderCell)grid.TryGetCell(0, 0)!;
                Check(innerFactoryCalls > 0 && ReferenceEquals(localRow, grid.TryGetRow(0)),
                    "The row-level factory was not propagated to the expander's inner control.");
                var finalInner = Inner(outer);
                grid.Model = null;
                Check(outer.Model is null && finalInner.Model is null && finalInner.RowModel is null && !finalInner.IsEditing,
                    "Source removal retained the inner model or editor.");
            }
            await RunCustomModelAsync(grid, display);
            using var reentrantSource = new HierarchicalTreeDataGridSource<Item>([new("Reentrant")]);
            reentrantSource.Columns.Add(new HierarchicalExpanderColumn<Item>(new TextColumn<Item, string>("Name", x => x.Name), x => x.Children));
            grid.ElementFactory = new Factory { CreatingInner = () => grid.Model = null };
            grid.Model = reentrantSource;
            grid.UpdateLayout();
            await Task.Delay(50);
            Check(grid.Model is null && grid.RowsPresenter!.RealizedCells.Count == 0,
                "A source change inside the inner factory left a stale realization.");
            Console.WriteLine("UNO_RUNTIME_EXPANDER_FACTORY_PASSED: custom text/checkbox/template inner controls, Core contract, lookup, edit commit/cancel/events, retained parent, incompatible key, source cleanup/reentrancy");
        }
        finally
        {
            grid.Model = null;
            grid.ElementFactory = previousFactory;
            grid.PresentationOptions = previousOptions;
            grid.CellTemplates.Remove("InnerCustom");
            grid.CellEditingTemplates.Remove("InnerCustom");
        }
    }
    private static TreeDataGridCell Inner(TreeDataGridExpanderCell outer) =>
        ShowcaseRuntimeChecks.Descendants(outer).OfType<TreeDataGridCell>().First();
    private static async Task RunCustomModelAsync(Uno.Controls.TreeDataGrid grid, DataTemplate display)
    {
        var item = new Item("Template content");
        using var source = new FlatTreeDataGridSource<Item>([item]);
        source.Columns.Add(new TextColumn<Item, string>("Custom", x => x.Name, width: new(300)) { PresentationKey = "CustomExpander" });
        ExternalExpander? supplied = null;
        var options = new TreeDataGridPresentationOptions<Item>();
        options.Columns["CustomExpander"] = _ => new ExternalColumn(row => supplied = new(row, new UI.TextCell<string>("First")));
        grid.PresentationOptions = options;
        try
        {
            grid.Model = source;
            grid.UpdateLayout();
            await Task.Delay(100);
            var outer = (TreeDataGridExpanderCell)grid.TryGetCell(0, 0)!;
            var first = (CustomText)Inner(outer);
            var parent = first.Parent;
            var unloads = 0;
            first.Unloaded += (_, _) => ++unloads;
            Check(ReferenceEquals(outer.Model, supplied), "Custom expander identity was hidden from the public cell model.");
            var changes = 0;
            void Changed(object? sender, TreeDataGridCellEventArgs args)
            {
                Check(ReferenceEquals(args.Cell, outer), "Content replacement reported an unregistered inner control.");
                ++changes;
            }
            grid.CellValueChanged += Changed;
            try
            {
                supplied!.Replace(new UI.TextCell<string>("Second"));
                grid.UpdateLayout();
                Check(ReferenceEquals(first, Inner(outer)) && first.Value == "Second" && ReferenceEquals(first.Parent, parent) && unloads == 0,
                    "Same-kind custom content replacement did not retain and rebind its control.");
                Check(changes == 1, "Same-kind content replacement did not publish one value event.");
                supplied.Replace(new UI.CheckBoxCell(true));
                grid.UpdateLayout();
                Check(Inner(outer) is CustomCheck { Value: true, IsReadOnly: true } && first.Parent is null,
                    "Changed custom content kind did not switch to the checkbox control.");
                Check(changes == 2, "Changed-kind content replacement published missing/duplicate value events.");
                supplied.Replace(new UI.TemplateCell(item, _ => display, null, null));
                grid.UpdateLayout();
                Check(Inner(outer) is CustomTemplate template && ReferenceEquals(template.Content, item),
                    "Custom template content did not resolve against its actual inner control.");
                Check(changes == 3, "Template replacement published missing/duplicate value events.");
                supplied.Replace(null);
                grid.UpdateLayout();
                Check(!ShowcaseRuntimeChecks.Descendants(outer).OfType<TreeDataGridCell>().Any() && changes == 4,
                    "Empty custom content retained its old child or lost its value event.");
                supplied.Replace(new UI.TextCell<string>("Restored"));
                grid.UpdateLayout();
                Check(Inner(outer) is CustomText { Value: "Restored" } && changes == 5,
                    "Custom expander content could not be restored after clearing.");
            }
            finally { grid.CellValueChanged -= Changed; }
            grid.Model = null;
            Check(supplied!.Disposals == 1, "The presentation did not dispose its custom expander exactly once.");
        }
        finally { grid.Model = null; grid.PresentationOptions = null; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed partial class CustomText : TreeDataGridTextCell { }
    private sealed partial class AlternateText : TreeDataGridTextCell { }
    private sealed partial class CustomCheck : TreeDataGridCheckBoxCell { }
    private sealed partial class CustomTemplate : TreeDataGridTemplateCell { }
    private sealed class Factory : TreeDataGridElementFactory
    {
        public Action? CreatingInner { get; init; }
        protected override Control CreateElement(object? data)
        {
            if (data is CellValue and not UI.IExpanderCellPresentation) CreatingInner?.Invoke();
            return data switch
            {
                UI.IExpanderCellPresentation => base.CreateElement(data),
                UI.CheckBoxCell => new CustomCheck(),
                UI.TemplateCell => new CustomTemplate(),
                CellValue { Kind: CellKind.CheckBox } => new CustomCheck(),
                CellValue { Kind: CellKind.Template } => new CustomTemplate(),
                CellValue { Value: "Alternate" } => new AlternateText(),
                UI.ITextCell => new CustomText(),
                CellValue => new CustomText(),
                _ => base.CreateElement(data),
            };
        }
        protected override string GetDataRecycleKey(object? data) => data switch
        {
            UI.IExpanderCellPresentation => base.GetDataRecycleKey(data),
            UI.CheckBoxCell => typeof(CustomCheck).FullName!,
            UI.TemplateCell => typeof(CustomTemplate).FullName!,
            CellValue { Kind: CellKind.CheckBox } => typeof(CustomCheck).FullName!,
            CellValue { Kind: CellKind.Template } => typeof(CustomTemplate).FullName!,
            CellValue { Value: "Alternate" } => typeof(AlternateText).FullName!,
            UI.ITextCell => typeof(CustomText).FullName!,
            CellValue => typeof(CustomText).FullName!,
            _ => base.GetDataRecycleKey(data),
        };
    }
    private sealed class ExternalColumn(Func<IRow<Item>, UI.ICell> create) : NotifyingBase, ICellColumn<Item>
    {
        public double ActualWidth => 300;
        public bool? CanUserResize => true;
        public object? Header => "Custom";
        public Microsoft.UI.Xaml.GridLength Width { get; private set; } = new(300);
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public double CellMeasured(double width, int rowIndex) => width;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(Microsoft.UI.Xaml.GridLength width) => Width = width;
        public UI.ICell CreateCell(IRow<Item> row) => create(row);
    }
    private sealed class ExternalExpander(IRow row, object? content) : NotifyingBase, UI.IExpanderCellPresentation, IDisposable
    {
        private object? _content = content;
        private bool _expanded;
        public IRow Row => row;
        public object? Content => _content;
        public object? Value => (_content as UI.ICell)?.Value;
        public bool CanEdit => (_content as UI.ICell)?.CanEdit == true;
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.Default;
        public bool IsExpanded { get => _expanded; set => RaiseAndSetIfChanged(ref _expanded, value); }
        public bool ShowExpander => true;
        public int Disposals { get; private set; }
        public void Replace(object? value)
        {
            var previous = _content;
            _content = value;
            try { RaisePropertyChanged(nameof(Content)); }
            finally { (previous as IDisposable)?.Dispose(); }
        }
        public void Dispose() { ++Disposals; (_content as IDisposable)?.Dispose(); }
    }
    private sealed class Item(string name) : INotifyPropertyChanged, IEditableObject
    {
        private string _name = name;
        private string? _snapshot;
        private bool? _checked = false;
        public string Name { get => _name; set { if (_name != value) { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } } }
        public bool? Checked { get => _checked; set { if (_checked != value) { _checked = value; PropertyChanged?.Invoke(this, new(nameof(Checked))); } } }
        public ObservableCollection<Item> Children { get; } = new();
        public event PropertyChangedEventHandler? PropertyChanged;
        public void BeginEdit() => _snapshot = Name;
        public void EndEdit() => _snapshot = null;
        public void CancelEdit() { if (_snapshot is { } name) { _snapshot = null; Name = name; } }
    }
}
