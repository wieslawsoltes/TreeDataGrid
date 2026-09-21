using System;
using Microsoft.UI.Xaml;

namespace Uno.Controls.Presentation;

internal static partial class NativeBindingPathWriter
{
    private static Endpoint? TryResolveMetadataIndexer(object owner, string token)
    {
#if !WINDOWS
        var type = global::Uno.UI.DataBinding.BindableMetadata.Provider?.GetBindableTypeByType(owner.GetType());
        var getter = type?.GetIndexerGetter();
        var setter = type?.GetIndexerSetter();
        if (getter is null && setter is null) return null;
        // Uno's generated getter contract uses a string key, including numeric
        // tokens. Match the native reader's quote coercion; do not redirect its
        // write to an unrelated CLR int overload merely because one exists.
        var key = StringKey(token);
        // Generated metadata does not expose an indexer's declared value type.
        // Use a matching public declaration when retained; generated-only/AOT
        // endpoints use the native object contract without guessing from a value.
        var valueType = FindIndexer(owner.GetType(), typeof(string))?.PropertyType ?? typeof(object);
        return new(valueType,
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
        // Use the same public generated metadata as Uno's binding engine before
        // reflection. Do not cache providers, model instances or generated
        // delegates globally: applications can replace their metadata provider.
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
        // Windows App SDK does not expose Uno's metadata provider. Its native
        // binding engine remains responsible for target evaluation/observation.
        return null;
#endif
    }
}
