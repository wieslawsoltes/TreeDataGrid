using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using TreeDataGridCore;
using Uno.Controls;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Deferred native gates for declarative XAML, ownership and nested binding lifetimes.</summary>
internal static class DeclarativeRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        var grid = page.Grid;
        page.ShowScenario(9);
        await Task.Delay(200);
        Check(grid.Model is null && grid.ItemsSource is not null && grid.Presentation?.Model is HierarchicalTreeDataGridSource<object>,
            "Declarative People did not construct an actual Core hierarchy from XAML columns.");
        Check(grid.RowsPresenter!.RealizedCells.Any(cell => cell.ActualHeight > 0), "Declarative People did not render.");
        page.ShowScenario(0);
        var definitions = grid.ColumnDefinitions.ToArray();
        var previousOptions = grid.PresentationOptions;
        var root = new Node();
        root.State.Children.Add(new());
        var items = new ObservableCollection<Node> { root };
        grid.Model = null;
        grid.PresentationOptions = null;
        grid.ColumnDefinitions.Clear();
        grid.ColumnDefinitions.Add(new TreeDataGridRowHeaderColumn { Header = "#", Width = new(45) });
        grid.ColumnDefinitions.Add(new TreeDataGridHierarchicalExpanderColumn
        {
            ChildrenBinding = Bind("State.Children"), HasChildrenBinding = Bind("State.Children.Count"),
            IsExpandedBinding = Bind("State.Expanded", true),
            InnerColumn = new TreeDataGridTextColumn { Header = "Name", Width = new(280), Binding = Bind("State.Name", true) },
        });
        grid.ColumnDefinitions.Add(new TreeDataGridCheckBoxColumn
            { Header = "Nullable", Width = new(120), Binding = Bind("State.Checked", true) });
        grid.ColumnDefinitions.Add(new TreeDataGridTextColumn
            { Header = "Amount", Width = new(120), Binding = Bind("State.Amount", true), Culture = CultureInfo.GetCultureInfo("de-DE") });
        grid.ColumnDefinitions.Add(new TreeDataGridTextColumn
            { Header = "Indexer", Width = new(120), Binding = Bind("State.Numbers[0]", true) });
        try
        {
            grid.ItemsSource = items;
            await Task.Delay(150);
            var source = (HierarchicalTreeDataGridSource<object>)grid.Presentation!.Model;
            Check(ReferenceEquals(source.Rows[0].Model, root) && source.Rows.Count == 1, "Generated source copied row models or expanded unexpectedly.");
            Check(CellText(1) == "Name", "Nested declarative text did not render.");
            root.State.Name = "Nested update";
            await Task.Delay(50);
            Check(CellText(1) == "Nested update", "Nested INPC did not refresh the native cell binding.");
            Check(grid.BeginEdit(0, 1), "Two-way declarative text did not enter editing.");
            grid.EditingCell!.EditingText = "invalid";
            Check(!grid.CommitEdit() && grid.EditingCell!.HasValidationError, "A declarative setter failure did not preserve the edit for retry.");
            grid.EditingCell.EditingText = "Edited";
            Check(grid.CommitEdit() && root.State.Name == "Edited", "Declarative text retry did not write back.");
            var check = (TreeDataGridCell)grid.TryGetCell(2, 0)!;
            Check(check.Column!.IsThreeState, "Nullable boolean type inference was lost through a nested path.");
            check.ViewModel!.Write(true);
            Check(root.State.Checked == true, "Declarative checkbox did not write to its nested owner.");
            ((TreeDataGridCell)grid.TryGetCell(3, 0)!).ViewModel!.Write("12,5");
            Check(root.State.Amount == 12.5m, "Declarative writeback ignored the column's parsing culture.");
            ((TreeDataGridCell)grid.TryGetCell(4, 0)!).ViewModel!.Write("42");
            Check(root.State.Numbers[0] == 42, "Declarative typed indexer writeback did not convert its value.");

            root.State.Expanded = true;
            Check(source.Rows.Count == 2, "A nested expansion binding did not update Core without a root INPC event.");
            root.State.Children = new() { new(), new() };
            Check(source.Rows.Count == 3, "Replacing a nested child collection did not update the Core hierarchy.");
            var previous = root.State;
            root.State = new State();
            await Task.Delay(50);
            Check(source.Rows.Count == 1 && CellText(1) == "Name", "Replacing a nested owner left old expansion or cell state.");
            Check(previous.SubscriberCount == 0, "The previous nested owner retained binding subscriptions.");
            root.State = null!;
            await Task.Delay(50);
            Check(string.IsNullOrEmpty(CellText(1)), "A null intermediate owner kept stale cell text.");
            root.State = new State();
            root.State.Children.Add(new());
            root.State.Expanded = true;
            Check(source.Rows.Count == 2, "Nested bindings did not recover after a null intermediate owner.");

            // Model is borrowed; removing it restores, rather than disposes, the
            // generated source. Removing ItemsSource disposes only that source.
            using var explicitSource = new FlatTreeDataGridSource<Node>([new()]);
            explicitSource.Columns.Add(new TreeDataGridCore.Models.TextColumn<Node, string>("Explicit", _ => "explicit"));
            grid.Model = explicitSource;
            Check(ReferenceEquals(grid.Presentation?.Model, explicitSource), "Model did not take precedence over ItemsSource.");
            grid.Model = null;
            Check(ReferenceEquals(grid.Presentation?.Model, source), "Clearing Model did not restore the declarative source.");
            grid.ItemsSource = null;
            Check(grid.Presentation is null && root.SubscriberCount == 0 && root.State.SubscriberCount == 0,
                "Clearing ItemsSource did not release generated rows and native binding subscriptions.");
            Check(explicitSource.Rows.Count == 1, "A caller-owned Model was disposed by the declarative controller.");
            Console.WriteLine("UNO_RUNTIME_DECLARATIVE_PASSED: XAML People, Core hierarchy, nested text/checkbox/edit retry, expansion, collection/owner replacement, null recovery, Model precedence, subscription cleanup");
        }
        finally
        {
            grid.Model = null;
            grid.ItemsSource = null;
            grid.ColumnDefinitions.Clear();
            foreach (var definition in definitions) grid.ColumnDefinitions.Add(definition);
            grid.PresentationOptions = previousOptions;
            page.ShowScenario(0);
        }
        string? CellText(int column) => grid.TryGetCell(column, 0) is { } cell
            ? ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().FirstOrDefault()?.Text : null;
    }
    private static Binding Bind(string path, bool twoWay = false) => new()
        { Path = new PropertyPath(path), Mode = twoWay ? BindingMode.TwoWay : BindingMode.OneWay };
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private abstract class Observable : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        internal int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
        protected void Notify(string name) => _changed?.Invoke(this, new(name));
    }
    private sealed class Node : Observable
    {
        private State _state = new();
        public State State { get => _state; set { _state = value; Notify(nameof(State)); } }
    }
    private sealed class State : Observable
    {
        private string _name = "Name";
        private bool _expanded;
        private bool? _checked;
        private ObservableCollection<Node> _children = new();
        public decimal Amount { get; set; }
        public List<int> Numbers { get; } = [1];
        public string Name
        {
            get => _name;
            set { if (value == "invalid") throw new ArgumentException("Rejected name."); _name = value; Notify(nameof(Name)); }
        }
        public bool Expanded { get => _expanded; set { if (_expanded == value) return; _expanded = value; Notify(nameof(Expanded)); } }
        public bool? Checked { get => _checked; set { _checked = value; Notify(nameof(Checked)); } }
        public ObservableCollection<Node> Children { get => _children; set { _children = value; Notify(nameof(Children)); } }
    }
}
