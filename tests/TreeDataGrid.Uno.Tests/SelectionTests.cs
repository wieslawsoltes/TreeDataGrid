using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class SelectionTests
{
    [Fact]
    public void Default_selection_uses_exact_Core_model()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        Assert.Same(source.Selection, view.Selection.Model);
        Assert.True(view.Selection.Select(1, 0));
        Assert.Equal(new IndexPath(1), source.RowSelection!.SelectedIndex);
        Assert.True(view.Selection.IsSelected(1, 1));
        Assert.False(view.Selection.IsSelected(0, 0));
    }
    [Fact]
    public void Sorted_row_range_selects_displayed_models_and_preserves_range_anchor()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        view.Selection.Configure(TreeDataGridSelectionMode.MultipleRows);
        source.SortBy(source.Columns[0], ListSortDirection.Ascending);
        view.Selection.Select(0, 0);
        view.Selection.Select(2, 0, extend: true);
        Assert.Equal(new[] { "a", "b", "c" }, source.RowSelection!.SelectedItems.Select(x => x!.Name).OrderBy(x => x));
        view.Selection.Select(1, 0, extend: true);
        Assert.Equal(new[] { "a", "b" }, source.RowSelection.SelectedItems.Select(x => x!.Name).OrderBy(x => x));
        Assert.Equal((1, 0), view.Selection.GetAnchor(range: true));
    }
    [Fact]
    public void Toggle_and_right_button_preservation_keep_multiple_rows()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        view.Selection.Configure(TreeDataGridSelectionMode.MultipleRows);
        view.Selection.Select(0, 0);
        view.Selection.Select(2, 0, toggle: true);
        view.Selection.Select(0, 0, preserve: true);
        Assert.Equal(2, source.RowSelection!.Count);
        view.Selection.Select(0, 0, toggle: true);
        Assert.Single(source.RowSelection.SelectedIndexes);
        Assert.True(view.Selection.IsSelected(2, 0));
    }
    [Fact]
    public void Cell_selection_maps_hidden_columns_and_sorted_rows()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        view.Selection.Configure(TreeDataGridSelectionMode.MultipleCells);
        source.SortBy(source.Columns[0], ListSortDirection.Ascending);
        view.Selection.Select(0, 1);
        var selection = Assert.IsType<TreeDataGridCellSelectionModel<Item>>(source.Selection);
        Assert.Equal(new CellIndex(2, new IndexPath(1)), selection.SelectedIndex);
        Assert.True(view.Selection.IsSelected(0, 1));
        Assert.False(view.Selection.IsSelected(0, 0));
        source.Columns.Move(2, 0);
        Assert.Equal(0, selection.SelectedIndex.ColumnIndex);
        Assert.True(view.Selection.IsSelected(0, 0));
    }
    [Fact]
    public void Rectangular_cell_range_includes_hidden_source_columns_like_Core()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        view.Selection.Configure(TreeDataGridSelectionMode.MultipleCells);
        view.Selection.Select(0, 0);
        view.Selection.Select(1, 1, extend: true);
        var selection = Assert.IsType<TreeDataGridCellSelectionModel<Item>>(source.Selection);
        Assert.Equal(6, selection.Count);
        Assert.True(selection.IsSelected(new(1, new IndexPath(1))));
        Assert.Equal((1, 1), view.Selection.GetAnchor(range: true));
    }
    [Fact]
    public void External_selection_replacement_detaches_previous_notifications()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        var previous = source.RowSelection!;
        source.Selection = new TreeDataGridCellSelectionModel<Item>(source);
        var notifications = 0;
        view.Selection.Changed += (_, _) => ++notifications;
        previous.SelectedIndex = new IndexPath(1);
        Assert.Equal(0, notifications);
        view.Selection.Select(0, 0);
        Assert.Equal(1, notifications);
    }
    [Fact]
    public void Unload_detaches_notifications_without_clearing_shared_selection()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        view.Selection.Select(0, 0);
        var notifications = 0;
        view.Selection.Changed += (_, _) => ++notifications;
        view.Suspend();
        source.RowSelection!.SelectedIndex = new IndexPath(1);
        Assert.Equal(0, notifications);
        Assert.False(view.Selection.Select(2, 0));
        view.Resume();
        Assert.True(view.Selection.IsSelected(1, 0));
        view.Dispose();
        Assert.Equal(new IndexPath(1), source.RowSelection.SelectedIndex);
    }
    [Fact]
    public void No_selection_and_invalid_indexes_are_safe()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        Assert.False(view.Selection.Select(-1, 0));
        Assert.False(view.Selection.Select(0, 99));
        view.Selection.Configure(TreeDataGridSelectionMode.None);
        Assert.Null(source.Selection);
        Assert.False(view.Selection.Select(0, 0));
        Assert.Equal((-1, -1), view.Selection.GetAnchor(false));
    }
    [Fact]
    public void Select_all_honors_single_and_multiple_modes()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        view.Selection.Configure(TreeDataGridSelectionMode.SingleRow);
        view.Selection.SelectAll();
        Assert.Equal(1, source.RowSelection!.Count);
        view.Selection.Configure(TreeDataGridSelectionMode.MultipleRows);
        view.Selection.SelectAll();
        Assert.Equal(3, source.RowSelection!.Count);
        view.Selection.Configure(TreeDataGridSelectionMode.MultipleCells);
        view.Selection.SelectAll();
        Assert.Equal(9, ((ITreeDataGridCellSelectionModel<Item>)source.Selection!).Count);
        view.Selection.Clear();
        Assert.Equal(0, ((ITreeDataGridCellSelectionModel<Item>)source.Selection!).Count);
    }
    private static FlatTreeDataGridSource<Item> Source()
    {
        var source = new FlatTreeDataGridSource<Item>(new ObservableCollection<Item>([new("c"), new("a"), new("b")]));
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name));
        source.Columns.Add(new TextColumn<Item, string>("Hidden", x => x.Name) { IsVisible = false });
        source.Columns.Add(new TextColumn<Item, string>("Other", x => x.Name));
        return source;
    }
    private sealed record Item(string Name);
}
