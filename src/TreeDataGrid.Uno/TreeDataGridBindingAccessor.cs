using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.UI.Xaml.Data;

namespace Avalonia.Controls;

internal sealed class TreeDataGridBindingAccessor
{
    private static readonly Func<object, object>[] s_selfLinks = { x => x };

    private readonly string[] _segments;
    private readonly Func<object, object>[] _links;
    private readonly BindingMode _mode;
    private readonly Type? _declaredType;
    private readonly bool _canWrite;
    private Type? _inferredType;
    private bool _sampleValueWasNull;

    public TreeDataGridBindingAccessor(Binding binding, object? sampleModel = null, Type? modelType = null)
    {
        _ = binding ?? throw new ArgumentNullException(nameof(binding));

        EnsureSupported(binding);

        _segments = ParsePath(GetPath(binding));
        _links = _segments.Length == 0 ? s_selfLinks : CreateLinks(_segments);
        _mode = binding.Mode;
        _declaredType = TryGetValueType(modelType, _segments);
        _canWrite = _segments.Length > 0 &&
            _mode == BindingMode.TwoWay &&
            CanWritePath(sampleModel?.GetType() ?? modelType, _segments);

        if (sampleModel is not null)
        {
            var sampleValue = Read(sampleModel);
            _sampleValueWasNull = sampleValue is null;
        }
    }

    public bool CanWrite => _canWrite;
    public Func<object, object>[] Links => _links;
    public bool SampleValueWasNull => _sampleValueWasNull;
    public Type? ValueType => _declaredType ?? _inferredType;

    public static Func<TModel, string?>? TryCreateTextSelector<TModel>(Binding? binding)
        where TModel : class
    {
        if (binding is null)
            return null;

        var accessor = new TreeDataGridBindingAccessor(binding, modelType: typeof(TModel));
        return model => accessor.ReadAsString(model);
    }

    public object? Read(object model)
    {
        var value = ReadPathValue(model, _segments.Length);

        if (value is not null)
            _inferredType ??= value.GetType();

        return value;
    }

    public string? ReadAsString(object model)
    {
        return Read(model)?.ToString();
    }

    public bool ReadAsBoolean(object model)
    {
        return ConvertToNullableBoolean(Read(model)) ?? false;
    }

    public bool? ReadAsNullableBoolean(object model)
    {
        return ConvertToNullableBoolean(Read(model));
    }

    public void Write(object model, object? value)
    {
        if (!_canWrite)
            return;

        var targetType = ValueType;
        var converted = ConvertForSource(value, targetType);

        if (WritePathValue(model, converted) is { } writtenValue)
        {
            if (writtenValue is not null)
                _inferredType ??= writtenValue.GetType();
        }
    }

    private static void EnsureSupported(Binding binding)
    {
        if (HasAssignedValue(binding, nameof(Binding.ElementName)) ||
            HasAssignedValue(binding, nameof(Binding.RelativeSource)) ||
            HasAssignedValue(binding, nameof(Binding.Source)))
        {
            throw new NotSupportedException(
                "TreeDataGrid declarative columns only support row-model bindings.");
        }
    }

    private static bool HasAssignedValue(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        var value = property?.GetValue(instance);

        return value switch
        {
            null => false,
            string s => !string.IsNullOrEmpty(s),
            _ => true,
        };
    }

    private static string GetPath(Binding binding)
    {
        var property = binding.GetType().GetProperty(nameof(Binding.Path), BindingFlags.Instance | BindingFlags.Public);
        var pathValue = property?.GetValue(binding);

        if (pathValue is null)
            return string.Empty;

        return pathValue.GetType().GetProperty("Path", BindingFlags.Instance | BindingFlags.Public)?.GetValue(pathValue) as string
            ?? string.Empty;
    }

    private static string[] ParsePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? Array.Empty<string>()
            : path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static Func<object, object>[] CreateLinks(IReadOnlyList<string> segments)
    {
        var result = new Func<object, object>[segments.Count];

        for (var i = 0; i < segments.Count; ++i)
        {
            var count = i + 1;
            result[i] = model => ReadPathValue(model, segments, count)!;
        }

        return result;
    }

    private static Type? TryGetValueType(Type? rootType, IReadOnlyList<string> segments)
    {
        var current = rootType;

        foreach (var segment in segments)
        {
            if (current is null)
                return null;

            current = GetMemberType(current, segment);
        }

        return current;
    }

    private static bool CanWritePath(Type? rootType, IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
            return false;

        if (rootType is null)
            return true;

        var current = rootType;

        for (var i = 0; i < segments.Count - 1; ++i)
        {
            current = GetMemberType(current, segments[i]);

            if (current is null)
                return true;
        }

        return IsWritableMember(current, segments[^1]);
    }

    private static Type? GetMemberType(Type type, string memberName)
    {
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property is not null)
            return property.PropertyType;

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return field?.FieldType;
    }

    private static bool IsWritableMember(Type? type, string memberName)
    {
        if (type is null)
            return false;

        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property is not null)
            return property.SetMethod is not null;

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return field is not null && !field.IsInitOnly;
    }

    private object? ReadPathValue(object model, int count)
    {
        return ReadPathValue(model, _segments, count);
    }

    private static object? ReadPathValue(object? model, IReadOnlyList<string> segments, int count)
    {
        object? current = model;

        if (count == 0)
            return current;

        for (var i = 0; i < count; ++i)
        {
            if (current is null)
                return null;

            current = GetMemberValue(current, segments[i]);
        }

        return current;
    }

    private object? WritePathValue(object model, object? value)
    {
        if (_segments.Length == 0)
            return null;

        object? current = model;

        for (var i = 0; i < _segments.Length - 1; ++i)
        {
            if (current is null)
                return null;

            current = GetMemberValue(current, _segments[i]);
        }

        if (current is null)
            return null;

        return SetMemberValue(current, _segments[^1], value);
    }

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property is not null)
            return property.GetValue(instance);

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field is not null)
            return field.GetValue(instance);

        return null;
    }

    private static object? SetMemberValue(object instance, string memberName, object? value)
    {
        var type = instance.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property is not null && property.SetMethod is not null)
        {
            property.SetValue(instance, value);
            return property.GetValue(instance);
        }

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field is not null && !field.IsInitOnly)
        {
            field.SetValue(instance, value);
            return field.GetValue(instance);
        }

        return null;
    }

    private object? ConvertForSource(object? value, Type? targetType)
    {
        if (value is null || targetType is null)
            return value;

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType.IsInstanceOfType(value))
            return value;

        if (underlyingType.IsEnum)
        {
            return value is string s
                ? Enum.Parse(underlyingType, s, ignoreCase: true)
                : Enum.ToObject(underlyingType, value);
        }

        if (underlyingType == typeof(string))
            return Convert.ToString(value, CultureInfo.CurrentCulture);

        if (typeof(IEnumerable).IsAssignableFrom(underlyingType) && value is IEnumerable)
            return value;

        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(underlyingType))
            return Convert.ChangeType(value, underlyingType, CultureInfo.CurrentCulture);

        return value;
    }

    private static bool? ConvertToNullableBoolean(object? value)
    {
        return value switch
        {
            null => null,
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            IConvertible => (bool?)Convert.ChangeType(value, typeof(bool), CultureInfo.CurrentCulture),
            _ => null,
        };
    }
}
