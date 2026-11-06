using System;
using System.Diagnostics.CodeAnalysis;

namespace Uno.Controls.Presentation;

internal static class BindingFeatures
{
    [FeatureSwitchDefinition("TreeDataGrid.Binding.ReflectionEnabled")]
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    internal static bool ReflectionEnabled =>
        !AppContext.TryGetSwitch("TreeDataGrid.Binding.ReflectionEnabled", out var enabled) || enabled;
}
