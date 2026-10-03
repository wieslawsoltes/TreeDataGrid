using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using Xunit;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class TriStateSortingTests
{
    [Theory]
    [InlineData(null, false, ListSortDirection.Ascending)]
    [InlineData(null, true, ListSortDirection.Ascending)]
    [InlineData(ListSortDirection.Ascending, false, ListSortDirection.Descending)]
    [InlineData(ListSortDirection.Ascending, true, ListSortDirection.Descending)]
    [InlineData(ListSortDirection.Descending, false, ListSortDirection.Ascending)]
    [InlineData(ListSortDirection.Descending, true, null)]
    [InlineData((ListSortDirection)42, false, ListSortDirection.Ascending)]
    [InlineData((ListSortDirection)42, true, ListSortDirection.Ascending)]
    public void Header_cycle_has_an_opt_in_unsorted_transition(ListSortDirection? current, bool triState, ListSortDirection? expected) =>
        Assert.Equal(expected, ColumnSortCycle.Next(current, triState));

    [Fact]
    public void Definition_common_options_capture_the_flag_without_sharing_scalar_storage()
    {
        var definition = new Definition { AllowTriStateSorting = true };
        var options = definition.Capture();
        definition.AllowTriStateSorting = false;
        Assert.True(options.AllowTriStateSorting);
        Assert.False(definition.Capture().AllowTriStateSorting);
    }

    [Fact]
    public void Copying_native_options_retains_the_flag_but_neutral_options_do_not_invent_it()
    {
        var options = new UI.TextColumnOptions<Item> { AllowTriStateSorting = true };
        var copied = UI.ColumnOptions<Item>.CopyCore(options, new UI.TemplateColumnOptions<Item>());
        Assert.True(copied.AllowTriStateSorting);
        UI.ColumnOptions<Item>.CopyCore(new ColumnOptions<Item>(), copied);
        Assert.False(copied.AllowTriStateSorting);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Built_in_native_columns_read_the_live_policy(int kind)
    {
        UI.ColumnOptions<Item> options = kind switch
        {
            0 => new UI.TextColumnOptions<Item>(),
            1 => new UI.CheckBoxColumnOptions<Item>(),
            _ => new UI.TemplateColumnOptions<Item>(),
        };
        using CellColumn column = kind switch
        {
            0 => new UI.TextColumn<Item, string>("Name", x => x.Name, options: (UI.TextColumnOptions<Item>)options),
            1 => new UI.CheckBoxColumn<Item>("Checked", x => x.Checked, options: (UI.CheckBoxColumnOptions<Item>)options),
            _ => new UI.TemplateColumn<Item>("Template", (object)"Deferred", options: (UI.TemplateColumnOptions<Item>)options),
        };
        Assert.False(column.AllowTriStateSorting);
        options.AllowTriStateSorting = true;
        Assert.True(column.AllowTriStateSorting);
        options.AllowTriStateSorting = false;
        Assert.False(column.AllowTriStateSorting);
    }

    [Fact]
    public void Fluent_columns_carry_the_flag_without_leaking_UI_options_into_Core()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("c")]);
        source.WithTextColumn(x => x.Name, options => options.AllowTriStateSorting = true);
        source.WithCheckBoxColumn(x => x.Checked, options => options.AllowTriStateSorting = true);
        source.WithTemplateColumnFromResourceKeys("Template", (object)"Deferred", configure: options => options.AllowTriStateSorting = true);
        using var view = TreeDataGridPresentation.Create(source);
        Assert.All(view.NativeColumns, column => Assert.True(column.AllowTriStateSorting));
        for (var i = 0; i < source.Columns.Count; ++i) Assert.Same(source.Columns[i], view.NativeColumns[i].Model);
        Assert.IsType<ColumnOptions<Item>>(((ValueColumn<Item, string>)source.Columns[0]).Options);
    }

    [Fact]
    public void Declarative_template_factory_copies_the_flag_into_each_view()
    {
        var definition = new TreeDataGridTemplateColumn("Template", "Deferred") { AllowTriStateSorting = true };
        using var generated = DeclarativeSource.Create(new[] { new Item("c") }, [definition]);
        using var first = TreeDataGridPresentation.Create(generated.Source);
        using var second = TreeDataGridPresentation.Create(generated.Source);
        Assert.True(first.NativeColumns[0].AllowTriStateSorting);
        Assert.True(second.NativeColumns[0].AllowTriStateSorting);
        Assert.NotSame(first.NativeColumns[0], second.NativeColumns[0]);
    }

    [Fact]
    public void Expander_delegates_to_its_inner_view_policy()
    {
        using var source = new HierarchicalTreeDataGridSource<Item>([new("c")]);
        source.WithHierarchicalExpanderTextColumn(x => x.Name, x => x.Children,
            options => options.AllowTriStateSorting = true);
        using var view = TreeDataGridPresentation.Create(source);
        Assert.Equal(CellKind.Expander, view.NativeColumns[0].Kind);
        Assert.True(view.NativeColumns[0].AllowTriStateSorting);
    }

    [Fact]
    public void Custom_column_adapter_shares_live_cycle_and_permission_options()
    {
        using var source = Source();
        source.Columns[0].PresentationKey = "custom";
        var policy = new UI.ColumnOptions<Item>();
        var custom = new CustomColumn(policy);
        var options = new TreeDataGridPresentationOptions<Item>();
        options.Columns.Add("custom", _ => custom);
        using var view = TreeDataGridPresentation.Create(source, options);
        var column = view.NativeColumns[0];
        Assert.False(column.AllowTriStateSorting);
        policy.AllowTriStateSorting = true;
        policy.CanUserSortColumn = false;
        Assert.True(column.AllowTriStateSorting);
        Assert.False(column.CanUserSort);
        ((CellColumnBase<Item>)custom).Options.AllowTriStateSorting = false;
        Assert.False(policy.AllowTriStateSorting);
        Assert.False(column.AllowTriStateSorting);
    }

    [Fact]
    public void Clear_sort_restores_source_order_rows_and_shared_selection_in_both_views()
    {
        using var source = Source();
        using var first = TreeDataGridPresentation.Create(source);
        using var second = TreeDataGridPresentation.Create(source);
        var rows = source.Rows;
        var selection = source.RowSelection!;
        selection.SelectedIndex = new IndexPath(1);
        var selected = selection.SelectedItem;
        first.SortBy(first.Columns[0], ListSortDirection.Ascending);
        var firstEvents = 0;
        var secondEvents = 0;
        first.Sorted += () => ++firstEvents;
        second.Sorted += () => ++secondEvents;
        first.ClearSort();
        Assert.False(first.IsSorted);
        Assert.False(second.IsSorted);
        Assert.Equal(new[] { "c", "a", "b" }, rows.Select(row => ((Item)row.Model!).Name));
        Assert.Same(rows, source.Rows);
        Assert.Same(selection, source.RowSelection);
        Assert.Same(selected, selection.SelectedItem);
        Assert.Equal(new IndexPath(1), selection.SelectedIndex);
        Assert.Null(first.Columns[0].SortDirection);
        Assert.Null(second.Columns[0].SortDirection);
        Assert.Equal(1, firstEvents);
        Assert.Equal(1, secondEvents);
        first.ClearSort();
        Assert.Equal(1, firstEvents);
        Assert.Equal(1, secondEvents);
    }

    [Fact]
    public void Clear_sort_uses_the_latest_mutated_source_order_not_an_initial_snapshot()
    {
        var items = new ObservableCollection<Item>([new("c"), new("a"), new("b")]);
        using var source = Source(items);
        using var view = TreeDataGridPresentation.Create(source);
        view.SortBy(view.Columns[0], ListSortDirection.Descending);
        items.Move(0, 2);
        items.Insert(1, new("d"));
        view.ClearSort();
        Assert.Equal(items.ToArray(), source.Rows.Select(row => (Item)row.Model!).ToArray());
    }

    [Fact]
    public void Hierarchical_clear_restores_root_and_child_order_and_preserves_expansion()
    {
        var root = new Item("c");
        root.Children.Add(new("z"));
        root.Children.Add(new("x"));
        var other = new Item("a");
        using var source = new HierarchicalTreeDataGridSource<Item>([root, other]);
        source.Columns.Add(new HierarchicalExpanderColumn<Item>(new TextColumn<Item, string>("Name", x => x.Name), x => x.Children));
        var expanded = (IExpanderRow<Item>)source.Rows[0];
        expanded.IsExpanded = true;
        source.RowSelection!.SelectedIndex = new IndexPath(0, 1);
        var selected = source.RowSelection.SelectedItem;
        using var view = TreeDataGridPresentation.Create(source);
        view.SortBy(view.Columns[0], ListSortDirection.Ascending);
        Assert.Equal(new[] { "a", "c", "x", "z" }, source.Rows.Select(row => ((Item)row.Model!).Name));
        view.ClearSort();
        Assert.Equal(new[] { "c", "z", "x", "a" }, source.Rows.Select(row => ((Item)row.Model!).Name));
        Assert.True(expanded.IsExpanded);
        Assert.Same(expanded, source.Rows[0]);
        Assert.Same(selected, source.RowSelection.SelectedItem);
        Assert.Equal(new IndexPath(0, 1), source.RowSelection.SelectedIndex);
    }

    [Fact]
    public void Disposed_view_cannot_clear_the_borrowed_sources_sort()
    {
        using var source = Source();
        var view = TreeDataGridPresentation.Create(source);
        view.SortBy(view.Columns[0], ListSortDirection.Ascending);
        view.Dispose();
        Assert.Throws<ObjectDisposedException>(() => view.ClearSort());
        Assert.True(source.IsSorted);
        Assert.Equal(3, source.Rows.Count);
    }

    private static FlatTreeDataGridSource<Item> Source(ObservableCollection<Item>? items = null)
    {
        var source = new FlatTreeDataGridSource<Item>(items ?? new ObservableCollection<Item>([new("c"), new("a"), new("b")]));
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name));
        return source;
    }
    private sealed class Definition : TreeDataGridTextColumn
    {
        public UI.ColumnOptions<object> Capture() => CreateCommonOptions();
    }
    private sealed class CustomColumn(UI.ColumnOptions<Item> options) : UI.ColumnBase<Item>("Custom", null, options)
    {
        public override Comparison<Item?>? GetComparison(ListSortDirection direction) => null;
        public override UI.ICell CreateCell(IRow<Item> row) => throw new InvalidOperationException("The options test does not realize cells.");
    }
    private sealed class Item(string name)
    {
        public string Name { get; set; } = name;
        public bool Checked { get; set; }
        public ObservableCollection<Item> Children { get; } = new();
    }
}
