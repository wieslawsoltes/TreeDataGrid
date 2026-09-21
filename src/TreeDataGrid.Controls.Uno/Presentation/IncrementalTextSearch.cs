using System;
using System.Globalization;

namespace Uno.Controls.Presentation;

/// <summary>View-input state only; never holds a source, row or selection.</summary>
internal sealed class IncrementalTextSearch
{
    private string _matched = string.Empty;
    private long? _lastInput;
    internal string GetCandidate(string text, long timestamp, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(text) || char.IsControl(text, 0)) return string.Empty;
        var continuation = _lastInput is { } previous && timestamp >= previous && timestamp - previous < 500;
        _lastInput = timestamp;
        if (!continuation) return text;
        // A repeated text element cycles instead of extending the word. Treat a
        // surrogate pair as one character, not two unrelated UTF-16 code units.
        var single = _matched.Length > 0 && StringInfo.GetNextTextElementLength(_matched) == _matched.Length;
        return single && culture.CompareInfo.Compare(_matched, text, CompareOptions.IgnoreCase) == 0
            ? _matched : _matched + text;
    }
    internal void Accept(string candidate) => _matched = candidate;
    internal void Reset() { _matched = string.Empty; _lastInput = null; }
    internal static bool IsSingleCharacter(string text) => text.Length > 0 && StringInfo.GetNextTextElementLength(text) == text.Length;
    internal static bool Matches(string? value, string candidate, CultureInfo culture) => value is not null &&
        culture.CompareInfo.IsPrefix(value, candidate, CompareOptions.IgnoreCase);
}
