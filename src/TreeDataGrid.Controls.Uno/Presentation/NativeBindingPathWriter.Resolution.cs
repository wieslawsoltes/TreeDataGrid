using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Uno.Controls.Presentation;

internal static partial class NativeBindingPathWriter
{
    private static Endpoint Resolve(object owner, Segment segment)
    {
        var type = owner.GetType();
        if (!segment.Index)
        {
            if (TryResolveMetadata(owner, segment.Name) is { } generated) return generated;
            if (TreeDataGridBindingRegistry.FindProperty(type, segment.Name) is { } registered)
                return Bind(owner, null, registered);
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
            if (FindRegisteredIndexer(type, segment.Name) is { } registered)
                return Bind(owner, ConvertKey(segment.Name, registered.Key), registered.Accessor);
        }
        if (BindingFeatures.ReflectionEnabled && ResolveReflected(owner, segment) is { } reflected) return reflected;
        var dynamicKey = segment.Index ? StringKey(segment.Name) : segment.Name;
        if (owner is IDictionary<string, object?> dynamicValues)
            return new(typeof(object), () => dynamicValues[dynamicKey], value => dynamicValues[dynamicKey] = value);
        if (owner is IDictionary dictionary)
            return new(typeof(object), () => dictionary[dynamicKey], value => dictionary[dynamicKey] = value);
        throw new InvalidOperationException($"No public binding endpoint '{segment.Name}' exists on '{type.Name}'. " +
            "Trimmed models require generated metadata or TreeDataGridBindingRegistry registration.");
    }

    private static Endpoint Bind(object owner, object? key, TreeDataGridBindingRegistry.Accessor accessor) => new(
        accessor.ValueType,
        () => accessor.Get is { } getter ? getter(owner, key) : throw new InvalidOperationException("The bound endpoint is not readable."),
        value =>
        {
            if (accessor.Set is not { } setter) throw new InvalidOperationException("The bound endpoint is read-only.");
            setter(owner, key, value);
        });

    private static (Type Key, TreeDataGridBindingRegistry.Accessor Accessor)? FindRegisteredIndexer(Type type, string token)
    {
        var numeric = int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        return TreeDataGridBindingRegistry.FindIndexer(type, numeric ? typeof(int) : typeof(string)) ??
            (numeric ? TreeDataGridBindingRegistry.FindIndexer(type, typeof(string)) : null) ??
            TreeDataGridBindingRegistry.FindIndexer(type, null);
    }
}
