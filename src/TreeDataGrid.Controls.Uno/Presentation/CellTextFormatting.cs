using System;
using System.Globalization;
using System.Text;

namespace Uno.Controls.Presentation;

/// <summary>Preserves the reference display contract without boxing known numeric values.</summary>
internal static class CellTextFormatting
{
    // Parse only the trusted identity literal. General CompositeFormat parsing
    // has different edge-case limits from string.Format(string, object), and
    // arbitrary format strings/providers must keep the original dispatch path.
    private static class Identity
    {
        internal static readonly CompositeFormat Format = CompositeFormat.Parse("{0}");
    }

    internal static string Format<T>(CultureInfo? culture, string format, T value)
    {
        // Type tests are invariant per closed generic call. Restrict this path to
        // framework numeric structs, not user ISpanFormattable implementations,
        // nullable wrappers, enums or objects with observable formatting callbacks.
        if (IsBuiltInNumber<T>() && format == "{0}")
        {
            var provider = culture ?? CultureInfo.CurrentCulture;
            if (provider.GetType() == typeof(CultureInfo))
                return string.Format(provider, Identity.Format, value);
        }
        return Format(culture, format, (object?)value);
    }

    internal static string Format(CultureInfo? culture, string format, object? value)
    {
        // The reference TextCell explicitly supplies CurrentCulture when its
        // options omit Culture. Passing null straight to string.Format is not
        // equivalent when CurrentCulture is a subclass providing ICustomFormatter.
        culture ??= CultureInfo.CurrentCulture;
        if (format == "{0}" && culture.GetType() == typeof(CultureInfo))
        {
            if (value is string text) return text;
            if (value is null) return string.Empty;
        }
        return string.Format(culture, format, value);
    }

    private static bool IsBuiltInNumber<T>() =>
        typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte) ||
        typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ||
        typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
        typeof(T) == typeof(long) || typeof(T) == typeof(ulong) ||
        typeof(T) == typeof(nint) || typeof(T) == typeof(nuint) ||
        typeof(T) == typeof(Int128) || typeof(T) == typeof(UInt128) ||
        typeof(T) == typeof(Half) || typeof(T) == typeof(float) ||
        typeof(T) == typeof(double) || typeof(T) == typeof(decimal);
}
