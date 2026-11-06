using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using TreeDataGridCore;
using Uno.Controls;
using Uno.Controls.Presentation;
using Core = TreeDataGridCore.Models;
using UI = Uno.Controls.Models.TreeDataGrid;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

// Authored during implementation; execution is deferred to the parity validation phase.
public class SourceExtensionsTests
{
    [Fact]
    public void Fluent_columns_keep_Core_identity_and_infer_editable_member_bindings()
    {
        var item = new Item { Name = "original", Number = 12, Checked = false };
        using var source = new FlatTreeDataGridSource<Item>([item]);
        Assert.Same(source, source.WithTextColumn(x => x.Name)
            .WithTextColumn("Formatted", x => x.Number, o => { o.StringFormat = "N={0}"; o.Culture = CultureInfo.InvariantCulture; })
            .WithCheckBoxColumn(x => x.Checked)
            .WithThreeStateCheckBoxColumn(x => x.NullableChecked));
        using var view = TreeDataGridPresentation.Create(source);
        Assert.Equal("Name", source.Columns[0].Header);
        Assert.Same(source, view.Model);
        Assert.Same(source.Columns[0], view.NativeColumns[0].Model);
        Assert.IsType<Core.ColumnOptions<Item>>(((Core.ValueColumn<Item, string?>)source.Columns[0]).Options);
        using var text = view.RealizeCell(0, 0);
        using var number = view.RealizeCell(1, 0);
        using var two = view.RealizeCell(2, 0);
        using var three = view.RealizeCell(3, 0);
        text.Write("edited");
        number.Write("34");
        two.Write(true);
        three.Write(null);
        Assert.Equal("edited", item.Name);
        Assert.Equal(34, item.Number);
        Assert.Equal("N=34", view.NativeColumns[1].FormatValue(number.Value));
        Assert.True(item.Checked);
        Assert.Null(item.NullableChecked);
        Assert.False(view.NativeColumns[2].IsThreeState);
        Assert.True(view.NativeColumns[3].IsThreeState);
        Assert.True(view.NativeColumns[0].IsTextSearchEnabled);
    }

    [Fact]
    public void Readonly_and_computed_getters_are_not_given_setters()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        source.WithTextColumn(x => x.Name, o => o.IsReadOnly = true)
            .WithTextColumn(x => x.Number + 1)
            .WithCheckBoxColumn(x => x.Checked, o => o.IsReadOnly = true)
            .WithThreeStateCheckBoxColumn(x => x.NullableChecked, o => o.IsReadOnly = true);
        using var view = TreeDataGridPresentation.Create(source);
        for (var i = 0; i < 4; ++i)
        {
            using var cell = view.RealizeCell(i, 0);
            Assert.False(cell.CanEdit);
        }
    }

    [Fact]
    public void Shared_source_creates_independent_views_and_measurement_state()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        source.WithTextColumn(x => x.Name);
        using var first = TreeDataGridPresentation.Create(source);
        using var second = TreeDataGridPresentation.Create(source);
        Assert.NotSame(first.Columns[0], second.Columns[0]);
        first.NativeColumns[0].CellMeasured(200, 0);
        first.NativeColumns[0].CommitActualWidth();
        second.NativeColumns[0].CommitActualWidth();
        Assert.Equal(200, first.Columns[0].ActualWidth);
        Assert.Equal(30, second.Columns[0].ActualWidth);
        first.Dispose();
        using var remaining = second.RealizeCell(0, 0);
        Assert.Equal("name", remaining.Value);
    }

    [Fact]
    public void Explicit_factory_overrides_fluent_presentation_and_key_changes_do_not_reuse_stale_metadata()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        source.WithCheckBoxColumn(x => x.Checked);
        var options = new TreeDataGridPresentationOptions<Item>();
        options.Columns["CheckBox"] = _ => new UI.TextColumn<Item, string>("Override", x => x.Name);
        using var view = TreeDataGridPresentation.Create(source, options);
        Assert.Equal(CellKind.Text, view.NativeColumns[0].Kind);
        Assert.Equal("Override", view.NativeColumns[0].Header);
        using var textSource = new FlatTreeDataGridSource<Item>([new()]);
        textSource.WithTextColumn(x => x.Name);
        textSource.Columns[0].PresentationKey = "Other";
        Assert.Throws<InvalidOperationException>(() => TreeDataGridPresentation.Create(textSource));
    }

    [Fact]
    public void Row_headers_capture_flyweight_rows_and_show_model_indexes_after_sorting()
    {
        using var source = new FlatTreeDataGridSource<Item>([new() { Name = "z" }, new() { Name = "a" }]);
        source.WithRowHeaderColumn().WithTextColumn(x => x.Name);
        using var view = TreeDataGridPresentation.Create(source);
        using var first = view.RealizeCell(0, 0);
        using var second = view.RealizeCell(0, 1);
        Assert.Equal("1", first.Value);
        Assert.Equal("2", second.Value);
        Assert.Null(source.Columns[0].GetComparison(ListSortDirection.Ascending));
        source.SortBy(source.Columns[1], ListSortDirection.Ascending);
        var sortedFirst = view.RealizeCell(0, 0);
        Assert.Equal("2", sortedFirst.Value);
        Assert.False(sortedFirst.CanEdit);
        view.RecycleCell(view.NativeColumns[0], sortedFirst);
        using var recycled = view.RealizeCell(0, 1);
        Assert.Same(sortedFirst, recycled);
        Assert.Equal("1", recycled.Value);
    }

    [Fact]
    public void Hierarchical_fluent_columns_forward_policy_and_number_each_sibling_group()
    {
        var parent = new Item { Children = [new(), new()] };
        using var source = new HierarchicalTreeDataGridSource<Item>([parent]);
        source.WithRowHeaderColumn().WithHierarchicalExpanderTextColumn(x => x.Name, x => x.Children, o =>
        {
            o.MinWidth = new(80); o.MaxWidth = new(120); o.CanUserSortColumn = false;
            o.BeginEditGestures = UI.BeginEditGestures.Tap;
            o.HasChildren = x => x.Children.Count > 0;
            o.IsExpanded = x => x.Expanded;
        });
        using var view = TreeDataGridPresentation.Create(source);
        Assert.Equal(80, view.NativeColumns[1].MinimumWidth);
        Assert.Equal(120, view.NativeColumns[1].MaximumWidth);
        Assert.False(view.NativeColumns[1].CanUserSort);
        using var cell = view.RealizeCell(1, 0);
        var expander = Assert.IsAssignableFrom<ExpanderCellValue>(cell);
        Assert.Equal(UI.BeginEditGestures.Tap, expander.Inner.EditGestures);
        expander.IsExpanded = true;
        Assert.True(parent.Expanded);
        Assert.Equal(3, source.Rows.Count);
        using var childOne = view.RealizeCell(0, 1);
        using var childTwo = view.RealizeCell(0, 2);
        Assert.Equal("1", childOne.Value);
        Assert.Equal("2", childTwo.Value);
    }

    [Fact]
    public void Template_descriptor_builds_actual_Core_expander_and_keeps_inner_width()
    {
        using var source = new HierarchicalTreeDataGridSource<Item>([new()]);
        source.WithHierarchicalExpanderColumn("Outer", new TreeDataGridTemplateColumn("Inner", "Display", "Editor")
            { Width = new(150) }, x => x.Children, o => o.MinWidth = new(60));
        var core = Assert.IsType<Core.HierarchicalExpanderColumn<Item>>(source.Columns[0]);
        Assert.IsType<Core.TemplateColumn<Item>>(core.Inner);
        using var view = TreeDataGridPresentation.Create(source);
        Assert.Equal("Outer", view.NativeColumns[0].Header);
        Assert.Equal(CellKind.Template, view.NativeColumns[0].ContentKind);
        Assert.Equal(150, view.NativeColumns[0].Width.Value);
        Assert.Equal(60, view.NativeColumns[0].MinimumWidth);
    }

    [Fact]
    public void Weak_registrations_do_not_globally_retain_discarded_Core_columns()
    {
        var weak = CreateDiscardedColumn();
        for (var i = 0; i < 3; ++i) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(weak.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDiscardedColumn()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        source.WithTextColumn(x => x.Name);
        using var view = TreeDataGridPresentation.Create(source);
        return new WeakReference(source.Columns[0]);
    }

    private sealed class Item
    {
        public string Name { get; set; } = "name";
        public int Number { get; set; }
        public bool Checked { get; set; }
        public bool? NullableChecked { get; set; }
        public bool Expanded { get; set; }
        public List<Item> Children { get; set; } = [];
    }
}
