using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Checks real native controls in the running desktop head, not UI stubs.</summary>
internal static class RuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid, DataTemplate template)
    {
        grid.Model = null;
        var controls = new List<TrackedCell>();
        grid.CellFactory = _ => { var cell = new TrackedCell(); controls.Add(cell); return cell; };
        grid.CellTemplates["Runtime"] = template;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 200).Select(i => new Item($"Row {i:000}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TemplateColumn<Item>("Name", "Runtime", width: new(220), options: new()
        {
            CompareAscending = (x, y) => string.CompareOrdinal(x?.Name, y?.Name),
            CompareDescending = (x, y) => string.CompareOrdinal(y?.Name, x?.Name),
        }));
        grid.Model = source;
        grid.Scroll.ChangeView(0, 0, null, true);
        await Task.Delay(300);
        var first = (TrackedCell)grid.RowsPresenter.RealizedCells.Single(x => x.RowIndex == 0);
        var parent = VisualTreeHelper.GetParent(first);
        var text = Descendants(first).OfType<TextBlock>().Single(x => x.Text == "Row 000");
        var templateParent = VisualTreeHelper.GetParent(text);
        var loads = first.Loads;
        var unloads = first.Unloads;
        Check(first.Begins == 0 && first.Ends == 0, "Fresh realization called replacement hooks.");

        items[0] = new Item("Replacement");
        await Task.Delay(100);
        Check(ReferenceEquals(first, grid.RowsPresenter.RealizedCells.Single(x => x.RowIndex == 0)), "Replacement discarded its native cell.");
        Check(first.Begins == 1 && first.Ends == 1 && first.LastSucceeded, "Replacement hooks are unbalanced.");
        Check(ReferenceEquals(parent, VisualTreeHelper.GetParent(first)), "Replacement changed the native parent.");
        Check(ReferenceEquals(templateParent, VisualTreeHelper.GetParent(text)) && text.Text == "Replacement", "Replacement recreated or failed to update template content.");
        Check(first.Loads == loads && first.Unloads == unloads, "Replacement detached and reattached the cell.");

        // A sort resets the row projection. Sort-direction notifications must not
        // independently reset the presenter and discard every retained control.
        Check(source.SortBy(source.Columns[0], ListSortDirection.Descending), "Template sorting was not enabled.");
        await Task.Delay(100);
        Check(ReferenceEquals(parent, VisualTreeHelper.GetParent(first)), "Sort detached the native cell.");
        Check(first.Loads == loads && first.Unloads == unloads, "Sort unloaded a retained cell.");
        Check(((Item)source.Rows[0].Model!).Name == "Row 199", "Sort did not reorder the Core rows.");
        Check(text.Text == "Row 199", "Sort did not refresh retained template content.");
        foreach (var cell in grid.RowsPresenter.RealizedCells)
            Check(ReferenceEquals(cell.RowModel, source.Rows[cell.RowIndex].Model), "Sort retained an obsolete model.");

        var beforeScroll = controls.Count;
        grid.Scroll.ChangeView(null, 1500, null, true);
        await Task.Delay(200);
        Check(grid.RowsPresenter.RealizedCells.All(x => x.RowIndex > 0), "Scroll did not advance the viewport.");
        Check(controls.Count <= beforeScroll + 4, "Scroll allocated a second viewport of native controls.");
        Check(first.Unloads == unloads && ReferenceEquals(parent, VisualTreeHelper.GetParent(first)), "Scrolling detached a pooled control.");
        Check(ReferenceEquals(templateParent, VisualTreeHelper.GetParent(text)), "Scrolling discarded the retained template.");

        // Incremental changes retain unaffected rows and update their display indices.
        // Use an unsorted replacement source so insertion has a known display position.
        grid.Model = null;
        using var unsorted = new FlatTreeDataGridSource<Item>(items);
        unsorted.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name));
        grid.Model = unsorted;
        grid.Scroll.ChangeView(0, 0, null, true);
        await Task.Delay(200);
        var retained = grid.RowsPresenter.RealizedCells.First(x => x.RowIndex == 2);
        var retainedRow = retained.RowModel;
        var oldIndex = retained.RowIndex;
        items.Insert(0, new Item("Inserted"));
        Check(retained.RowIndex == oldIndex + 1 && ReferenceEquals(retained.RowModel, retainedRow), "Insertion failed to preserve shifted model identity.");
        items.RemoveAt(0);
        Check(retained.RowIndex == oldIndex && ReferenceEquals(retained.RowModel, retainedRow), "Removal failed to preserve shifted model identity.");
        unsorted.Columns.Add(new TextColumn<Item, string>("Other", x => x.Name, width: new(180)));
        await Task.Delay(100);
        var retainedParent = VisualTreeHelper.GetParent(retained);
        unsorted.Columns.Move(0, 1);
        Check(retained.ColumnIndex == 1 && ReferenceEquals(retainedParent, VisualTreeHelper.GetParent(retained)), "Column reorder detached a retained cell.");
        unsorted.Columns[1].Width = new(260);
        await Task.Delay(100);
        Check(retained.ActualWidth == 260 && ReferenceEquals(retainedParent, VisualTreeHelper.GetParent(retained)), "Column resize discarded its retained cell or width.");
        grid.Model = null;
        await Task.Delay(100);
        Check(grid.RowsPresenter.RealizedCells.Count == 0, "Source removal retained realized cells.");
        using var wide = new FlatTreeDataGridSource<Item>(items.Take(4).ToArray());
        for (var i = 0; i < 1000; ++i)
            wide.Columns.Add(new TextColumn<Item, string>($"Column {i}", x => x.Name, width: new(100)));
        grid.Model = wide;
        grid.Scroll.ChangeView(0, 0, null, true);
        await Task.Delay(200);
        var columnBudget = (int)Math.Ceiling(grid.Scroll.ViewportWidth / 100) + 1;
        Check(grid.ColumnHeadersPresenter.RealizedCount <= columnBudget, "Headers are not horizontally virtualized.");
        Check(grid.RowsPresenter.RealizedCells.Count <= 4 * columnBudget, "Cells are not horizontally virtualized.");
        grid.Scroll.ChangeView(90000, null, null, true);
        await Task.Delay(200);
        Check(grid.RowsPresenter.RealizedCells.Count > 0 && grid.RowsPresenter.RealizedCells.All(x => x.ColumnIndex >= 899), "Wide-grid scroll failed to advance column realization.");
        Check(grid.ColumnHeadersPresenter.RealizedCount <= columnBudget, "Scrolling grew the header realization window.");
        foreach (var cell in grid.RowsPresenter.RealizedCells)
            Check(cell.ActualWidth == 100, "Recycled wide-grid cell has incorrect geometry.");
        grid.Model = null;
        Console.WriteLine("UNO_RUNTIME_RECYCLING_PASSED: replacement, template identity, parent retention, sort, scrolling, index shifts, column reorder/resize, source removal, 1000-column virtualization");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); ++i)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed partial class TrackedCell : TreeDataGridCell
    {
        public TrackedCell() { Loaded += (_, _) => ++Loads; Unloaded += (_, _) => ++Unloads; }
        public int Loads { get; private set; }
        public int Unloads { get; private set; }
        public int Begins { get; private set; }
        public int Ends { get; private set; }
        public bool LastSucceeded { get; private set; }
        public override void BeginRebind() { ++Begins; base.BeginRebind(); }
        public override void EndRebind(bool realized) { ++Ends; LastSucceeded = realized; base.EndRebind(realized); }
    }
}
