using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class NullableFormattingAllocationTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void Nullable_identity_formatting_does_not_add_argument_boxes(int type, bool empty)
    {
        switch (type)
        {
            case 0: Verify<int>(empty ? null : 123456789); break;
            case 1: Verify<long>(empty ? null : 12345678901234567L); break;
            default: Verify<decimal>(empty ? null : 123456789.125m); break;
        }
    }

    [Fact]
    public void Reference_string_paths_preserve_original_identity_and_null_values()
    {
        var text = new string('x', 25);
        Assert.Same(text, CellTextFormatting.Format<string>(CultureInfo.InvariantCulture, "{0}", text));
        Assert.Same(text, CellTextFormatting.Format<object>(CultureInfo.InvariantCulture, "{0}", text));
        Assert.Same(string.Empty, CellTextFormatting.Format<string?>(CultureInfo.InvariantCulture, "{0}", null));
        Assert.Same(string.Empty, CellTextFormatting.Format<object?>(CultureInfo.InvariantCulture, "{0}", null));
    }

    private static void Verify<T>(T? value) where T : struct
    {
        for (var i = 0; i < 4096; ++i) { _ = Reference(value); _ = Candidate(value); }
        const int count = 8192;
        long expectedCharacters = 0, actualCharacters = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < count; ++i) expectedCharacters += Reference(value).Length;
        var expected = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < count; ++i) actualCharacters += Candidate(value).Length;
        var actual = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Reference(value), Candidate(value));
        Assert.Equal(expectedCharacters, actualCharacters);
        if (RuntimeFeature.IsDynamicCodeCompiled)
            Assert.True(actual <= expected, $"Nullable {typeof(T)}: allocated {actual} bytes versus {expected} composite-format bytes.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string Reference<T>(T value) => string.Format(CultureInfo.InvariantCulture, "{0}", value);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string Candidate<T>(T value) => CellTextFormatting.Format(CultureInfo.InvariantCulture, "{0}", value);
}
