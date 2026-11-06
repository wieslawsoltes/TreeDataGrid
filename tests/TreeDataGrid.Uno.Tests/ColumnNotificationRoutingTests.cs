using System;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnNotificationRoutingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    public void A_width_change_notifies_only_its_own_view(int count)
    {
        using var source = CreateSource(count);
        using var presentation = TreeDataGridPresentation.Create(source);
        var views = presentation.NativeColumns.ToArray();
        var calls = new int[count];
        for (var index = 0; index < count; ++index)
        {
            var slot = index;
            views[index].PropertyChanged += (_, args) => { if (args.PropertyName == "Width") ++calls[slot]; };
        }
        var layoutChanges = 0;
        presentation.ColumnsChanged += (_, _) => ++layoutChanges;
        source.Columns[count / 2].Width = new GridLength(211);
        Assert.Equal(1, calls.Sum());
        Assert.Equal(1, calls[count / 2]);
        Assert.Equal(1, layoutChanges);
        Assert.Equal(211d, views[count / 2].Width.Value);
    }

    [Fact]
    public void Forwarded_inner_notifications_reach_each_related_view_once()
    {
        var inner = new TextColumn<Item, string>("Inner", item => item.Name);
        var expander = new HierarchicalExpanderColumn<Item>(inner, _ => Array.Empty<Item>());
        using var source = new HierarchicalTreeDataGridSource<Item>([new()]);
        source.Columns.Add(expander);
        source.Columns.Add(inner);
        source.Columns.Add(new TextColumn<Item, string>("Unrelated", item => item.Name));
        using var presentation = TreeDataGridPresentation.Create(source);
        var calls = new int[3];
        for (var index = 0; index < calls.Length; ++index)
        {
            var slot = index;
            presentation.NativeColumns[index].PropertyChanged += (_, args) => { if (args.PropertyName == "Width") ++calls[slot]; };
        }
        inner.Width = new GridLength(183);
        Assert.Equal(new[] { 1, 1, 0 }, calls);
        Assert.Equal(183d, presentation.NativeColumns[0].Width.Value);
        Assert.Equal(183d, presentation.NativeColumns[1].Width.Value);
    }

    [Fact]
    public void Hidden_cached_views_receive_only_their_own_definition_changes()
    {
        using var source = CreateSource(2);
        using var presentation = TreeDataGridPresentation.Create(source);
        var hidden = presentation.NativeColumns[1];
        source.Columns[1].IsVisible = false;
        var hiddenChanges = 0;
        hidden.PropertyChanged += (_, args) => { if (args.PropertyName == "Width") ++hiddenChanges; };
        source.Columns[0].Width = new GridLength(181);
        Assert.Equal(0, hiddenChanges);
        source.Columns[1].Width = new GridLength(191);
        Assert.Equal(1, hiddenChanges);
        source.Columns[1].IsVisible = true;
        Assert.Same(hidden, presentation.NativeColumns[1]);
        Assert.Equal(191d, hidden.Width.Value);
    }

    [Fact]
    public void Removing_a_definition_inside_its_notification_does_not_visit_retired_views()
    {
        using var source = CreateSource(2);
        using var presentation = TreeDataGridPresentation.Create(source);
        var definition = source.Columns[0];
        var view = presentation.NativeColumns[0];
        var calls = 0;
        view.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != "Width") return;
            ++calls;
            source.Columns.RemoveAt(0);
        };
        definition.Width = new GridLength(181);
        Assert.Equal(1, calls);
        Assert.Single(presentation.Columns);
        definition.Width = new GridLength(182);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Disposal_inside_a_view_notification_stops_retired_publication()
    {
        using var source = CreateSource(2);
        using var presentation = TreeDataGridPresentation.Create(source);
        var changes = 0;
        presentation.ColumnsChanged += (_, _) => ++changes;
        presentation.NativeColumns[0].PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "Width") presentation.Dispose();
        };
        source.Columns[0].Width = new GridLength(181);
        Assert.Empty(presentation.Columns);
        Assert.Equal(0, changes);
        source.Columns[0].Width = new GridLength(182);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Repeated_suspend_resume_keeps_one_handler_per_definition()
    {
        using var source = CreateSource(2);
        using var presentation = TreeDataGridPresentation.Create(source);
        var view = presentation.NativeColumns[0];
        var calls = 0;
        view.PropertyChanged += (_, args) => { if (args.PropertyName == "Width") ++calls; };
        for (var iteration = 0; iteration < 4; ++iteration)
        {
            presentation.Suspend();
            source.Columns[0].Width = new GridLength(200 + iteration * 2);
            Assert.Equal(iteration, calls);
            presentation.Resume();
            Assert.Same(view, presentation.NativeColumns[0]);
            source.Columns[0].Width = new GridLength(201 + iteration * 2);
            Assert.Equal(iteration + 1, calls);
        }
    }

    [Fact]
    public void Nested_model_changes_restore_the_outer_notification_scope()
    {
        using var source = CreateSource(2);
        using var presentation = TreeDataGridPresentation.Create(source);
        var view = presentation.NativeColumns[0];
        var changes = 0;
        var entered = false;
        presentation.ColumnsChanged += (_, _) => ++changes;
        view.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != "Width" || entered) return;
            entered = true;
            source.Columns[1].Width = new GridLength(212);
            // Still inside the outer model notification: this local change is
            // already covered by the pending outer ColumnsChanged event.
            view.Header = "Retitled";
        };
        source.Columns[0].Width = new GridLength(211);
        Assert.Equal(2, changes);
        Assert.Equal("Retitled", view.Header);
    }

    [Fact]
    public void Throwing_handlers_do_not_disable_future_view_notifications()
    {
        using var source = CreateSource(2);
        using var presentation = TreeDataGridPresentation.Create(source);
        var view = presentation.NativeColumns[0];
        var failure = new InvalidOperationException("application observer");
        PropertyChangedEventHandler handler = (_, args) => { if (args.PropertyName == "Width") throw failure; };
        view.PropertyChanged += handler;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => source.Columns[0].Width = new GridLength(211)));
        view.PropertyChanged -= handler;
        var changes = 0;
        presentation.ColumnsChanged += (_, _) => ++changes;
        view.Header = "Recovered";
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Cached_event_routing_has_zero_warmed_allocation()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var column = new NotifyingColumn();
        source.Columns.Add(column);
        using var presentation = TreeDataGridPresentation.Create(source);
        var changes = 0;
        presentation.ColumnsChanged += (_, _) => ++changes;
        for (var iteration = 0; iteration < 1024; ++iteration) column.NotifyWidth();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 4096; ++iteration) column.NotifyWidth();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(5120, changes);
    }

    private static FlatTreeDataGridSource<Item> CreateSource(int count)
    {
        var source = new FlatTreeDataGridSource<Item>([new()]);
        for (var index = 0; index < count; ++index)
            source.Columns.Add(new TextColumn<Item, string>($"Column {index}", item => item.Name));
        return source;
    }

    private sealed class Item { public string Name => "Item"; }
    private sealed class NotifyingColumn() : TextColumn<Item, string>("Name", item => item.Name)
    {
        private static readonly PropertyChangedEventArgs WidthChanged = new("Width");
        public void NotifyWidth() => RaisePropertyChanged(WidthChanged);
    }
}
