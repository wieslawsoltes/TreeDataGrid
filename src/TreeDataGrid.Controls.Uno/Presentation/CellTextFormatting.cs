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
        // Resolve the provider and identity predicate once. Reference types and
        // nonidentity/custom-provider calls do not pay a second helper dispatch.
        culture ??= CultureInfo.CurrentCulture;
        if (format == "{0}" && culture.GetType() == typeof(CultureInfo))
        {
            if (typeof(T).IsValueType) return FormatIdentityValue(culture, value);
            if (value is string text) return text;
            if (value is null) return string.Empty;
        }
        return string.Format(culture, format, value);
    }

    private static string FormatIdentityValue<T>(CultureInfo provider, T value)
    {
        // Exact type equality guards every reinterpretation. No user-defined
        // conversion, formatter, nullable wrapper or enum enters a numeric arm.
        // JIT instantiations fold the tests. Built-in formatters still consult
        // the live NumberFormatInfo; no formatted value or culture is cached.
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
        // Unknown value types retain string.Format's boxed callback semantics.
        return string.Format(provider, "{0}", value);
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
