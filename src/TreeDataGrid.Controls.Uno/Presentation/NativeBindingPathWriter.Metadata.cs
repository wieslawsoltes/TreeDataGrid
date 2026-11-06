using System;
using Microsoft.UI.Xaml;

namespace Uno.Controls.Presentation;

internal static partial class NativeBindingPathWriter
{
    private static Type? GetMetadataPropertyType(Type type, string name)
    {
#if !WINDOWS
        return global::Uno.UI.DataBinding.BindableMetadata.Provider?.GetBindableTypeByType(type)?.GetProperty(name)?.PropertyType;
#else
        return null;
#endif
    }

    private static Endpoint? TryResolveMetadataIndexer(object owner, string token)
    {
#if !WINDOWS
        var type = global::Uno.UI.DataBinding.BindableMetadata.Provider?.GetBindableTypeByType(owner.GetType());
        var getter = type?.GetIndexerGetter();
        var setter = type?.GetIndexerSetter();
        if (getter is null && setter is null) return null;
        var key = StringKey(token);
        // Native generated indexers use string keys; do not redirect to an
        // unrelated int overload. Registration supplies the declared value type.
        var valueType = TreeDataGridBindingRegistry.FindIndexer(owner.GetType(), typeof(string))?.Accessor.ValueType;
        if (valueType is null && BindingFeatures.ReflectionEnabled)
            valueType = FindIndexer(owner.GetType(), typeof(string))?.PropertyType;
        return new(valueType ?? typeof(object),
            () => getter is not null ? getter(owner, key) :
                throw new InvalidOperationException("The generated bound indexer is not readable."),
            value =>
            {
                if (setter is null) throw new InvalidOperationException("The generated bound indexer is read-only.");
                setter(owner, key, value);
            });
#else
        return null;
#endif
    }

    private static Endpoint? TryResolveMetadata(object owner, string name)
    {
#if !WINDOWS
        // A host may replace its provider. Never cache provider instances or
        // model owners in the global syntax/registration caches.
        var property = global::Uno.UI.DataBinding.BindableMetadata.Provider?
            .GetBindableTypeByType(owner.GetType())?.GetProperty(name);
        if (property is null) return null;
        if (property.DependencyProperty is { } dependencyProperty)
        {
            if (owner is not DependencyObject dependencyOwner)
                throw new InvalidOperationException("A generated dependency property requires a native dependency-object owner.");
            return new(property.PropertyType, () => dependencyOwner.GetValue(dependencyProperty),
                value => dependencyOwner.SetValue(dependencyProperty, value));
        }
        return new(property.PropertyType,
            () => property.Getter is { } getter ? getter(owner, null) :
                throw new InvalidOperationException("The generated bound property is not readable."),
            value =>
            {
                if (property.Setter is not { } setter)
                    throw new InvalidOperationException("The generated bound property is read-only.");
                setter(owner, value, null);
            });
#else
        return null;
#endif
    }
}
