using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Uno.Controls.Presentation;

internal static partial class NativeBindingPathWriter
{
    private static object? ConvertValue(object? value, Type target, CultureInfo culture)
    {
        var nullable = Nullable.GetUnderlyingType(target);
        if (value is null || (nullable is not null && value is string { Length: 0 }))
        {
            if (target.IsValueType && nullable is null) throw new InvalidCastException($"Null is not valid for {target.Name}.");
            return null;
        }
        target = nullable ?? target;
        if (target.IsInstanceOfType(value)) return value;
        if (target.IsEnum) return value is string text ? Enum.Parse(target, text, true) : Enum.ToObject(target, value);
        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(target)) return Convert.ChangeType(value, target, culture);
        if (TreeDataGridBindingRegistry.TryConvert(target, value, culture, out var registered)) return registered;
        if (value is string input)
        {
            if (target == typeof(Guid)) return Guid.Parse(input);
            if (target == typeof(TimeSpan)) return TimeSpan.Parse(input, culture);
            if (target == typeof(DateOnly)) return DateOnly.Parse(input, culture);
            if (target == typeof(TimeOnly)) return TimeOnly.Parse(input, culture);
            if (target == typeof(Uri)) return new Uri(input, UriKind.RelativeOrAbsolute);
        }
        if (BindingFeatures.ReflectionEnabled) return ConvertWithTypeDescriptor(value, target, culture);
        throw new InvalidCastException($"Cannot convert {value.GetType().Name} to {target.Name}. Register a typed conversion with TreeDataGridBindingRegistry.");
    }

    [RequiresUnreferencedCode("TypeDescriptor requires preserved custom converter constructors and members. Register a typed conversion in trimmed hosts.")]
    private static object? ConvertWithTypeDescriptor(object value, Type target, CultureInfo culture)
    {
        var converter = TypeDescriptor.GetConverter(target);
        if (converter.CanConvertFrom(value.GetType())) return converter.ConvertFrom(null, culture, value);
        throw new InvalidCastException($"Cannot convert {value.GetType().Name} to {target.Name}.");
    }

    private static object? ConvertKey(string token, Type keyType)
    {
        var numeric = int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index);
        return keyType == typeof(object) && numeric ? index :
            ConvertValue(keyType == typeof(string) ? StringKey(token) : token, keyType, CultureInfo.InvariantCulture);
    }
}
