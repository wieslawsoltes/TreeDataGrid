using System;
using System.Globalization;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class UnicodeTextInputTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData(0, 499, true)]
    [InlineData(0, 500, false)]
    [InlineData(500, 499, false)]
    [InlineData(long.MinValue, long.MaxValue, false)]
    [InlineData(-100, 100, true)]
    [InlineData(long.MaxValue - 100, long.MaxValue, true)]
    [InlineData(long.MinValue, long.MinValue + 499, true)]
    public void Timeout_comparison_never_wraps_signed_arithmetic(long previous, long timestamp, bool expected) =>
        Assert.Equal(expected, IncrementalTextSearch.IsWithinTimeout(previous, timestamp));

    [Fact]
    public void Large_forward_clock_jump_does_not_append_to_an_old_match()
    {
        var input = new IncrementalTextSearch();
        input.GetCandidate("a", long.MinValue, English);
        input.Accept("a");
        Assert.Equal("b", input.GetCandidate("b", long.MaxValue, English));
    }

    [Theory]
    [InlineData(0x1F332)]
    [InlineData(0x10FFFF)]
    [InlineData(0x10000)]
    public void Native_surrogate_pairs_are_one_complete_scalar(int scalar)
    {
        var text = char.ConvertFromUtf32(scalar);
        var input = new Utf16TextInput();
        Assert.Null(input.Push(text[0], 1));
        Assert.Equal(text, input.Push(text[1], 2));
        Assert.Null(input.Push(text[1], 3));
    }

    [Fact]
    public void Reset_control_and_ordinary_text_retire_a_pending_high_surrogate()
    {
        var input = new Utf16TextInput();
        Assert.Null(input.Push('\uD83C', 0));
        input.Reset();
        Assert.Null(input.Push('\uDF32', 1));
        Assert.Null(input.Push('\uD83C', 2));
        Assert.Null(input.Push('\t', 3));
        Assert.Null(input.Push('\uDF32', 4));
        Assert.Null(input.Push('\uD83C', 5));
        Assert.Equal("a", input.Push('a', 6));
        Assert.Null(input.Push('\uDF32', 7));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(-1)]
    [InlineData(long.MaxValue)]
    public void Surrogates_cannot_bridge_timeout_or_clock_rollback(long secondTime)
    {
        var input = new Utf16TextInput();
        Assert.Null(input.Push('\uD83C', 0));
        Assert.Null(input.Push('\uDF32', secondTime));
        Assert.Equal("x", input.Push('x', secondTime));
    }

    [Fact]
    public void New_high_surrogate_supersedes_an_unfinished_pair()
    {
        var input = new Utf16TextInput();
        Assert.Null(input.Push('\uD83C', 0));
        Assert.Null(input.Push('\uD83D', 1));
        Assert.Equal("\U0001F600", input.Push('\uDE00', 2));
    }

    [Fact]
    public void Buffering_a_high_surrogate_does_not_allocate()
    {
        var input = new Utf16TextInput();
        for (var i = 0; i < 100; ++i) input.Push('\uD83C', i);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; ++i) input.Push('\uD83C', i);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, bytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Malformed_or_control_text_does_not_modify_search_timing_or_last_match(int kind)
    {
        // Custom-attribute UTF-8 serialization replaces unpaired surrogates.
        // Construct the malformed UTF-16 at execution time, not in InlineData.
        var text = kind switch
        {
            0 => new string((char)0xD800, 1),
            1 => new string((char)0xDC00, 1),
            2 => "a" + (char)0xD800 + "b",
            3 => "ab\tcd",
            _ => "ab\0cd",
        };
        var input = new IncrementalTextSearch();
        input.GetCandidate("a", 1, English);
        input.Accept("a");
        Assert.Equal(string.Empty, input.GetCandidate(text, 490, English));
        Assert.Equal("b", input.GetCandidate("b", 501, English));
    }

    [Theory]
    [InlineData("e\u0301")]
    [InlineData("\U0001F469\u200D\U0001F4BB")]
    [InlineData("\U0001F1F5\U0001F1F1")]
    [InlineData("\U0001F44D\U0001F3FD")]
    public void Extended_graphemes_cycle_as_complete_text_elements(string text)
    {
        var input = new IncrementalTextSearch();
        Assert.True(IncrementalTextSearch.IsSingleCharacter(text));
        Assert.Equal(text, input.GetCandidate(text, 1, English));
        input.Accept(text);
        Assert.Equal(text, input.GetCandidate(text, 2, English));
        Assert.Equal(text + "日本", input.GetCandidate("日本", 3, English));
    }

    [Theory]
    [InlineData(int.MaxValue - 1, int.MaxValue - 1, int.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue - 1, int.MaxValue)]
    [InlineData(int.MaxValue - 1, 2, int.MaxValue)]
    [InlineData(0, 0, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(3, 4, 10)]
    public void Virtual_source_search_wraps_without_overflow(int start, int offset, int count) =>
        Assert.Equal((int)(((long)start + offset) % count), IncrementalTextSearch.WrapIndex(start, offset, count));
}
