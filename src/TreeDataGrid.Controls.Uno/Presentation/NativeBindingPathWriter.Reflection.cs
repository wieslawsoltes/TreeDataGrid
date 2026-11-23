using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Uno.Controls.Presentation;

internal static partial class NativeBindingPathWriter
{
    [RequiresUnreferencedCode("Unregistered models require preserved public members. Use generated metadata or TreeDataGridBindingRegistry in trimmed hosts.")]
    private static Endpoint? ResolveReflected(object owner, Segment segment)
    {
        var type = owner.GetType();
        if (!segment.Index)
        {
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
            var numeric = int.TryParse(segment.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            var indexer = FindIndexer(type, numeric ? typeof(int) : typeof(string)) ??
                (numeric ? FindIndexer(type, typeof(string)) : null) ?? FindIndexer(type, null);
            if (indexer is not null)
            {
                var arguments = new[] { ConvertKey(segment.Name, indexer.GetIndexParameters()[0].ParameterType) };
                return new(indexer.PropertyType, () => indexer.GetMethod?.IsPublic == true ? indexer.GetValue(owner, arguments) :
                    throw new InvalidOperationException("The bound indexer is not publicly readable."), value =>
                {
                    if (indexer.SetMethod?.IsPublic != true) throw new InvalidOperationException("The bound indexer is read-only.");
                    indexer.SetValue(owner, value, arguments);
                });
            }
        }
        return null;
    }

    [RequiresUnreferencedCode("Runtime binding type discovery requires preserved public members.")]
    private static Type? GetReflectedValueType(Type type, Segment segment)
    {
        if (!segment.Index) return type.GetProperty(segment.Name)?.PropertyType ?? type.GetField(segment.Name)?.FieldType;
        var numeric = int.TryParse(segment.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        return (FindIndexer(type, numeric ? typeof(int) : typeof(string)) ??
            (numeric ? FindIndexer(type, typeof(string)) : null) ?? FindIndexer(type, null))?.PropertyType;
    }

    [RequiresUnreferencedCode("Runtime indexer discovery requires preserved public properties.")]
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
}
