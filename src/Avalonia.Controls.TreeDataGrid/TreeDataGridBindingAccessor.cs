using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Avalonia.Data;
using Avalonia.Data.Core;

namespace Avalonia.Controls
{
    internal sealed class TreeDataGridBindingAccessor : IDisposable
    {
        private static readonly PropertyInfo? s_compiledBindingPathElementsProperty =
            typeof(CompiledBindingPath).GetProperty("Elements", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private readonly IDisposable _bindingDisposable;
        private readonly BindingExpressionBase? _expression;
        private readonly TreeDataGridBindingProbe _probe = new();
        private readonly Func<object, object>[] _links;
        private readonly BindingMode _mode;
        private readonly Type? _declaredType;
        private Type? _inferredType;
        private bool _sampleValueWasNull;

        public TreeDataGridBindingAccessor(BindingBase binding, object? sampleModel = null, Type? modelType = null)
        {
            _expression = _probe.Bind(TreeDataGridBindingProbe.ValueProperty, binding);
            _bindingDisposable = _expression;
            _mode = GetMode(binding);
            _links = TryCreateLinks(binding, modelType) ?? TreeDataGridMemberPath.CreateProgressiveLinks(Array.Empty<string>());
            _declaredType = TryGetValueType(binding, modelType);

            if (sampleModel is not null)
            {
                var sampleValue = Read(sampleModel);
                _sampleValueWasNull = sampleValue is null;
            }
        }

        public bool CanWrite => _mode is BindingMode.TwoWay or BindingMode.OneWayToSource;
        public Func<object, object>[] Links => _links;
        public bool SampleValueWasNull => _sampleValueWasNull;
        public Type? ValueType => _declaredType ?? _inferredType;

        public static Func<TModel, string?>? TryCreateTextSelector<TModel>(BindingBase? binding)
            where TModel : class
        {
            if (binding is null)
                return null;

            var accessor = new TreeDataGridBindingAccessor(binding);
            return model => accessor.ReadAsString(model);
        }

        public object? Read(object model)
        {
            SetModel(model);
            _expression?.UpdateTarget();
            var value = NormalizeValue(_probe.Value);

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
            return ConvertToBoolean(Read(model));
        }

        public bool? ReadAsNullableBoolean(object model)
        {
            return ConvertToNullableBoolean(Read(model));
        }

        public void Write(object model, object? value)
        {
            if (!CanWrite)
                return;

            SetModel(model);
            _probe.SetCurrentValue(TreeDataGridBindingProbe.ValueProperty, ConvertForSource(value));
            _expression?.UpdateSource();
            _expression?.UpdateTarget();

            var writtenValue = NormalizeValue(_probe.Value);

            if (writtenValue is not null)
                _inferredType ??= writtenValue.GetType();
        }

        public void Dispose()
        {
            _bindingDisposable.Dispose();
        }

        private static BindingMode GetMode(BindingBase binding)
        {
            return binding switch
            {
                Binding x => NormalizeMode(x.Mode),
                ReflectionBinding x => NormalizeMode(x.Mode),
                CompiledBinding x => NormalizeMode(x.Mode),
                MultiBinding x => NormalizeMode(x.Mode),
                _ => BindingMode.TwoWay,
            };
        }

        private static BindingMode NormalizeMode(BindingMode mode)
        {
            return mode == BindingMode.Default ? BindingMode.TwoWay : mode;
        }

        private static object? NormalizeValue(object? value)
        {
            return value switch
            {
                BindingNotification notification when notification.HasValue => NormalizeValue(notification.Value),
                BindingNotification => null,
                var x when ReferenceEquals(x, AvaloniaProperty.UnsetValue) => null,
                _ => value,
            };
        }

        private static bool ConvertToBoolean(object? value)
        {
            return ConvertToNullableBoolean(value) ?? false;
        }

        private static bool? ConvertToNullableBoolean(object? value)
        {
            value = NormalizeValue(value);

            return value switch
            {
                null => null,
                bool b => b,
                _ => (bool?)Convert.ChangeType(value, typeof(bool), CultureInfo.CurrentCulture),
            };
        }

        private object? ConvertForSource(object? value)
        {
            value = NormalizeValue(value);

            if (value is null)
                return null;

            var targetType = ValueType;

            if (targetType is null)
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

            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(underlyingType))
                return Convert.ChangeType(value, underlyingType, CultureInfo.CurrentCulture);

            return value;
        }

        private void SetModel(object model)
        {
            if (!ReferenceEquals(_probe.DataContext, model))
                _probe.DataContext = model;
        }

        private static Func<object, object>[]? TryCreateLinks(BindingBase binding, Type? modelType)
        {
            return binding switch
            {
                CompiledBinding x => TryCreateCompiledBindingLinks(x),
                ReflectionBinding x => TryCreateReflectionBindingLinks(x, modelType),
                _ => null,
            };
        }

        private static Type? TryGetValueType(BindingBase binding, Type? modelType)
        {
            return binding switch
            {
                CompiledBinding x => TryGetCompiledBindingValueType(x),
                ReflectionBinding x => TryGetReflectionBindingValueType(x, modelType),
                _ => null,
            };
        }

        private static Func<object, object>[]? TryCreateCompiledBindingLinks(CompiledBinding binding)
        {
            var properties = TryGetCompiledBindingProperties(binding);
            return properties is not null ? CreateLinks(properties) : null;
        }

        private static Type? TryGetCompiledBindingValueType(CompiledBinding binding)
        {
            var properties = TryGetCompiledBindingProperties(binding);
            return properties is { Count: > 0 } ? properties[properties.Count - 1].PropertyType : null;
        }

        private static IReadOnlyList<IPropertyInfo>? TryGetCompiledBindingProperties(CompiledBinding binding)
        {
            var elements = s_compiledBindingPathElementsProperty?.GetValue(binding.Path) as IEnumerable;

            if (elements is null)
                return null;

            var result = new List<IPropertyInfo>();

            foreach (var element in elements)
            {
                if (element is null)
                    continue;

                var property = element.GetType().GetProperty(
                    "Property",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (property?.GetValue(element) is IPropertyInfo propertyInfo)
                {
                    result.Add(propertyInfo);
                    continue;
                }

                var typeName = element.GetType().Name;

                if (typeName != "SelfPathElement" &&
                    typeName != "TypeCastPathElement" &&
                    !typeName.StartsWith("TypeCastPathElement`", StringComparison.Ordinal))
                {
                    return null;
                }
            }

            return result;
        }

        private static Func<object, object>[]? TryCreateReflectionBindingLinks(ReflectionBinding binding, Type? modelType)
        {
            if (GetReflectionBindingSource(binding) is not null ||
                binding.RelativeSource is not null ||
                !string.IsNullOrEmpty(binding.ElementName))
            {
                return null;
            }

            var members = TreeDataGridMemberPath.TryResolve(modelType, binding.Path);
            return members is not null ? TreeDataGridMemberPath.CreateSubscriptionLinks(members) : null;
        }

        private static Type? TryGetReflectionBindingValueType(ReflectionBinding binding, Type? modelType)
        {
            var rootType = GetReflectionBindingSource(binding)?.GetType() ?? modelType;
            var members = TreeDataGridMemberPath.TryResolve(rootType, binding.Path);
            return TreeDataGridMemberPath.TryGetValueType(members, rootType);
        }

        private static object? GetReflectionBindingSource(ReflectionBinding binding)
        {
            return ReferenceEquals(binding.Source, AvaloniaProperty.UnsetValue) ? null : binding.Source;
        }

        private static Func<object, object>[] CreateLinks(IReadOnlyList<IPropertyInfo> properties)
        {
            if (properties.Count == 0)
                return TreeDataGridMemberPath.CreateProgressiveLinks(Array.Empty<string>());

            var result = new Func<object, object>[properties.Count];
            result[0] = x => x;

            for (var i = 1; i < properties.Count; ++i)
            {
                var length = i;
                result[i] = x => EvaluateProperties(x, properties, length);
            }

            return result;
        }

        private static object EvaluateProperties(object model, IReadOnlyList<IPropertyInfo> properties, int length)
        {
            object? current = model;

            for (var i = 0; i < length && current is not null; ++i)
                current = properties[i].Get(current);

            return current!;
        }

    }
}
