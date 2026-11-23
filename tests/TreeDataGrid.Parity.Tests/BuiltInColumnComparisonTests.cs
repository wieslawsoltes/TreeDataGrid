using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = global::Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Parity.Tests;

public sealed class BuiltInColumnComparisonTests
{
    [Theory]
    [InlineData(ListSortDirection.Ascending)]
    [InlineData(ListSortDirection.Descending)]
    public void Numeric_text_comparisons_match_actual_reference_for_nulls_and_values(ListSortDirection direction)
    {
        var a = new A.TextColumn<Item, int?>("Number", item => item.Number);
        var u = new U.TextColumn<Item, int?>("Number", item => item.Number);
        CompareAll(a.GetComparison(direction)!, u.GetComparison(direction)!,
            [null, new() { Number = null }, new() { Number = int.MinValue }, new() { Number = 0 }, new() { Number = int.MaxValue }]);
        Assert.Same(u.GetComparison(direction), u.GetComparison(direction));
    }

    [Theory]
    [InlineData(ListSortDirection.Ascending)]
    [InlineData(ListSortDirection.Descending)]
    public void Nullable_checkbox_comparisons_and_selectors_match_reference(ListSortDirection direction)
    {
        var a = new A.CheckBoxColumn<Item>("Flag", item => item.Flag);
        var u = new U.CheckBoxColumn<Item>("Flag", item => item.Flag);
        var items = new Item?[] { null, new() { Flag = null }, new() { Flag = false }, new() { Flag = true } };
        CompareAll(a.GetComparison(direction)!, u.GetComparison(direction)!, items);
        Assert.True(a.IsThreeState);
        Assert.Equal(a.IsThreeState, u.IsThreeState);
        foreach (var item in items)
            if (item is not null) Assert.Equal(a.ValueSelector(item), u.ValueSelector(item));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Boolean_checkbox_constructors_keep_two_state_selectors(bool flag)
    {
        var a = new A.CheckBoxColumn<Item>("Flag", item => item.Boolean);
        var u = new U.CheckBoxColumn<Item>("Flag", item => item.Boolean);
        var item = new Item { Boolean = flag };
        Assert.False(a.IsThreeState);
        Assert.Equal(a.IsThreeState, u.IsThreeState);
        Assert.Equal(a.ValueSelector(item), u.ValueSelector(item));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void Value_comparisons_capture_policy_at_construction(bool? allowed)
    {
        Comparison<Item?> ascending = static (_, _) => 17;
        Comparison<Item?> descending = static (_, _) => -23;
        var ao = new A.TextColumnOptions<Item> { CanUserSortColumn = allowed, CompareAscending = ascending, CompareDescending = descending };
        var uo = new U.TextColumnOptions<Item> { CanUserSortColumn = allowed, CompareAscending = ascending, CompareDescending = descending };
        var a = new A.TextColumn<Item, int?>("Number", item => item.Number, options: ao);
        var u = new U.TextColumn<Item, int?>("Number", item => item.Number, options: uo);
        ao.CompareAscending = uo.CompareAscending = static (_, _) => 99;
        ao.CompareDescending = uo.CompareDescending = null;
        ao.CanUserSortColumn = uo.CanUserSortColumn = allowed != true;
        foreach (var direction in new[] { ListSortDirection.Ascending, ListSortDirection.Descending, (ListSortDirection)42 })
        {
            var expected = a.GetComparison(direction);
            var actual = u.GetComparison(direction);
            Assert.Same(expected, actual);
            if (actual is not null) Assert.Equal(expected!(null, null), actual(null, null));
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void Invalid_direction_never_falls_back_to_descending(int direction)
    {
        var a = new A.CheckBoxColumn<Item>("Flag", item => item.Flag);
        var u = new U.CheckBoxColumn<Item>("Flag", item => item.Flag);
        Assert.Null(a.GetComparison((ListSortDirection)direction));
        Assert.Null(u.GetComparison((ListSortDirection)direction));
    }

    [Fact]
    public void Public_text_selector_is_raw_and_keeps_the_original_core_getter_identity()
    {
        var ao = new A.TextColumnOptions<Item> { StringFormat = "[value {0}]", Culture = CultureInfo.InvariantCulture };
        var uo = new U.TextColumnOptions<Item> { StringFormat = "[value {0}]", Culture = CultureInfo.InvariantCulture };
        var a = new A.TextColumn<Item, string?>("Name", item => item.Name, options: ao);
        var definition = new TreeDataGridCore.Models.ValueColumn<Item, string?>("Name", item => item.Name);
        var u = new U.TextColumn<Item, string?>(definition, uo);
        var item = new Item { Name = "Raw value" };
        Assert.Equal(a.ValueSelector(item), u.ValueSelector(item));
        Assert.Same(definition.Getter, u.ValueSelector);
        Assert.Same(u.ValueSelector, u.ValueSelector);
        Assert.Equal("[value Raw value]", u.FormatValue(item.Name));
        item.Name = null;
        Assert.Null(a.ValueSelector(item));
        Assert.Null(u.ValueSelector(item));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void Template_comparisons_read_live_delegates_independently_of_ui_sort_policy(bool? allowed)
    {
        var ao = new A.TemplateColumnOptions<Item> { CanUserSortColumn = allowed };
        var uo = new U.TemplateColumnOptions<Item> { CanUserSortColumn = allowed };
        var a = new A.TemplateColumn<Item>("Item", (object)"Display", options: ao);
        var u = new U.TemplateColumn<Item>("Item", (object)"Display", options: uo);
        Assert.Null(a.GetComparison(ListSortDirection.Ascending));
        Assert.Null(u.GetComparison(ListSortDirection.Ascending));
        Comparison<Item?> first = static (_, _) => 13;
        Comparison<Item?> second = static (_, _) => -31;
        ao.CompareAscending = uo.CompareAscending = first;
        ao.CompareDescending = uo.CompareDescending = second;
        Assert.Same(a.GetComparison(ListSortDirection.Ascending), u.GetComparison(ListSortDirection.Ascending));
        Assert.Same(a.GetComparison(ListSortDirection.Descending), u.GetComparison(ListSortDirection.Descending));
        ao.CompareAscending = uo.CompareAscending = second;
        ao.CompareDescending = uo.CompareDescending = null;
        Assert.Same(second, u.GetComparison(ListSortDirection.Ascending));
        Assert.Same(a.GetComparison(ListSortDirection.Ascending), u.GetComparison(ListSortDirection.Ascending));
        Assert.Null(u.GetComparison(ListSortDirection.Descending));
        Assert.Null(u.GetComparison((ListSortDirection)42));
    }

    [Fact]
    public void Separate_view_policy_does_not_overwrite_core_source_sorting()
    {
        Comparison<Item?> coreComparison = static (_, _) => 5;
        Comparison<Item?> viewComparison = static (_, _) => 9;
        var definition = new TreeDataGridCore.Models.ValueColumn<Item, int?>("Number", item => item.Number,
            options: new() { CompareAscending = coreComparison });
        var view = new U.TextColumn<Item, int?>(definition, new() { CompareAscending = viewComparison });
        Assert.Same(definition, view.Model);
        Assert.Same(viewComparison, view.GetComparison(ListSortDirection.Ascending));
        Assert.Same(coreComparison, definition.GetComparison(ListSortDirection.Ascending));
    }

    [Fact]
    public void Noncomparable_values_fail_only_when_default_comparison_is_executed()
    {
        var a = new A.TextColumn<Item, object?>("Payload", item => item.Payload);
        var u = new U.TextColumn<Item, object?>("Payload", item => item.Payload);
        var first = new Item { Payload = new object() };
        var second = new Item { Payload = new object() };
        var expected = Record.Exception(() => a.GetComparison(ListSortDirection.Ascending)!(first, second));
        var actual = Record.Exception(() => u.GetComparison(ListSortDirection.Ascending)!(first, second));
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
    }

    [Fact]
    public void Warm_comparison_queries_and_string_comparisons_allocate_no_managed_storage()
    {
        var view = new U.TextColumn<Item, string?>("Name", item => item.Name);
        var first = new Item { Name = "Alpha" };
        var second = new Item { Name = "Beta" };
        var asc = view.GetComparison(ListSortDirection.Ascending)!;
        var desc = view.GetComparison(ListSortDirection.Descending)!;
        for (var i = 0; i < 1024; ++i) { asc(first, second); desc(first, second); }
        var checksum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i)
        {
            checksum += Math.Sign(view.GetComparison(ListSortDirection.Ascending)!(first, second));
            checksum += Math.Sign(view.GetComparison(ListSortDirection.Descending)!(first, second));
            if (!ReferenceEquals(view.ValueSelector(first), first.Name)) ++checksum;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(0, checksum);
        Assert.Same(asc, view.GetComparison(ListSortDirection.Ascending));
        Assert.Same(desc, view.GetComparison(ListSortDirection.Descending));
    }

    private static void CompareAll(Comparison<Item?> expected, Comparison<Item?> actual, IReadOnlyList<Item?> items)
    {
        foreach (var first in items)
            foreach (var second in items)
                Assert.Equal(Math.Sign(expected(first, second)), Math.Sign(actual(first, second)));
    }
    private sealed class Item
    {
        public int? Number { get; set; }
        public bool? Flag { get; set; }
        public bool Boolean { get; set; }
        public string? Name { get; set; }
        public object? Payload { get; set; }
    }
}
