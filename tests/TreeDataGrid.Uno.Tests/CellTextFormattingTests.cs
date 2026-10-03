using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;
using ITextCell = global::Uno.Controls.Models.TreeDataGrid.ITextCell;

namespace TreeDataGrid.Uno.Tests;

public sealed class CellTextFormattingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("already formatted")]
    [InlineData("Zażółć gęślą jaźń — Ω\n\0\uD83D\uDE80")]
    public void Identity_format_preserves_text_and_standard_cultures(string? text)
    {
        foreach (var culture in new CultureInfo?[] { null, CultureInfo.InvariantCulture, new("pl-PL"), new("tr-TR") })
        {
            var actual = CellTextFormatting.Format(culture, "{0}", text);
            Assert.Equal(string.Format(culture, "{0}", (object?)text), actual);
            Assert.Same(text ?? string.Empty, actual);
        }
    }

    [Theory]
    [InlineData("{0,20}")]
    [InlineData("{0,-20}")]
    [InlineData("prefix {0} suffix")]
    [InlineData("{{{0}}}")]
    [InlineData("{0:N3}")]
    [InlineData("{0}{0}")]
    [InlineData("literal only")]
    public void Nonidentity_formats_match_the_runtime(string format)
    {
        foreach (var culture in new[] { CultureInfo.InvariantCulture, new CultureInfo("pl-PL") })
            foreach (var value in new object?[] { null, "example", 12345.625, 17m })
                Assert.Equal(string.Format(culture, format, value), CellTextFormatting.Format(culture, format, value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{")]
    [InlineData("{1}")]
    [InlineData("{0,}")]
    public void Invalid_formats_preserve_exception_type_and_argument(string? format)
    {
        var expected = Record.Exception(() => string.Format(CultureInfo.InvariantCulture, format!, (object)"value"));
        var actual = Record.Exception(() => CellTextFormatting.Format(CultureInfo.InvariantCulture, format!, "value"));
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal((expected as ArgumentException)?.ParamName, (actual as ArgumentException)?.ParamName);
    }

    [Fact]
    public void Derived_culture_formatter_runs_for_string_and_null_on_every_call()
    {
        var culture = new CustomCulture();
        Assert.Equal("custom:text", CellTextFormatting.Format(culture, "{0}", "text"));
        Assert.Equal("custom:<null>", CellTextFormatting.Format(culture, "{0}", null));
        culture.Prefix = "changed:";
        Assert.Equal("changed:text", CellTextFormatting.Format(culture, "{0}", "text"));
        Assert.Equal(3, culture.Calls);
        var failure = new InvalidOperationException("custom formatter");
        culture.Failure = failure;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => CellTextFormatting.Format(culture, "{0}", "text")));
    }

    [Fact]
    public void Nonstring_formattable_is_not_bypassed_or_memoized()
    {
        var value = new Formattable();
        var culture = CultureInfo.GetCultureInfo("pl-PL");
        Assert.Equal("first", CellTextFormatting.Format(culture, "{0}", value));
        value.Text = "second";
        Assert.Equal("second", CellTextFormatting.Format(culture, "{0}", value));
        Assert.Equal(2, value.Calls);
        Assert.Same(culture, value.Provider);
    }

    [Fact]
    public void Column_rendering_search_and_public_cell_text_use_the_same_format_contract()
    {
        var item = new Item("text");
        using var source = new FlatTreeDataGridSource<Item>([item]);
        var definition = new ValueColumn<Item, string>("Text", x => x.Name);
        source.Columns.Add(definition);
        var culture = new CustomCulture();
        using var view = new ValueCellColumn<Item, string>(definition, CellKind.Text,
            new TextCellOptions { Culture = culture, StringFormat = "{0}", IsTextSearchEnabled = true });
        using var cell = view.CreateCell(source.Rows[0]);
        Assert.Equal("custom:text", view.FormatValue(item.Name));
        Assert.Equal("custom:text", view.GetSearchText(item));
        Assert.Equal("custom:text", Assert.IsAssignableFrom<ITextCell>(cell).Text);
        Assert.Equal(3, culture.Calls);
    }

    [Fact]
    public void Warm_rendering_search_and_public_cell_text_allocate_no_copied_strings()
    {
        var item = new Item(new string('x', 64));
        using var source = new FlatTreeDataGridSource<Item>([item]);
        var definition = new ValueColumn<Item, string>("Text", x => x.Name);
        source.Columns.Add(definition);
        using var view = new ValueCellColumn<Item, string>(definition, CellKind.Text,
            new TextCellOptions { Culture = CultureInfo.InvariantCulture, IsTextSearchEnabled = true });
        using var cell = view.CreateCell(source.Rows[0]);
        var textCell = Assert.IsAssignableFrom<ITextCell>(cell);
        for (var i = 0; i < 1024; ++i)
        {
            view.FormatValue(item.Name); view.GetSearchText(item); _ = textCell.Text;
        }
        var characters = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i)
            characters += view.FormatValue(item.Name).Length + view.GetSearchText(item)!.Length + textCell.Text!.Length;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(4096 * 3 * 64, characters);
    }

    [Fact]
    public void Observed_bound_cell_retarget_preserves_event_order_without_event_argument_allocations()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("first"), new("second")]);
        var definition = new ValueColumn<Item, string>("Text", x => x.Name);
        source.Columns.Add(definition);
        using var view = new ValueCellColumn<Item, string>(definition, CellKind.Text);
        using var cell = view.CreateCell(source.Rows[0]);
        var notifications = new List<string?>();
        PropertyChangedEventHandler verifyOrder = (_, args) => notifications.Add(args.PropertyName);
        cell.PropertyChanged += verifyOrder;
        Assert.True(view.TryReuseCell(cell, (IRow<Item>)source.Rows[1]));
        Assert.Equal(new[] { "Value", "Error" }, notifications);
        cell.PropertyChanged -= verifyOrder;
        var calls = 0;
        PropertyChangedEventHandler handler = (sender, args) => ++calls;
        cell.PropertyChanged += handler;
        try
        {
            for (var i = 0; i < 1024; ++i) view.TryReuseCell(cell, (IRow<Item>)source.Rows[i & 1]);
            calls = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 4096; ++i) view.TryReuseCell(cell, (IRow<Item>)source.Rows[i & 1]);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0L, allocated);
            Assert.Equal(8192, calls);
            Assert.Equal("second", cell.Value);
        }
        finally { cell.PropertyChanged -= handler; }
    }

    private sealed record Item(string Name);
    private sealed class Formattable : IFormattable
    {
        internal string Text = "first";
        internal int Calls;
        internal IFormatProvider? Provider;
        public string ToString(string? format, IFormatProvider? provider) { ++Calls; Provider = provider; return Text; }
        public override string ToString() => throw new InvalidOperationException("IFormattable dispatch was bypassed.");
    }
    private sealed class CustomCulture() : CultureInfo("en-US"), ICustomFormatter
    {
        internal int Calls;
        internal string Prefix = "custom:";
        internal Exception? Failure;
        public override object? GetFormat(Type? formatType) => formatType == typeof(ICustomFormatter) ? this : base.GetFormat(formatType);
        public string Format(string? format, object? arg, IFormatProvider? formatProvider)
        {
            ++Calls;
            if (Failure is { } failure) throw failure;
            return Prefix + (arg?.ToString() ?? "<null>");
        }
    }
}
