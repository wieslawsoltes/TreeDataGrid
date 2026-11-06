using System;
using Microsoft.UI.Xaml.Data;
using Uno.Controls.Presentation;

namespace Uno.Controls;

// Binding evaluation belongs to the native UI thread, like the corresponding
// Avalonia binding probe. Do not approximate WinUI converters/paths via reflection.
internal static class TreeDataGridBindingAccessor
{
    internal static Func<TModel, string?>? TryCreateTextSelector<TModel>(Binding? binding) where TModel : class
    {
        if (binding is null) return null;
        // Construct lazily: source-building need not construct native UI objects.
        NativeColumnBinding? probe = null;
        return model =>
        {
            probe ??= new NativeColumnBinding(binding);
            // Snapshot cleanup also disconnects explicit Source bindings; merely
            // setting DataContext to null leaves those observers subscribed.
            return probe.ReadSnapshot(model)?.ToString();
        };
    }
}
