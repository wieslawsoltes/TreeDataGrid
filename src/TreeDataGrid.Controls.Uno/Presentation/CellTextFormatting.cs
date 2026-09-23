using System;
using System.Globalization;

namespace Uno.Controls.Presentation;

/// <summary>Preserves composite formatting while avoiding a copy of an already formatted string.</summary>
internal static class CellTextFormatting
{
    internal static string Format(CultureInfo? culture, string format, object? value)
    {
        // Only the exact identity format and the base CultureInfo implementation
        // have no application-controlled formatter to invoke. A CultureInfo
        // subclass may override GetFormat even for strings/null, so it must take
        // the ordinary path. Non-string values retain IFormattable/ISpanFormattable
        // dispatch and provider, alignment, escaping and exception semantics.
        if (format == "{0}" && (culture is null || culture.GetType() == typeof(CultureInfo)))
        {
            if (value is string text) return text;
            if (value is null) return string.Empty;
        }
        return string.Format(culture, format, value);
    }
}
