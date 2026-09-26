using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Uno.Controls.Presentation;

/// <summary>Preserves composite formatting while avoiding known numeric argument boxes.</summary>
internal static class CellTextFormatting
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Format<T>(CultureInfo? culture, string format, T value)
    {
        // Keep shared reference-type instantiations out of the numeric dispatcher.
        // The string identity path remains the original object helper, with no
        // formatting buffer, parsed-format cache or formatted-value cache.
        return typeof(T).IsValueType
            ? FormatValue(culture, format, value)
            : Format(culture, format, (object?)value);
    }

    private static string FormatValue<T>(CultureInfo? culture, string format, T value)
    {
        if (format == "{0}")
        {
            var provider = culture ?? CultureInfo.CurrentCulture;
            if (provider.GetType() == typeof(CultureInfo))
            {
                // Each reinterpretation is guarded by exact runtime type equality.
                // No user-defined conversion, formatter, nullable wrapper or enum
                // enters this path. JIT instantiations fold the type tests, and
                // the built-in formatter still reads the live NumberFormatInfo.
                if (typeof(T) == typeof(int)) return Unsafe.As<T, int>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(double)) return Unsafe.As<T, double>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(decimal)) return Unsafe.As<T, decimal>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(long)) return Unsafe.As<T, long>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(uint)) return Unsafe.As<T, uint>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(ulong)) return Unsafe.As<T, ulong>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(float)) return Unsafe.As<T, float>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(byte)) return Unsafe.As<T, byte>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(sbyte)) return Unsafe.As<T, sbyte>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(short)) return Unsafe.As<T, short>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(ushort)) return Unsafe.As<T, ushort>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(nint)) return Unsafe.As<T, nint>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(nuint)) return Unsafe.As<T, nuint>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(Int128)) return Unsafe.As<T, Int128>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(UInt128)) return Unsafe.As<T, UInt128>(ref value).ToString(null, provider);
                if (typeof(T) == typeof(Half)) return Unsafe.As<T, Half>(ref value).ToString(null, provider);
            }
        }
        // Preserve the original string parser, argument boxing and custom
        // provider/ISpanFormattable/IFormattable callback order on every fallback.
        return Format(culture, format, (object?)value);
    }

    internal static string Format(CultureInfo? culture, string format, object? value)
    {
        // Avalonia supplies CurrentCulture explicitly when options omit Culture.
        // A null provider is not equivalent for a custom CurrentCulture subclass.
        culture ??= CultureInfo.CurrentCulture;
        if (format == "{0}" && culture.GetType() == typeof(CultureInfo))
        {
            if (value is string text) return text;
            if (value is null) return string.Empty;
        }
        return string.Format(culture, format, value);
    }
}
