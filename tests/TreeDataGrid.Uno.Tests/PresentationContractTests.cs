using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using Uno.Controls;
using Uno.Controls.Presentation;
using Windows.ApplicationModel.DataTransfer;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class PresentationContractTests
{
    [Fact]
    public void Reference_selection_surface_uses_original_Core_indexes_and_items()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        Assert.Same(source, view.SourceIdentity);
        Assert.False(view.IsHierarchical);
        Assert.False(view.CanSelectMultiple);
        view.Selection.Configure(TreeDataGridSelectionMode.MultipleRows);
        Assert.True(view.CanSelectMultiple);
        view.Select(new(0), replace: true);
        view.Select(new(2), replace: false);
        Assert.True(view.IsSelected(new(0)));
        Assert.Equal(new[] { new IndexPath(0), new IndexPath(2) }, view.SelectedIndexes);
        Assert.Same(source.RowSelection!.SelectedItems, view.SelectedItems);
        view.Deselect(new(0));
        Assert.Equal(new[] { new IndexPath(2) }, view.SelectedIndexes);
        view.Selection.Configure(TreeDataGridSelectionMode.Cell);
        Assert.Null(view.SelectedItems);
        Assert.Null(view.SelectedIndexes);
        Assert.False(view.CanSelectMultiple);
    }

    [Fact]
    public void Sorting_uses_UI_column_identity_and_forwards_completion()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        var sorted = 0;
        view.Sorted += () => ++sorted;
        Assert.True(view.SortBy(view.Columns[0], ListSortDirection.Ascending));
        Assert.True(view.IsSorted);
        Assert.Equal("a", ((Item)view.Rows[0].Model!).Name);
        Assert.Equal(1, sorted);
        view.Suspend();
        source.SortBy(source.Columns[0], ListSortDirection.Descending);
        Assert.Equal(1, sorted);
        view.Resume();
        view.SortBy(view.Columns[0], ListSortDirection.Ascending);
        Assert.Equal(2, sorted);
    }

    [Fact]
    public void Native_move_effects_reach_the_original_Core_collection()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        view.MoveRows([new(0)], new(2), TreeDataGridRowDropPosition.After, DataPackageOperation.Move);
        Assert.Equal(new[] { "a", "b", "c" }, source.Items.Select(item => item.Name));
        Assert.Same(source, view.SourceIdentity);
    }

    [Fact]
    public void Interaction_availability_notifies_on_selection_and_lifetime_changes()
    {
        using var source = Source();
        var view = TreeDataGridPresentation.Create(source);
        try
        {
            var notifications = new List<string?>();
            ((INotifyPropertyChanged)view).PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
            Assert.Same(view.Selection, view.SelectionInteraction);
            source.Selection = null;
            Assert.Null(view.SelectionInteraction);
            Assert.Contains(nameof(TreeDataGridPresentation.SelectionInteraction), notifications);
            source.Selection = new TreeDataGridRowSelectionModel<Item>(source);
            Assert.Same(view.Selection, view.SelectionInteraction);
            view.Suspend();
            Assert.Null(view.SelectionInteraction);
            var suspendedCount = notifications.Count;
            source.Selection = null;
            Assert.Equal(suspendedCount, notifications.Count);
            source.Selection = new TreeDataGridRowSelectionModel<Item>(source);
            view.Resume();
            Assert.Same(view.Selection, view.SelectionInteraction);
            Assert.True(notifications.Count > suspendedCount);
        }
        finally { view.Dispose(); }
        Assert.Null(view.SelectionInteraction);
    }

    private static FlatTreeDataGridSource<Item> Source()
    {
        var source = new FlatTreeDataGridSource<Item>(new ObservableCollection<Item>([new("c"), new("a"), new("b")]));
        source.Columns.Add(new TextColumn<Item, string>("Name", item => item.Name));
        return source;
    }
    private sealed record Item(string Name);
}
