using System;
using System.ComponentModel;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;
using U = global::Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class BuiltInBindingSnapshotTests
{
    [Fact]
    public void Ordinary_cells_do_not_materialize_the_public_descriptor()
    {
        var column = new U.TextColumn<Item, string?>("Name", x => x.Name);
        Assert.Null(column.CaptureBindingSnapshot());
        using var cell = column.CreateCell(new Row(new()));
        Assert.Null(column.CaptureBindingSnapshot());
        Assert.Equal("Name", cell.Value);
    }

    [Fact]
    public void Stable_configuration_reuses_instructions_and_link_edits_are_detected()
    {
        var column = new U.TextColumn<Item, string?>("Name", x => x.Name);
        var descriptor = column.Binding;
        var first = column.CaptureBindingSnapshot()!;
        Assert.Same(first, column.CaptureBindingSnapshot());
        var link = descriptor.Links![0];
        descriptor.Links[0] = static _ => null!;
        var second = column.CaptureBindingSnapshot()!;
        Assert.NotSame(first, second);
        Assert.Same(link, first.Links[0]);
        Assert.Null(second.Links[0](new()));
        Assert.Same(second, column.CaptureBindingSnapshot());
        descriptor.Links[0] = link;
        Assert.NotSame(second, column.CaptureBindingSnapshot());
    }

    [Fact]
    public void Different_views_do_not_share_mutable_descriptors_or_link_arrays()
    {
        var core = new ValueColumn<Item, string?>("Name", x => x.Name);
        var first = new U.TextColumn<Item, string?>(core);
        var second = new U.TextColumn<Item, string?>(core);
        Assert.NotSame(first.Binding, second.Binding);
        Assert.NotSame(first.Binding.Links, second.Binding.Links);
        first.Binding.Read = static _ => "Different";
        first.Binding.Links![0] = static _ => null!;
        var item = new Item();
        using var cell = second.CreateCell(new Row(item));
        Assert.Equal("Name", cell.Value);
        Assert.Same(item, second.Binding.Links![0](item));
        Assert.Same(core.Getter, second.ValueSelector);
    }

    [Fact]
    public void Invalid_link_mutation_keeps_live_cells_and_previous_snapshot_intact()
    {
        var column = new U.TextColumn<Item, string?>("Name", x => x.Name);
        var descriptor = column.Binding;
        var item = new Item();
        using var cell = column.CreateCell(new Row(item));
        var previous = column.CaptureBindingSnapshot()!;
        var link = descriptor.Links![0];
        descriptor.Links[0] = null!;
        Assert.Throws<ArgumentException>(() => column.CreateCell(new Row(new())));
        item.Name = "Still observed";
        Assert.Equal("Still observed", cell.Value);
        Assert.Equal(1, item.Subscribers);
        Assert.Same(link, previous.Links[0]);
        descriptor.Links[0] = link;
        Assert.Same(previous, column.CaptureBindingSnapshot());
        using var recovered = column.CreateCell(new Row(new()));
        Assert.Equal("Name", recovered.Value);
    }

    [Fact]
    public void Fallback_versioning_never_invokes_application_equality()
    {
        var definition = ValueColumn<Item, Uninspectable>.FromDelegate("Value", static _ => new());
        var column = new ValueCellColumn<Item, Uninspectable>(definition, CellKind.Text);
        var first = new Uninspectable(); var second = new Uninspectable();
        column.Binding.FallbackValue = first;
        var before = column.CaptureBindingSnapshot()!;
        Assert.Same(before, column.CaptureBindingSnapshot());
        column.Binding.FallbackValue = second;
        var after = column.CaptureBindingSnapshot()!;
        Assert.NotSame(before, after);
        Assert.Same(first, before.Fallback.Value);
        Assert.Same(second, after.Fallback.Value);
    }

    [Fact]
    public void Suspended_descriptor_cells_release_owners_and_keep_their_creation_snapshot()
    {
        var column = new U.TextColumn<Item, string?>("Name", x => x.Name, (x, value) => x.Name = value);
        _ = column.Binding;
        var first = new Item(); var second = new Item();
        using var value = column.CreateCell(new Row(first));
        Assert.True(value.TrySuspend());
        Assert.Null(value.Value);
        Assert.Equal(0, first.Subscribers);
        column.Binding.Read = static _ => "Future cells only";
        column.Binding.Write = null;
        Assert.True(column.TryReuseCell(value, new Row(second)));
        Assert.Equal("Name", value.Value);
        Assert.True(value.CanWrite);
        value.Write("Retained writer");
        Assert.Equal("Retained writer", second.Name);
        Assert.Equal("Name", first.Name);
        value.Dispose();
        Assert.Equal(0, second.Subscribers);
    }

    [Fact]
    public void Warm_snapshot_queries_and_descriptor_cell_recycling_allocate_nothing()
    {
        var column = new U.TextColumn<Item, string?>("Name", x => x.Name, (x, value) => x.Name = value);
        column.Binding.Read = static x => x.Name;
        column.Binding.Links = [static x => x];
        var snapshot = column.CaptureBindingSnapshot()!;
        var first = new Row(new()); var second = new Row(new());
        using var value = column.CreateCell(first);
        for (var i = 0; i < 1024; ++i)
        {
            column.CaptureBindingSnapshot();
            value.TrySuspend(); column.TryReuseCell(value, (i & 1) == 0 ? first : second);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var correct = true;
        for (var i = 0; i < 4096; ++i)
        {
            correct &= ReferenceEquals(snapshot, column.CaptureBindingSnapshot());
            correct &= value.TrySuspend();
            correct &= column.TryReuseCell(value, (i & 1) == 0 ? first : second);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(correct);
        Assert.Equal(0, allocated);
        value.Dispose();
        Assert.Equal(0, first.Model.Subscribers);
        Assert.Equal(0, second.Model.Subscribers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pool_suspension_clears_prior_value_before_a_new_row_getter_fails(bool requestDescriptor)
    {
        var column = new U.TextColumn<Item, string?>("Name", x => x.Name);
        if (requestDescriptor) _ = column.Binding;
        var first = new Item { Name = "Private previous row" };
        var second = new Item { FailRead = true };
        using var cell = column.CreateCell(new Row(first));
        Assert.Equal("Private previous row", cell.Value);
        Assert.True(cell.TrySuspend());
        Assert.Null(cell.Value);
        Assert.Equal(0, first.Subscribers);
        Assert.True(column.TryReuseCell(cell, new Row(second)));
        Assert.Null(cell.Value);
        Assert.IsType<InvalidOperationException>(cell.Error);
        Assert.Equal(1, second.Subscribers);
        second.FailRead = false;
        second.Name = "New row recovered";
        Assert.Equal("New row recovered", cell.Value);
        Assert.Null(cell.Error);
        cell.Dispose();
        Assert.Null(cell.Value);
        Assert.Equal(0, second.Subscribers);
    }

    private sealed class Row(Item model) : IRow<Item>
    {
        public Item Model => model;
        public object? Header => null;
        public TreeDataGridCore.GridLength Height { get; set; }
        public void UpdateModelIndex(int delta) { }
    }
    private sealed class Item : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs NameChanged = new(nameof(Name));
        private string? _name = "Name";
        private PropertyChangedEventHandler? _changed;
        public bool FailRead { get; set; }
        public string? Name { get => FailRead ? throw new InvalidOperationException("Expected getter failure.") : _name; set { _name = value; _changed?.Invoke(this, NameChanged); } }
        public int Subscribers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
    private sealed class Uninspectable
    {
        public override bool Equals(object? other) => throw new InvalidOperationException("Do not compare fallback values.");
        public override int GetHashCode() => throw new InvalidOperationException("Do not hash fallback values.");
    }
}
