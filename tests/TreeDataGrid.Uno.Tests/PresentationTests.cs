using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class PresentationTests
{
    [Fact]
    public void Presentation_exposes_exact_Core_rows_and_source()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        Assert.Same(source, view.Model);
        Assert.Same(source.Rows, view.Rows);
        Assert.Same(source.Rows[0], view.Rows[0]);
        using var cell = view.RealizeCell(0, 0);
        Assert.Equal("b", cell.Value);
    }

    [Fact]
    public void Recycled_value_cell_is_reused_and_retargeted()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        var first = view.RealizeCell(0, 0);
        view.RecycleCell(view.Columns[0], first);
        Assert.Null(first.Value);
        using var second = view.RealizeCell(0, 1);
        Assert.Same(first, second);
        Assert.Equal("a", second.Value);
        second.Write("new");
        Assert.Equal("new", ((Item)source.Rows[1].Model!).Name);
    }

    [Fact]
    public void Flat_sort_notifies_presentation_even_without_collection_reset()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        var changes = 0;
        view.RowsChanged += (_, _) => ++changes;
        Assert.True(source.SortBy(source.Columns[0], ListSortDirection.Ascending));
        Assert.Equal(1, changes);
        using var cell = view.RealizeCell(0, 0);
        Assert.Equal("a", cell.Value);
        view.Suspend();
        source.ClearSort();
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Built_in_template_value_is_pooled_and_retargeted()
    {
        using var source = Source();
        source.Columns.Clear();
        source.Columns.Add(new TemplateColumn<Item>("Name", "Template"));
        using var view = TreeDataGridPresentation.Create(source);
        var first = view.RealizeCell(0, 0);
        Assert.Same(source.Rows[0].Model, first.Value);
        view.RecycleCell(view.Columns[0], first);
        Assert.Null(first.Value);
        using var next = view.RealizeCell(0, 1);
        Assert.Same(first, next);
        Assert.Same(source.Rows[1].Model, next.Value);
    }

    [Fact]
    public void Suspend_clears_pool_but_does_not_dispose_Core_source()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        var first = view.RealizeCell(0, 0);
        view.RecycleCell(view.Columns[0], first);
        view.Suspend();
        source.SortBy(source.Columns[0], ListSortDirection.Ascending);
        view.Resume();
        using var second = view.RealizeCell(0, 0);
        Assert.NotSame(first, second);
        Assert.Equal("a", second.Value);
        view.Dispose();
        Assert.Equal(2, source.Rows.Count);
    }

    [Fact]
    public void Column_changes_while_suspended_are_synchronized_on_resume()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        view.Suspend();
        source.Columns[0].IsVisible = false;
        source.Columns.Add(new TextColumn<Item, string?>("Other", x => x.Name));
        view.Resume();
        Assert.Single(view.Columns);
        Assert.Same(source.Columns[1], view.Columns[0].Model);
    }

    [Fact]
    public void Reordering_preserves_column_identity_and_updates_order()
    {
        using var source = Source();
        source.Columns.Add(new TextColumn<Item, string?>("Other", x => x.Name));
        using var view = TreeDataGridPresentation.Create(source);
        var first = view.Columns[0];
        source.Columns.Move(0, 1);
        Assert.Same(first, view.Columns[1]);
    }

    [Fact]
    public void Changing_presentation_key_disposes_old_custom_column()
    {
        using var source = Source();
        source.Columns[0].PresentationKey = "Custom";
        CountingColumn? custom = null;
        var options = new TreeDataGridPresentationOptions();
        options.Columns["Custom"] = column => custom = new CountingColumn(column);
        using var view = TreeDataGridPresentation.Create(source, options);
        source.Columns[0].PresentationKey = null;
        Assert.Equal(1, custom!.Disposals);
        Assert.NotSame(custom, view.Columns[0]);
    }

    [Fact]
    public void Failed_replacement_retains_old_view_and_recovers_on_key_repair()
    {
        using var source = Source();
        source.Columns[0].PresentationKey = "Custom";
        var options = new TreeDataGridPresentationOptions();
        options.Columns["Custom"] = column => new CountingColumn(column);
        using var view = TreeDataGridPresentation.Create(source, options);
        var previous = (CountingColumn)view.Columns[0];
        Assert.Throws<InvalidOperationException>(() => source.Columns[0].PresentationKey = "Missing");
        Assert.Same(previous, view.Columns[0]);
        Assert.Equal(0, previous.Disposals);
        source.Columns[0].PresentationKey = null;
        Assert.NotSame(previous, view.Columns[0]);
        Assert.Equal(1, previous.Disposals);
        using var cell = view.RealizeCell(0, 0);
        Assert.Equal("b", cell.Value);
    }

    [Fact]
    public void Newly_added_failed_column_is_observed_until_repaired()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        var column = new TextColumn<Item, string?>("Other", x => x.Name) { PresentationKey = "Missing" };
        Assert.Throws<InvalidOperationException>(() => source.Columns.Add(column));
        Assert.Single(view.Columns);
        column.PresentationKey = null;
        Assert.Equal(2, view.Columns.Count);
        Assert.Same(column, view.Columns[1].Model);
    }

    [Fact]
    public void Removed_failed_column_no_longer_notifies_view()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        var column = new TextColumn<Item, string?>("Other", x => x.Name) { PresentationKey = "Missing" };
        Assert.Throws<InvalidOperationException>(() => source.Columns.Add(column));
        source.Columns.Remove(column);
        var changes = 0;
        view.ColumnsChanged += (_, _) => ++changes;
        column.PresentationKey = null;
        Assert.Equal(0, changes);
        Assert.Single(view.Columns);
    }

    [Fact]
    public void Resume_after_failed_factory_can_be_retried_without_losing_views()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        var previous = view.Columns[0];
        view.Suspend();
        source.Columns[0].PresentationKey = "Missing";
        Assert.Throws<InvalidOperationException>(view.Resume);
        Assert.Same(previous, view.Columns[0]);
        source.Columns[0].PresentationKey = null;
        view.Resume();
        using var cell = view.RealizeCell(0, 0);
        Assert.Equal("b", cell.Value);
    }

    [Fact]
    public void Failed_expander_realization_disposes_custom_inner_cell_exactly_once()
    {
        var fail = false;
        var item = new Item("parent");
        using var source = new HierarchicalTreeDataGridSource<Item>([item]);
        var inner = new TextColumn<Item, string?>("Name", x => x.Name) { PresentationKey = "Custom" };
        source.Columns.Add(new HierarchicalExpanderColumn<Item>(inner,
            x => fail ? throw new InvalidOperationException("children") : x.Children));
        var options = new TreeDataGridPresentationOptions();
        CountingColumn? column = null;
        options.Columns["Custom"] = model => column = new CountingColumn(model);
        using var view = TreeDataGridPresentation.Create(source, options);
        _ = source.Rows[0];
        fail = true;
        Assert.Throws<InvalidOperationException>(() => view.RealizeCell(0, 0));
        Assert.Equal(1, column!.LastCell!.Disposals);
    }

    [Fact]
    public void Expander_disposes_custom_inner_column_once()
    {
        using var source = new HierarchicalTreeDataGridSource<Item>([new Item("parent")]);
        var inner = new TextColumn<Item, string?>("Name", x => x.Name) { PresentationKey = "Custom" };
        source.Columns.Add(new HierarchicalExpanderColumn<Item>(inner, x => x.Children));
        var options = new TreeDataGridPresentationOptions();
        CountingColumn? custom = null;
        options.Columns["Custom"] = column => custom = new CountingColumn(column);
        var view = TreeDataGridPresentation.Create(source, options);
        view.Dispose();
        view.Dispose();
        Assert.Equal(1, custom!.Disposals);
    }

    [Fact]
    public void Custom_cells_are_disposed_instead_of_pooled()
    {
        using var source = Source();
        source.Columns[0].PresentationKey = "Custom";
        var options = new TreeDataGridPresentationOptions();
        options.Columns["Custom"] = column => new CountingColumn(column);
        using var view = TreeDataGridPresentation.Create(source, options);
        var cell = (CountingCell)view.RealizeCell(0, 0);
        view.RecycleCell(view.Columns[0], cell);
        Assert.Equal(1, cell.Disposals);
    }

    [Fact]
    public void Bounded_pool_rejects_cells_beyond_capacity()
    {
        using var source = Source();
        using var view = TreeDataGridPresentation.Create(source);
        var cells = new CellValue[257];
        for (var i = 0; i < cells.Length; ++i) cells[i] = view.RealizeCell(0, 0);
        foreach (var cell in cells) view.RecycleCell(view.Columns[0], cell);
        Assert.Throws<ObjectDisposedException>(() => cells[^1].Write("disposed"));
        Assert.Null(cells[0].Value);
    }

    [Fact]
    public void Expansion_is_the_shared_Core_row_state()
    {
        var parent = new Item("parent");
        parent.Children.Add(new Item("child"));
        using var source = new HierarchicalTreeDataGridSource<Item>([parent]);
        source.Columns.Add(new HierarchicalExpanderColumn<Item>(new TextColumn<Item, string?>("Name", x => x.Name), x => x.Children));
        using var view = TreeDataGridPresentation.Create(source);
        using var cell = view.RealizeCell(0, 0);
        var expander = Assert.IsAssignableFrom<ExpanderCellValue>(cell);
        Assert.True(expander.ShowExpander);
        expander.IsExpanded = true;
        Assert.Equal(2, source.Rows.Count);
        Assert.True(((IExpander)source.Rows[0]).IsExpanded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Explicit_has_children_binding_does_not_force_lazy_children(bool hasChildren)
    {
        var model = new Item(hasChildren ? "parent" : "");
        using var source = new HierarchicalTreeDataGridSource<Item>([model]);
        source.Columns.Add(new HierarchicalExpanderColumn<Item>(
            new TextColumn<Item, string?>("Name", x => x.Name),
            _ => throw new InvalidOperationException("Children should only load on expansion."),
            x => !string.IsNullOrEmpty(x.Name)));
        using var view = TreeDataGridPresentation.Create(source);
        using var cell = view.RealizeCell(0, 0);
        var expander = Assert.IsAssignableFrom<ExpanderCellValue>(cell);
        Assert.Equal(hasChildren, expander.ShowExpander);
        model.Name = hasChildren ? "" : "parent";
        Assert.Equal(!hasChildren, expander.ShowExpander);
    }

    private static FlatTreeDataGridSource<Item> Source()
    {
        var result = new FlatTreeDataGridSource<Item>([new Item("b"), new Item("a")]);
        result.Columns.Add(new TextColumn<Item, string?>("Name", x => x.Name, (x, value) => x.Name = value));
        return result;
    }

    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string? _name = name;
        public string? Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public ObservableCollection<Item> Children { get; } = new();
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class CountingColumn(IColumn model) : CellColumn(model)
    {
        public int Disposals { get; private set; }
        public CountingCell? LastCell { get; private set; }
        public override CellValue CreateCell(IRow row) => LastCell = new CountingCell();
        public override void Dispose() => ++Disposals;
    }
    private sealed class CountingCell : CellValue
    {
        public int Disposals { get; private set; }
        public override object? Value => null;
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new NotSupportedException();
        public override void Dispose() => ++Disposals;
    }
}
