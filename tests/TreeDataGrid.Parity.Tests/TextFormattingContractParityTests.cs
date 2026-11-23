using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Subjects;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Parity.Tests;

public sealed class TextFormattingContractParityTests
{
    [Theory]
    [InlineData("{0}")]
    [InlineData("{0,30}")]
    [InlineData("{0,-30}")]
    [InlineData("value={0}; again={0}")]
    [InlineData("{{{0}}}")]
    [InlineData("{0:N3}")]
    [InlineData("{0:X}")]
    [InlineData("literal")]
    [InlineData(null)]
    public void Numeric_and_fallback_types_match_actual_reference_cells(string? format)
    {
        foreach (var culture in new CultureInfo?[] { CultureInfo.InvariantCulture, new("pl-PL"), new("ar-EG"), null })
        {
            Compare(byte.MaxValue, format, culture);
            Compare(sbyte.MinValue, format, culture);
            Compare(short.MinValue, format, culture);
            Compare(ushort.MaxValue, format, culture);
            Compare(int.MinValue, format, culture);
            Compare(uint.MaxValue, format, culture);
            Compare(long.MinValue, format, culture);
            Compare(ulong.MaxValue, format, culture);
            Compare((nint)(-12345), format, culture);
            Compare((nuint)12345, format, culture);
            Compare(Int128.MinValue, format, culture);
            Compare(UInt128.MaxValue, format, culture);
            Compare((Half)(-12.5), format, culture);
            Compare(-12345.625f, format, culture);
            Compare(-12345.625, format, culture);
            Compare(-12345.625m, format, culture);
            Compare(double.NaN, format, culture);
            Compare(double.PositiveInfinity, format, culture);
            Compare(-0.0, format, culture);
            Compare<int?>(17, format, culture);
            Compare<int?>(null, format, culture);
            Compare<string?>("Zażółć — Ω", format, culture);
            Compare<string?>(null, format, culture);
            Compare(true, format, culture);
            Compare(DayOfWeek.Friday, format, culture);
            Compare(new DateTime(2020, 3, 4, 5, 6, 7), format, culture);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Null_culture_uses_custom_current_culture_on_every_read(int kind)
    {
        var previous = CultureInfo.CurrentCulture;
        var culture = new FormatterCulture();
        try
        {
            CultureInfo.CurrentCulture = culture;
            if (kind == 0) Compare<string?>(null, "{0}", null, culture);
            else if (kind == 1) Compare("text", "{0}", null, culture);
            else Compare(123456789, "{0}", null, culture);
            culture.Prefix = "changed:";
            Compare(17, "{0}", null, culture);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Existing_cells_follow_current_culture_changes_without_reconstruction()
    {
        var previous = CultureInfo.CurrentCulture;
        using var aBinding = new BehaviorSubject<Avalonia.Data.BindingValue<int>>(new(17));
        using var uBinding = new BehaviorSubject<int>(17);
        using var a = new A.TextCell<int>(aBinding, true, new A.TextColumnOptions<object> { Culture = null! });
        using var u = new U.TextCell<int>(uBinding, true, new U.TextColumnOptions<object> { Culture = null! });
        try
        {
            CultureInfo.CurrentCulture = new FormatterCulture { Prefix = "first:" };
            Assert.Equal(a.Text, u.Text);
            Assert.Equal("first:17", u.Text);
            CultureInfo.CurrentCulture = new FormatterCulture { Prefix = "second:" };
            Assert.Equal(a.Text, u.Text);
            Assert.Equal("second:17", u.Text);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal("17", u.Text);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{1}")]
    [InlineData("{0,}")]
    [InlineData("{0,1000000}")]
    [InlineData("{1000000}")]
    public void Malformed_or_oversized_formats_keep_the_reference_string_parser(string format)
    {
        Compare(123456789, format, CultureInfo.InvariantCulture);
        var culture = new FormatterCulture();
        Compare(123456789, format, culture, culture);
    }

    [Fact]
    public void Custom_formatter_error_identity_and_provider_dispatch_are_preserved()
    {
        var failure = new InvalidOperationException("custom formatter");
        var culture = new FormatterCulture { Failure = failure };
        using var aBinding = new BehaviorSubject<Avalonia.Data.BindingValue<int>>(new(17));
        using var uBinding = new BehaviorSubject<int>(17);
        using var a = new A.TextCell<int>(aBinding, true, new A.TextColumnOptions<object> { Culture = culture });
        using var u = new U.TextCell<int>(uBinding, true, new U.TextColumnOptions<object> { Culture = culture });
        Assert.Same(failure, Record.Exception(() => _ = a.Text));
        var trace = culture.Trace.ToArray();
        culture.Trace.Clear();
        Assert.Same(failure, Record.Exception(() => _ = u.Text));
        Assert.Equal(trace, culture.Trace);
        culture.Failure = null;
        Assert.Equal("custom:17", u.Text);
    }

    [Fact]
    public void Custom_span_formattable_keeps_fallback_order_and_exact_format_argument()
    {
        var trace = new List<string>();
        var value = new TracedValue(trace);
        using var aBinding = new BehaviorSubject<Avalonia.Data.BindingValue<TracedValue>>(new(value));
        using var uBinding = new BehaviorSubject<TracedValue>(value);
        using var a = new A.TextCell<TracedValue>(aBinding, true, new A.TextColumnOptions<object>());
        using var u = new U.TextCell<TracedValue>(uBinding, true, new U.TextColumnOptions<object>());
        var expected = a.Text;
        var expectedTrace = trace.ToArray();
        trace.Clear();
        Assert.Equal(expected, u.Text);
        Assert.Equal(expectedTrace, trace);
    }

    [Fact]
    public void Editing_text_is_not_passed_through_the_display_formatter()
    {
        var culture = new FormatterCulture();
        using var aBinding = new BehaviorSubject<Avalonia.Data.BindingValue<int>>(new(17));
        using var uBinding = new BehaviorSubject<int>(17);
        using var a = new A.TextCell<int>(aBinding, false, new A.TextColumnOptions<object> { Culture = culture });
        using var u = new U.TextCell<int>(uBinding, false, new U.TextColumnOptions<object> { Culture = culture });
        a.BeginEdit(); u.BeginEdit();
        a.Text = u.Text = "buffered edit";
        culture.Trace.Clear();
        Assert.Equal("buffered edit", a.Text);
        Assert.Equal(a.Text, u.Text);
        Assert.Empty(culture.Trace);
        a.CancelEdit(); u.CancelEdit();
        Assert.Equal(a.Text, u.Text);
    }

    private static void Compare<T>(T value, string? format, CultureInfo? culture, FormatterCulture? trace = null)
    {
        using var aBinding = new BehaviorSubject<Avalonia.Data.BindingValue<T>>(new(value));
        using var uBinding = new BehaviorSubject<T>(value);
        using var a = new A.TextCell<T>(aBinding, true, new A.TextColumnOptions<object> { Culture = culture!, StringFormat = format! });
        using var u = new U.TextCell<T>(uBinding, true, new U.TextColumnOptions<object> { Culture = culture!, StringFormat = format! });
        trace?.Trace.Clear();
        string? expected = null, actual = null;
        var referenceError = Record.Exception(() => expected = a.Text);
        var expectedTrace = trace?.Trace.ToArray();
        trace?.Trace.Clear();
        var nativeError = Record.Exception(() => actual = u.Text);
        Assert.Equal(referenceError?.GetType(), nativeError?.GetType());
        Assert.Equal((referenceError as ArgumentException)?.ParamName, (nativeError as ArgumentException)?.ParamName);
        Assert.Equal(expected, actual);
        if (trace is not null) Assert.Equal(expectedTrace, trace.Trace);
    }

    private sealed class FormatterCulture() : CultureInfo("en-US"), ICustomFormatter
    {
        internal readonly List<string> Trace = new();
        internal string Prefix = "custom:";
        internal Exception? Failure;
        public override object? GetFormat(Type? type)
        {
            Trace.Add("GetFormat:" + type?.Name);
            return type == typeof(ICustomFormatter) ? this : base.GetFormat(type);
        }
        public string Format(string? format, object? value, IFormatProvider? provider)
        {
            Assert.Same(this, provider);
            Trace.Add("Format:" + (format ?? "<null>") + ":" + (value?.GetType().Name ?? "null"));
            if (Failure is { } error) throw error;
            return Prefix + (value?.ToString() ?? "<null>");
        }
    }

    private readonly struct TracedValue(List<string> trace) : ISpanFormattable
    {
        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            trace.Add("TryFormat:" + format.ToString());
            charsWritten = 0;
            return false;
        }
        public string ToString(string? format, IFormatProvider? provider)
        {
            trace.Add("ToString:" + (format ?? "<null>"));
            return "fallback";
        }
        public override string ToString() => throw new InvalidOperationException("Wrong formatting dispatch.");
    }
}
