using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class NumericTextFormattingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Identity_numeric_text_avoids_the_boxed_format_argument(int route)
    {
        var row = new Row();
        using var nativeColumn = new U.TextColumn<Row, int>("Number", x => x.Number,
            options: new() { Culture = CultureInfo.InvariantCulture });
        var definition = new ValueColumn<Row, int>("Number", x => x.Number);
        using var viewColumn = new ValueCellColumn<Row, int>(definition, CellKind.Text,
            new TextCellOptions { Culture = CultureInfo.InvariantCulture });
        using var source = new FlatTreeDataGridSource<Row>([row]);
        source.Columns.Add(definition);
        using var scalar = new U.TextCell<int>(new Once<int>(row.Number), true,
            new U.TextColumnOptions<Row> { Culture = CultureInfo.InvariantCulture });
        using var native = nativeColumn.CreateCell(source.Rows[0]);
        using var core = viewColumn.CreateCell(source.Rows[0]);
        var cell = route switch { 0 => (U.ITextCell)scalar, 1 => (U.ITextCell)native, _ => (U.ITextCell)core };
        for (var i = 0; i < 2048; ++i) { _ = cell.Text; _ = Baseline(row.Number); }
        long actualChars = 0, expectedChars = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) expectedChars += Baseline(row.Number).Length;
        var expectedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) actualChars += cell.Text!.Length;
        var actualBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(expectedChars, actualChars);
        Assert.Equal("123456789", cell.Text);
        if (RuntimeFeature.IsDynamicCodeCompiled)
            Assert.True(actualBytes < expectedBytes, $"Route {route}: numeric text allocated {actualBytes} versus boxed formatting {expectedBytes}.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bound_text_uses_current_culture_when_options_omit_it(bool nativeFacade)
    {
        var previous = CultureInfo.CurrentCulture;
        using var source = new FlatTreeDataGridSource<Row>([new Row()]);
        var definition = new ValueColumn<Row, int>("Number", x => x.Number);
        source.Columns.Add(definition);
        using CellColumn column = nativeFacade
            ? new U.TextColumn<Row, int>(definition, new() { Culture = null! })
            : new ValueCellColumn<Row, int>(definition, CellKind.Text, new TextCellOptions { Culture = null! });
        using var model = column.CreateCell(source.Rows[0]);
        var text = (U.ITextCell)model;
        try
        {
            CultureInfo.CurrentCulture = new CustomCulture { Prefix = "first:" };
            Assert.Equal("first:123456789", text.Text);
            CultureInfo.CurrentCulture = new CustomCulture { Prefix = "second:" };
            Assert.Equal("second:123456789", text.Text);
            Assert.Equal("second:123456789", column.FormatValue(123456789));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Live_number_format_changes_are_observed_without_caching_the_result()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        var options = new U.TextColumnOptions<Row> { Culture = culture };
        using var cell = new U.TextCell<int>(new Once<int>(-123456789), true, options);
        Assert.Equal("-123456789", cell.Text);
        culture.NumberFormat.NegativeSign = "minus:";
        Assert.Equal("minus:123456789", cell.Text);
        options.StringFormat = "[{0:N2}]";
        Assert.Equal(string.Format(culture, options.StringFormat, -123456789), cell.Text);
        options.StringFormat = "{0}";
        Assert.Equal("minus:123456789", cell.Text);
    }

    [Fact]
    public void Native_retarget_keeps_typed_and_public_object_values_in_sync()
    {
        using var source = new FlatTreeDataGridSource<Row>([new Row(), new Row { Number = 987654321 }]);
        using var column = new U.TextColumn<Row, int>("Number", x => x.Number,
            options: new() { Culture = CultureInfo.InvariantCulture });
        using var value = column.CreateCell(source.Rows[0]);
        Assert.True(column.TryReuseCell(value, (IRow<Row>)source.Rows[1]));
        Assert.Equal(987654321, value.Value);
        Assert.Equal("987654321", ((U.ITextCell)value).Text);
    }

    private static string Baseline(int value) => string.Format(CultureInfo.InvariantCulture, "{0}", value);
    private sealed class Row { public int Number { get; set; } = 123456789; }
    private sealed class Once<T>(T value) : IObservable<T>, IDisposable
    {
        public IDisposable Subscribe(IObserver<T> observer) { observer.OnNext(value); return this; }
        public void Dispose() { }
    }
    private sealed class CustomCulture() : CultureInfo("en-US"), ICustomFormatter
    {
        internal string Prefix = "custom:";
        public override object? GetFormat(Type? type) => type == typeof(ICustomFormatter) ? this : base.GetFormat(type);
        public string Format(string? format, object? value, IFormatProvider? provider) => Prefix + value;
    }
}
