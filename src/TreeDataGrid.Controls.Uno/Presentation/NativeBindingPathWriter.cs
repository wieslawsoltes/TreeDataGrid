using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Uno.Controls.Presentation;

/// <summary>
/// Uno's BindingPath setter swallows model exceptions. For row property/indexer
/// paths, write the public endpoint explicitly so CellEditSession can reject and
/// retry failed edits. Observation and target evaluation remain native bindings.
/// </summary>
internal static partial class NativeBindingPathWriter
{
    internal static bool TryWrite(Binding binding, object model, object? value, CultureInfo culture, Func<bool>? isCurrent = null)
    {
        if (binding.ElementName is not null || binding.RelativeSource is not null) return false;
        return TryWriteResolvedSource(binding, binding.Source ?? model, value, culture, isCurrent);
    }

    internal static bool TryWriteResolvedSource(Binding binding, object? source, object? value, CultureInfo culture, Func<bool>? isCurrent = null)
    {
        var path = binding.Path?.Path ?? string.Empty;
        if (path.Contains('(') || path.Contains(')')) return false; // Native attached-property syntax.
        if (source is null) throw new InvalidOperationException("The native binding source is not available in this scope.");
        return WritePath(path, source, value, culture, binding.Converter,
            binding.ConverterParameter, binding.ConverterLanguage, isCurrent);
    }

    // Kept independent of native Binding construction for exhaustive path tests.
    internal static bool WritePath(string path, object model, object? value, CultureInfo culture,
        IValueConverter? converter = null, object? converterParameter = null, string? converterLanguage = null, Func<bool>? isCurrent = null)
    {
        var segments = Parse(path);
        if (segments.Count == 0) throw new InvalidOperationException("A row itself is not a writable binding endpoint.");
        object? owner = model;
        try
        {
            for (var i = 0; i < segments.Count - 1; ++i)
            {
                owner = Resolve(owner ?? throw new InvalidOperationException("A binding path owner is null."), segments[i]).Read();
                EnsureCurrent();
                if (owner?.GetType().IsValueType == true)
                    throw new InvalidOperationException("A binding path cannot write through a copied value-type owner.");
            }
            var endpoint = Resolve(owner ?? throw new InvalidOperationException("A binding path owner is null."), segments[^1]);
            EnsureCurrent();
            if (converter is not null)
                value = converter.ConvertBack(value!, endpoint.Type, converterParameter!,
                    string.IsNullOrEmpty(converterLanguage) ? culture.Name : converterLanguage);
            EnsureCurrent();
            if (ReferenceEquals(value, DependencyProperty.UnsetValue))
                throw new InvalidOperationException("The binding converter rejected the value.");
            var converted = ConvertValue(value, endpoint.Type, culture);
            EnsureCurrent();
            if (segments.Count > 1)
            {
                // Conversion can replace a nested owner without changing the
                // row. Windows BindingExpression.DataItem need not identify
                // the leaf owner, so verify the path independently of it.
                object? currentOwner = model;
                for (var i = 0; i < segments.Count - 1; ++i)
                {
                    if (currentOwner is null) throw new OperationCanceledException("The binding path owner changed during conversion.");
                    currentOwner = Resolve(currentOwner, segments[i]).Read();
                    EnsureCurrent();
                }
                if (!ReferenceEquals(currentOwner, owner))
                    throw new OperationCanceledException("The binding path owner changed during conversion.");
            }
            endpoint.Write(converted);
            return true;
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
        void EnsureCurrent()
        {
            if (isCurrent?.Invoke() == false) throw new OperationCanceledException("The cell binding changed while resolving its write endpoint.");
        }
    }

    private readonly record struct Segment(string Name, bool Index);
    private readonly record struct Endpoint(Type Type, Func<object?> Read, Action<object?> Write);
    private static List<Segment> Parse(string path)
    {
        var result = new List<Segment>();
        path = path.Trim();
        for (var i = 0; i < path.Length;)
        {
            if (result.Count > 0 && path[i] != '[')
            {
                if (path[i++] != '.') throw InvalidPath();
                while (i < path.Length && char.IsWhiteSpace(path[i])) ++i;
                if (i == path.Length || path[i] == '.') throw InvalidPath();
            }
            if (path[i] == '[')
            {
                var end = path.IndexOf(']', i + 1);
                if (end < 0) throw InvalidPath();
                var token = path[(i + 1)..end];
                // Uno's binding path splits dots even inside an index token.
                // Do not write a key which the read side cannot address.
                if (token.Contains('[') || token.Contains('.')) throw InvalidPath();
                if (token.Contains('"') && (token.Length < 2 || token[0] != '"' || token[^1] != '"' ||
                    token.AsSpan(1, token.Length - 2).Contains('"'))) throw InvalidPath();
                result.Add(new(token, true));
                i = end + 1;
                while (i < path.Length && char.IsWhiteSpace(path[i])) ++i;
            }
            else
            {
                var start = i;
                while (i < path.Length && path[i] is not ('.' or '[')) ++i;
                var name = path[start..i].Trim();
                if (name.Length == 0 || name.Contains(']')) throw InvalidPath();
                result.Add(new(name, false));
            }
        }
        return result;
        ArgumentException InvalidPath() => new($"Unsupported or malformed binding write path '{path}'.", nameof(path));
    }
    private static Endpoint Resolve(object owner, Segment segment)
    {
        var type = owner.GetType();
        if (!segment.Index)
        {
            if (TryResolveMetadata(owner, segment.Name) is { } generated) return generated;
            if (type.GetProperty(segment.Name, BindingFlags.Instance | BindingFlags.Public) is { } property && property.GetIndexParameters().Length == 0)
                return new(property.PropertyType, () => property.GetMethod?.IsPublic == true ? property.GetValue(owner) :
                    throw new InvalidOperationException("The bound property is not publicly readable."), value =>
                {
                    if (property.SetMethod?.IsPublic != true) throw new InvalidOperationException("The bound property is read-only.");
                    property.SetValue(owner, value);
                });
            if (type.GetField(segment.Name, BindingFlags.Instance | BindingFlags.Public) is { } field)
                return new(field.FieldType, () => field.GetValue(owner), value =>
                {
                    if (field.IsInitOnly || field.IsLiteral) throw new InvalidOperationException("The bound field is read-only.");
                    field.SetValue(owner, value);
                });
        }
        else
        {
            if (owner is Array array)
            {
                if (array.Rank != 1) throw new InvalidOperationException("Only single-dimensional binding indexers are supported.");
                var index = int.Parse(segment.Name, CultureInfo.InvariantCulture);
                return new(type.GetElementType()!, () => array.GetValue(index), value => array.SetValue(value, index));
            }
            if (TryResolveMetadataIndexer(owner, segment.Name) is { } generated) return generated;
            // Native binding chooses an int overload for an unquoted numeric
            // token, then string, then one unambiguous fallback. Reflection
            // enumeration order must never decide which property gets edited.
            var numeric = int.TryParse(segment.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            var indexer = FindIndexer(type, numeric ? typeof(int) : typeof(string)) ??
                (numeric ? FindIndexer(type, typeof(string)) : null) ?? FindIndexer(type, null);
            if (indexer is not null)
            {
                var parameterType = indexer.GetIndexParameters()[0].ParameterType;
                var token = parameterType == typeof(string) ? StringKey(segment.Name) : segment.Name;
                var key = parameterType == typeof(object) && numeric
                    ? int.Parse(token, CultureInfo.InvariantCulture) : ConvertValue(token, parameterType, CultureInfo.InvariantCulture);
                var arguments = new[] { key };
                return new(indexer.PropertyType, () => indexer.GetMethod?.IsPublic == true ? indexer.GetValue(owner, arguments) :
                    throw new InvalidOperationException("The bound indexer is not publicly readable."), value =>
                {
                    if (indexer.SetMethod?.IsPublic != true) throw new InvalidOperationException("The bound indexer is read-only.");
                    indexer.SetValue(owner, value, arguments);
                });
            }
        }
        var dynamicKey = segment.Index ? StringKey(segment.Name) : segment.Name;
        if (owner is IDictionary<string, object?> dynamicValues)
            return new(typeof(object), () => dynamicValues[dynamicKey], value => dynamicValues[dynamicKey] = value);
        if (owner is IDictionary dictionary)
            return new(typeof(object), () => dictionary[dynamicKey], value => dictionary[dynamicKey] = value);
        throw new InvalidOperationException($"No public binding endpoint '{segment.Name}' exists on '{type.Name}'.");
    }
    private static string StringKey(string token) => token.Length >= 2 && token[0] == '"' && token[^1] == '"'
        ? token[1..^1] : token;
    private static PropertyInfo? FindIndexer(Type type, Type? parameterType)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            var matches = current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(property => property.Name == "Item" && property.GetIndexParameters() is { Length: 1 } parameters &&
                    (parameterType is null || parameters[0].ParameterType == parameterType)).ToArray();
            if (matches.Length > 1) throw new AmbiguousMatchException($"Binding indexer on '{type.Name}' is ambiguous.");
            if (matches.Length == 1) return matches[0];
        }
        return null;
    }
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
        var converter = TypeDescriptor.GetConverter(target);
        if (converter.CanConvertFrom(value.GetType())) return converter.ConvertFrom(null, culture, value);
        throw new InvalidCastException($"Cannot convert {value.GetType().Name} to {target.Name}.");
    }
}
