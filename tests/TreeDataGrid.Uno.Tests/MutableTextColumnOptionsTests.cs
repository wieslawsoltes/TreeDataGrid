using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;
using Native = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class MutableTextColumnOptionsTests
{
    [Fact]
    public void Existing_and_reused_cells_read_the_current_options()
    {
        var first = new Item { Number = 12.5m };
        var second = new Item { Number = 8.25m };
        using var source = new FlatTreeDataGridSource<Item>([first, second]);
        var options = new Native.TextColumnOptions<Item> { StringFormat = "N={0:F1}", Culture = CultureInfo.InvariantCulture };
        using var column = new Native.TextColumn<Item, decimal>("Number", row => row.Number, options: options);
        using var cell = column.CreateCell(source.Rows[0]);
        var text = Assert.IsAssignableFrom<Native.ITextCell>(cell);
        Assert.Equal("N=12.5", text.Text);
        options.StringFormat = "V={0:F2}";
        options.Culture = CultureInfo.GetCultureInfo("fr-FR");
        options.TextAlignment = TextAlignment.Right;
        options.TextWrapping = TextWrapping.Wrap;
        options.TextTrimming = TextTrimming.WordEllipsis;
        options.BeginEditGestures = Native.BeginEditGestures.F2;
        Assert.Equal("V=12,50", text.Text);
        Assert.Equal("V=12,50", column.FormatValue(cell.Value));
        Assert.Equal(TextAlignment.Right, text.TextAlignment);
        Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
        Assert.Equal(TextTrimming.WordEllipsis, text.TextTrimming);
        Assert.Equal(Native.BeginEditGestures.F2, cell.EditGestures);
        Assert.True(column.TryReuseCell(cell, (IRow<Item>)source.Rows[1]));
        Assert.Equal("V=8,25", text.Text);
    }

    [Fact]
    public void Search_policy_and_render_snapshots_follow_mutable_configuration()
    {
        var options = new Native.TextColumnOptions<Item>();
        using var column = new Native.TextColumn<Item, string>("Text", row => row.Text, options: options);
        var snapshot = column.TextOptions;
        Assert.False(column.IsTextSearchEnabled);
        options.IsTextSearchEnabled = true;
        options.TextWrapping = TextWrapping.Wrap;
        Assert.True(column.IsTextSearchEnabled);
        var updated = column.TextOptions;
        Assert.NotSame(snapshot, updated);
        Assert.False(snapshot.IsTextSearchEnabled);
        Assert.True(updated.IsTextSearchEnabled);
        Assert.Equal(TextWrapping.Wrap, updated.TextWrapping);
        Assert.Same(updated, column.TextOptions);
    }

    [Fact]
    public void Edited_values_use_the_current_culture_without_replacing_the_binding()
    {
        var model = new Item();
        using var source = new FlatTreeDataGridSource<Item>([model]);
        var options = new Native.TextColumnOptions<Item> { Culture = CultureInfo.InvariantCulture };
        using var column = new Native.TextColumn<Item, decimal>("Number", row => row.Number,
            (row, value) => row.Number = value, options: options);
        using var cell = column.CreateCell(source.Rows[0]);
        options.Culture = CultureInfo.GetCultureInfo("fr-FR");
        cell.Write("12,5");
        Assert.Equal(12.5m, model.Number);
        options.Culture = CultureInfo.InvariantCulture;
        cell.Write("8.25");
        Assert.Equal(8.25m, model.Number);
    }

    [Fact]
    public void Null_format_retains_unformatted_null_and_string_contracts()
    {
        var model = new Item { Text = null! };
        using var source = new FlatTreeDataGridSource<Item>([model]);
        var options = new Native.TextColumnOptions<Item>();
        using var column = new Native.TextColumn<Item, string>("Text", row => row.Text, options: options);
        using var cell = column.CreateCell(source.Rows[0]);
        var text = Assert.IsAssignableFrom<Native.ITextCell>(cell);
        Assert.Equal(string.Empty, text.Text);
        options.StringFormat = null!;
        Assert.Null(text.Text);
        Assert.Equal(string.Empty, column.FormatValue(null));
        Assert.Equal("Original", column.FormatValue("Original"));
    }

    [Fact]
    public void Read_only_cells_remain_read_only_after_option_updates()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var options = new Native.TextColumnOptions<Item>();
        using var column = new Native.TextColumn<Item, string>("Text", row => row.Text, options: options);
        using var cell = column.CreateCell(source.Rows[0]);
        options.Culture = CultureInfo.GetCultureInfo("pl-PL");
        options.StringFormat = "[{0}]";
        Assert.False(cell.CanWrite);
        Assert.Throws<InvalidOperationException>(() => cell.Write("new"));
    }

    [Fact]
    public void Shared_options_update_distinct_columns_without_sharing_cell_identity()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var options = new Native.TextColumnOptions<Item>();
        using var first = new Native.TextColumn<Item, string>("First", row => row.Text, options: options);
        using var second = new Native.TextColumn<Item, string>("Second", row => row.Text, options: options);
        using var a = first.CreateCell(source.Rows[0]);
        using var b = second.CreateCell(source.Rows[0]);
        options.StringFormat = "[{0}]";
        Assert.NotSame(a, b);
        Assert.Equal("[text]", ((Native.ITextCell)a).Text);
        Assert.Equal("[text]", ((Native.ITextCell)b).Text);
        Assert.False(first.TryReuseCell(b, (IRow<Item>)source.Rows[0]));
    }

    [Fact]
    public void Unchanged_live_options_have_no_warmed_query_allocation()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var options = new Native.TextColumnOptions<Item> { Culture = CultureInfo.InvariantCulture };
        using var column = new Native.TextColumn<Item, string>("Text", row => row.Text, options: options);
        using var cell = column.CreateCell(source.Rows[0]);
        var text = (Native.ITextCell)cell;
        for (var index = 0; index < 1024; ++index) { _ = cell.TextOptions; _ = text.Text; }
        var expected = column.TextOptions;
        var matches = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 4096; ++index)
            if (ReferenceEquals(expected, cell.TextOptions) && text.Text == "text") ++matches;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(4096, matches);
    }

    private sealed class Item
    {
        public string Text { get; set; } = "text";
        public decimal Number { get; set; }
    }
}
