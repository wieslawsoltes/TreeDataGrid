using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class VisibleColumnMutationTests
{
    [Fact]
    public void Insert_remove_and_move_publish_precise_changes_without_replacing_views()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("value")]);
        var a = new TextColumn<Item, string>("a", x => x.Name);
        var b = new TextColumn<Item, string>("b", x => x.Name);
        source.Columns.Add(a);
        source.Columns.Add(b);
        using var view = TreeDataGridPresentation.Create(source);
        var first = view.Columns[0];
        var second = view.Columns[1];
        var changes = new List<NotifyCollectionChangedEventArgs>();
        view.Columns.CollectionChanged += (_, args) => changes.Add(args);
        var inserted = new TextColumn<Item, string>("inserted", x => x.Name);
        source.Columns.Insert(1, inserted);
        Assert.Equal(NotifyCollectionChangedAction.Add, Assert.Single(changes).Action);
        Assert.Same(first, view.Columns[0]);
        Assert.Same(second, view.Columns[2]);
        changes.Clear();
        source.Columns.Move(2, 0);
        var move = Assert.Single(changes);
        Assert.Equal(NotifyCollectionChangedAction.Move, move.Action);
        Assert.Equal(2, move.OldStartingIndex);
        Assert.Equal(0, move.NewStartingIndex);
        Assert.Same(second, view.Columns[0]);
        Assert.Same(first, view.Columns[1]);
        changes.Clear();
        source.Columns.Remove(inserted);
        Assert.Equal(NotifyCollectionChangedAction.Remove, Assert.Single(changes).Action);
        Assert.Same(second, view.Columns[0]);
        Assert.Same(first, view.Columns[1]);
    }

    [Fact]
    public void Visibility_change_only_removes_or_inserts_the_affected_view()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("value")]);
        for (var i = 0; i < 3; ++i) source.Columns.Add(new TextColumn<Item, string>(i, x => x.Name));
        using var view = TreeDataGridPresentation.Create(source);
        var original = view.Columns.ToArray();
        var changes = new List<NotifyCollectionChangedAction>();
        view.Columns.CollectionChanged += (_, args) => changes.Add(args.Action);
        source.Columns[1].IsVisible = false;
        Assert.Equal(new[] { NotifyCollectionChangedAction.Remove }, changes);
        Assert.Same(original[0], view.Columns[0]);
        Assert.Same(original[2], view.Columns[1]);
        source.Columns[1].IsVisible = true;
        Assert.Equal(new[] { NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add }, changes);
        Assert.Same(original[1], view.Columns[1]);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    [InlineData(1, 2)]
    public void Moving_a_column_preserves_order_and_all_column_instances(int from, int to)
    {
        using var source = new FlatTreeDataGridSource<Item>([new("value")]);
        for (var i = 0; i < 4; ++i) source.Columns.Add(new TextColumn<Item, string>(i, x => x.Name));
        using var view = TreeDataGridPresentation.Create(source);
        var original = view.Columns.ToArray();
        var expected = original.ToList();
        var moved = expected[from];
        expected.RemoveAt(from);
        expected.Insert(to, moved);
        var changes = new List<NotifyCollectionChangedAction>();
        view.Columns.CollectionChanged += (_, args) => changes.Add(args.Action);
        source.Columns.Move(from, to);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Move }, changes);
        for (var i = 0; i < expected.Count; ++i) Assert.Same(expected[i], view.Columns[i]);
    }

    private sealed record Item(string Name);
}
