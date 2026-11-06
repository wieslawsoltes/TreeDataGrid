using System;
using System.Globalization;
using Xunit;
using Native = global::Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class TextSearchValueContractTests
{
    [Fact]
    public void Search_does_not_evaluate_the_display_formatter()
    {
        var options = new Native.TextColumnOptions<Item>
        {
            StringFormat = "Display {0}", Culture = new ThrowingDisplayCulture(),
        };
        using var column = new Native.TextColumn<Item, string?>("Text", row => row.Text, options: options);
        var model = new Item { Text = "raw" };
        Assert.Equal("raw", column.GetSearchText(model));
        Assert.Equal("raw", ((Native.ITextSearchableColumn<Item>)column).SelectValue(model));
        Assert.Throws<InvalidOperationException>(() => column.FormatValue("raw"));
    }

    [Fact]
    public void Null_search_values_remain_null_not_a_formatted_empty_string()
    {
        using var column = new Native.TextColumn<Item, string?>("Text", row => row.Text,
            options: new() { StringFormat = "[{0}]" });
        Assert.Null(column.GetSearchText(new Item()));
        Assert.Null(((Native.ITextSearchableColumn<Item>)column).SelectValue(new Item()));
        Assert.Equal("[]", column.FormatValue(null));
    }

    [Fact]
    public void An_explicit_search_selector_overrides_the_raw_value()
    {
        var model = new Item { Text = "raw" };
        var calls = 0;
        using var column = new Native.TextColumn<Item, string?>("Text", row => row.Text)
        {
            TextSearchValueSelector = value => { Assert.Same(model, value); ++calls; return "custom"; },
        };
        Assert.True(column.IsTextSearchEnabled);
        Assert.Equal("custom", ((Native.ITextSearchableColumn<Item>)column).SelectValue(model));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Warm_string_search_preserves_identity_without_allocation()
    {
        var model = new Item { Text = new string('x', 12) };
        using var column = new Native.TextColumn<Item, string?>("Text", row => row.Text,
            options: new() { StringFormat = "Display {0}" });
        for (var index = 0; index < 1024; ++index) column.GetSearchText(model);
        var matches = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 4096; ++index)
            if (ReferenceEquals(model.Text, column.GetSearchText(model))) ++matches;
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(4096, matches);
    }

    private sealed class Item { public string? Text { get; init; } }
    private sealed class ThrowingDisplayCulture() : CultureInfo("en-US")
    {
        public override object? GetFormat(Type? formatType) => throw new InvalidOperationException("Display formatting invoked");
    }
}
