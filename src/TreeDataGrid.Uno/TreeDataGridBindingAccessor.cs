using System;
using System.Reflection;
using Microsoft.UI.Xaml.Data;

namespace Avalonia.Controls;

internal sealed class TreeDataGridBindingAccessor
{
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

        _segments = TreeDataGridMemberPath.Parse(GetPath(binding));
        _links = TreeDataGridMemberPath.CreateProgressiveLinks(_segments);
        _mode = binding.Mode;
        _declaredType = TreeDataGridMemberPath.TryGetValueType(modelType, _segments);
        _canWrite = _segments.Length > 0 &&
            _mode == BindingMode.TwoWay &&
            TreeDataGridMemberPath.CanWrite(sampleModel?.GetType() ?? modelType, _segments);

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
        var value = TreeDataGridMemberPath.Read(model, _segments, _segments.Length);

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
        return TreeDataGridMemberPath.ConvertToNullableBoolean(Read(model)) ?? false;
    }

    public bool? ReadAsNullableBoolean(object model)
    {
        return TreeDataGridMemberPath.ConvertToNullableBoolean(Read(model));
    }

    public void Write(object model, object? value)
    {
        if (!_canWrite)
            return;

        var targetType = ValueType;
        var converted = TreeDataGridMemberPath.ConvertForSource(value, targetType);

        if (TreeDataGridMemberPath.Write(model, _segments, converted) is { } writtenValue)
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

}
