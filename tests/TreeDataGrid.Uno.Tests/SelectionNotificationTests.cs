using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using Uno.Controls;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

// Authored during the implementation-first parity pass; execute with the full
// deferred validation suite, not as evidence for the unbuilt working changes.
public class SelectionNotificationTests
{
    [Fact]
    public void Row_deltas_use_model_indexes_and_snapshot_items_after_sorting()
    {
        var items = Items();
        using var source = Source(items);
        using var view = TreeDataGridPresentation.Create(source);
        source.SortBy(source.Columns[0], ListSortDirection.Ascending);
        var events = new List<TreeDataGridSelectionChangedEventArgs>();
        view.Selection.SelectionChanged += (_, e) => events.Add(e);

        view.Selection.Select(0, 0);
        view.Selection.Select(2, 0);

        Assert.Equal(2, events.Count);
        Assert.Equal(new IndexPath(1), Assert.Single(events[0].SelectedIndexes));
        Assert.Same(items[1], Assert.Single(events[0].SelectedItems));
        Assert.Equal(new IndexPath(1), Assert.Single(events[1].DeselectedIndexes));
        Assert.Equal(new IndexPath(0), Assert.Single(events[1].SelectedIndexes));
        var selected = items[0];
        items.Insert(0, new("new"));
        Assert.Same(selected, Assert.Single(events[1].SelectedItems));
        Assert.Equal(new IndexPath(0), Assert.Single(events[1].SelectedIndexes));
        Assert.Empty(events[1].SelectedCellIndexes);
    }

    [Fact]
    public void Row_index_changes_refresh_visual_state_without_fabricating_selection_deltas()
    {
        var items = Items();
        using var source = Source(items);
        using var view = TreeDataGridPresentation.Create(source);
        view.Selection.Select(1, 0);
        var deltas = 0;
        var invalidations = 0;
        view.Selection.SelectionChanged += (_, _) => ++deltas;
        view.Selection.Changed += (_, _) => ++invalidations;

        items.Insert(0, new("new"));

        Assert.Equal(0, deltas);
        Assert.True(invalidations > 0);
        Assert.Equal(new IndexPath(2), source.RowSelection!.SelectedIndex);
    }

    [Fact]
    public void Cell_range_deltas_include_hidden_source_columns()
    {
        using var source = Source(Items());
        using var view = TreeDataGridPresentation.Create(source);
        view.Selection.Configure(TreeDataGridSelectionMode.Cell | TreeDataGridSelectionMode.Multiple);
        var events = new List<TreeDataGridSelectionChangedEventArgs>();
        view.Selection.SelectionChanged += (_, e) => events.Add(e);

        view.Selection.Select(0, 0);
        view.Selection.Select(1, 1, extend: true);
        view.Selection.Select(0, 0);

        Assert.Equal(3, events.Count);
        Assert.Single(events[0].SelectedCellIndexes);
        Assert.Equal(5, events[1].SelectedCellIndexes.Count);
        Assert.Contains(new CellIndex(1, new IndexPath(1)), events[1].SelectedCellIndexes);
        Assert.Empty(events[1].DeselectedCellIndexes);
        Assert.Equal(5, events[2].DeselectedCellIndexes.Count);
        Assert.Empty(events[2].SelectedCellIndexes);
        // Match Avalonia: cell deltas are source CellIndex values, not fabricated
        // row-item selection or flattened visible-column indexes.
        Assert.Empty(events[1].SelectedIndexes);
        Assert.Empty(events[1].SelectedItems);
    }

    [Fact]
    public void Late_cell_subscriber_starts_from_current_selection()
    {
        using var source = Source(Items());
        using var view = TreeDataGridPresentation.Create(source);
        view.Selection.Configure(TreeDataGridSelectionMode.Cell);
        view.Selection.Select(1, 1);
        TreeDataGridSelectionChangedEventArgs? change = null;
        view.Selection.SelectionChanged += (_, e) => change = e;
        view.Selection.Select(2, 0);
        Assert.NotNull(change);
        Assert.Equal(new CellIndex(2, new IndexPath(1)), Assert.Single(change.DeselectedCellIndexes));
        Assert.Equal(new CellIndex(0, new IndexPath(2)), Assert.Single(change.SelectedCellIndexes));
    }

    [Fact]
    public void Reentrant_cell_selection_commits_baseline_before_publishing()
    {
        using var source = Source(Items());
        using var view = TreeDataGridPresentation.Create(source);
        view.Selection.Configure(TreeDataGridSelectionMode.Cell);
        var events = new List<TreeDataGridSelectionChangedEventArgs>();
        view.Selection.SelectionChanged += (_, e) =>
        {
            events.Add(e);
            if (events.Count == 1) view.Selection.Select(1, 1);
        };
        view.Selection.Select(0, 0);
        view.Selection.Select(2, 0);
        Assert.Equal(3, events.Count);
        Assert.Equal(new CellIndex(0, new IndexPath(0)), Assert.Single(events[1].DeselectedCellIndexes));
        Assert.Equal(new CellIndex(2, new IndexPath(1)), Assert.Single(events[1].SelectedCellIndexes));
        Assert.Equal(new CellIndex(2, new IndexPath(1)), Assert.Single(events[2].DeselectedCellIndexes));
        Assert.Equal(new CellIndex(0, new IndexPath(0)), Assert.Single(events[0].SelectedCellIndexes));
    }

    [Fact]
    public void Replacement_suspend_and_dispose_detach_detailed_notifications()
    {
        using var source = Source(Items());
        using var view = TreeDataGridPresentation.Create(source);
        var previous = source.RowSelection!;
        var events = new List<TreeDataGridSelectionChangedEventArgs>();
        view.Selection.SelectionChanged += (_, e) => events.Add(e);
        view.Selection.Configure(TreeDataGridSelectionMode.Cell);
        var cells = (ITreeDataGridCellSelectionModel<Item>)source.Selection!;
        previous.SelectedIndex = new IndexPath(1);
        Assert.Empty(events);
        cells.SelectedIndex = new(0, new IndexPath(0));
        Assert.Single(events);
        events.Clear();
        view.Suspend();
        cells.SelectedIndex = new(2, new IndexPath(1));
        Assert.Empty(events);
        view.Resume();
        Assert.Empty(events);
        cells.SelectedIndex = new(0, new IndexPath(2));
        Assert.Equal(new CellIndex(2, new IndexPath(1)), Assert.Single(Assert.Single(events).DeselectedCellIndexes));
        events.Clear();
        view.Dispose();
        cells.Clear();
        Assert.Empty(events);
    }

    [Fact]
    public void Arguments_preserve_Core_base_contract_and_generic_shape()
    {
        var item = new Item("a");
        var args = new TreeDataGridSelectionChangedEventArgs<Item>(
            selectedIndexes: [new IndexPath(2)], selectedItems: [item],
            selectedCellIndexes: [new CellIndex(1, new IndexPath(2))]);
        TreeSelectionModelSelectionChangedEventArgs untyped = args;
        Assert.Same(item, Assert.Single(untyped.SelectedItems));
        Assert.Equal(new IndexPath(2), Assert.Single(untyped.SelectedIndexes));
        Assert.Empty(args.DeselectedCellIndexes);
        Assert.Single(args.SelectedCellIndexes);
    }

    private static ObservableCollection<Item> Items() => [new("c"), new("a"), new("b")];
    private static FlatTreeDataGridSource<Item> Source(ObservableCollection<Item> items)
    {
        var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name));
        source.Columns.Add(new TextColumn<Item, string>("Hidden", x => x.Name) { IsVisible = false });
        source.Columns.Add(new TextColumn<Item, string>("Other", x => x.Name));
        return source;
    }
    private sealed record Item(string Name);
}
