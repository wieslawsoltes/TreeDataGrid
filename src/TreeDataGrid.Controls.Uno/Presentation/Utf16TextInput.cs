using System;

namespace Uno.Controls.Presentation;

/// <summary>Decodes native UTF-16 character events without retaining a grid or model.</summary>
internal struct Utf16TextInput
{
    private char _highSurrogate;
    private long _timestamp;

    public string? Push(char character, long timestamp)
    {
        var high = _highSurrogate;
        var previous = _timestamp;
        _highSurrogate = default;
        if (char.IsHighSurrogate(character))
        {
            _highSurrogate = character;
            _timestamp = timestamp;
            return null;
        }
        if (char.IsLowSurrogate(character))
        {
            if (high == default || !IncrementalTextSearch.IsWithinTimeout(previous, timestamp)) return null;
            return string.Create(2, (high, character), static (span, pair) =>
            {
                span[0] = pair.high;
                span[1] = pair.character;
            });
        }
        return char.IsControl(character) ? null : character.ToString();
    }

    public void Reset() => this = default;
}
