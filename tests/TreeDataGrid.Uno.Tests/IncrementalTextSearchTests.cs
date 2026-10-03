using System.Globalization;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class IncrementalTextSearchTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    [Fact]
    public void Repeated_character_cycles_and_other_characters_extend_last_success()
    {
        var search = new IncrementalTextSearch();
        Assert.Equal("a", search.GetCandidate("a", 1, English));
        search.Accept("a");
        Assert.Equal("a", search.GetCandidate("A", 100, English));
        Assert.Equal("ab", search.GetCandidate("b", 200, English));
        search.Accept("ab");
        Assert.Equal("abx", search.GetCandidate("x", 300, English));
        // A failed match must not poison the next candidate.
        Assert.Equal("abc", search.GetCandidate("c", 400, English));
    }
    [Fact]
    public void Timeout_reset_and_clock_rollback_start_a_new_candidate()
    {
        var search = new IncrementalTextSearch();
        search.GetCandidate("a", 1, English);
        search.Accept("a");
        Assert.Equal("b", search.GetCandidate("b", 501, English));
        Assert.Equal("c", search.GetCandidate("c", 100, English));
        search.Reset();
        Assert.Equal("d", search.GetCandidate("d", 101, English));
    }
    [Fact]
    public void Unicode_text_elements_and_committed_strings_are_not_truncated()
    {
        var search = new IncrementalTextSearch();
        var symbol = char.ConvertFromUtf32(0x1F332);
        Assert.True(IncrementalTextSearch.IsSingleCharacter(symbol));
        Assert.Equal(symbol, search.GetCandidate(symbol, 1, English));
        search.Accept(symbol);
        Assert.Equal(symbol, search.GetCandidate(symbol, 2, English));
        Assert.Equal(symbol + "日本", search.GetCandidate("日本", 3, English));
        Assert.True(IncrementalTextSearch.Matches("日本語", "日本", English));
    }
    [Fact]
    public void Matching_uses_current_culture_case_rules_and_ignores_nulls()
    {
        var turkish = CultureInfo.GetCultureInfo("tr-TR");
        Assert.True(IncrementalTextSearch.Matches("İstanbul", "i", turkish));
        Assert.False(IncrementalTextSearch.Matches("Isparta", "i", turkish));
        Assert.False(IncrementalTextSearch.Matches(null, "i", turkish));
        var search = new IncrementalTextSearch();
        Assert.Equal(string.Empty, search.GetCandidate("\t", 1, turkish));
        Assert.Equal(string.Empty, search.GetCandidate(string.Empty, 2, turkish));
    }
}
