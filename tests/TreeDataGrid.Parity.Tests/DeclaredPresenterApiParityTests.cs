using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using Xunit;
using A = Avalonia.Controls.Presentation;
using U = Uno.Controls.Presentation;

namespace TreeDataGrid.Parity.Tests;

public sealed class DeclaredPresenterApiParityTests
{
    [Theory]
    [InlineData("SourceIdentity")]
    [InlineData("IsHierarchical")]
    [InlineData("IsSorted")]
    [InlineData("CanSelectMultiple")]
    [InlineData("SelectedItems")]
    [InlineData("SelectionInteraction")]
    [InlineData("PropertyChanged")]
    [InlineData("Sorted")]
    [InlineData("SortBy")]
    public void Generic_presenter_declares_the_reference_portable_member_owner(string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var expected = Assert.Single(typeof(A.TreeDataGridPresentation<Item>).GetMember(name, flags));
        var actual = Assert.Single(typeof(U.TreeDataGridPresentation<Item>).GetMember(name, flags));
        Assert.Equal(expected.MemberType, actual.MemberType);
        Assert.Equal(typeof(U.TreeDataGridPresentation<Item>), actual.DeclaringType);
        Assert.DoesNotContain(typeof(U.TreeDataGridPresentation<Item>).GetFields(flags | BindingFlags.NonPublic),
            x => x.Name == name || x.Name == $"<{name}>k__BackingField");
    }

    [Fact]
    public void Generic_queries_reference_the_actual_source_and_selection()
    {
        using var source = CreateSource();
        using var a = new A.TreeDataGridPresentation<Item>(source);
        using var u = new U.TreeDataGridPresentation<Item>(source);
        Assert.Same(source, a.SourceIdentity); Assert.Same(source, u.SourceIdentity);
        Assert.Equal(a.IsHierarchical, u.IsHierarchical);
        Assert.Equal(a.IsSorted, u.IsSorted);
        var selection = (ITreeDataGridRowSelectionModel<Item>)source.Selection!;
        selection.SingleSelect = false;
        selection.Select(new IndexPath(1));
        Assert.Equal(a.CanSelectMultiple, u.CanSelectMultiple);
        Assert.Same(a.SelectedItems, u.SelectedItems);
        Assert.Same(source.Items.ElementAt(1), Assert.Single(u.SelectedItems!));
        Assert.Same(((U.TreeDataGridPresentation)u).SelectionInteraction, u.SelectionInteraction);
        Assert.True(u.SortBy(u.Columns[0], ListSortDirection.Ascending));
        Assert.Equal(a.IsSorted, u.IsSorted);
        Assert.Equal("A", ((Item)source.Rows[0].Model!).Name);
        Assert.True(a.SortBy(a.Columns[0], ListSortDirection.Descending));
        Assert.Equal("B", ((Item)source.Rows[0].Model!).Name);
        Assert.Same(a.SelectedItems, u.SelectedItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Property_event_add_and_remove_across_static_types_share_one_store(bool genericFirst)
    {
        using var source = CreateSource();
        using var u = new U.TreeDataGridPresentation<Item>(source);
        U.TreeDataGridPresentation untyped = u;
        var calls = 0;
        PropertyChangedEventHandler handler = (sender, e) =>
        {
            Assert.Same(u, sender);
            if (e.PropertyName == nameof(u.SelectionInteraction)) ++calls;
        };
        if (genericFirst) u.PropertyChanged += handler; else untyped.PropertyChanged += handler;
        source.Selection = null;
        Assert.Equal(1, calls);
        if (genericFirst) untyped.PropertyChanged -= handler; else u.PropertyChanged -= handler;
        source.Selection = new TreeDataGridRowSelectionModel<Item>(source);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sorted_event_and_dispatch_do_not_duplicate_core_notifications(bool genericFirst)
    {
        using var source = CreateSource();
        using var u = new U.TreeDataGridPresentation<Item>(source);
        U.TreeDataGridPresentation untyped = u;
        var calls = 0; Action handler = () => ++calls;
        if (genericFirst) u.Sorted += handler; else untyped.Sorted += handler;
        Assert.True(untyped.SortBy(u.Columns[0], ListSortDirection.Ascending));
        Assert.Equal(1, calls);
        if (genericFirst) untyped.Sorted -= handler; else u.Sorted -= handler;
        Assert.True(u.SortBy(u.Columns[0], ListSortDirection.Descending));
        Assert.Equal(1, calls);
        Assert.Same(source, u.SourceIdentity);
    }

    [Fact]
    public void Suspension_and_retirement_keep_the_existing_interaction_lifetime()
    {
        using var source = CreateSource();
        var u = new U.TreeDataGridPresentation<Item>(source);
        Assert.NotNull(u.SelectionInteraction);
        var notifications = 0;
        u.Sorted += () => ++notifications;
        u.Suspend();
        Assert.Null(u.SelectionInteraction);
        source.SortBy(source.Columns[0], ListSortDirection.Ascending);
        Assert.Equal(0, notifications);
        u.Resume();
        Assert.NotNull(u.SelectionInteraction);
        Assert.True(u.IsSorted);
        source.SortBy(source.Columns[0], ListSortDirection.Descending);
        Assert.Equal(1, notifications);
        u.Dispose();
        Assert.Null(u.SelectionInteraction);
        source.ClearSort();
        Assert.Equal(1, notifications);
        Assert.Equal(2, source.Rows.Count);
    }

    private static FlatTreeDataGridSource<Item> CreateSource()
    {
        var result = new FlatTreeDataGridSource<Item>(new ObservableCollection<Item> { new("B"), new("A") });
        result.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name));
        return result;
    }
    private sealed record Item(string Name);
}
