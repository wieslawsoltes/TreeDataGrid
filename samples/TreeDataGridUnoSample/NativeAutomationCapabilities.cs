using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;

namespace TreeDataGridUnoSample;

internal static class NativeAutomationCapabilities
{
    internal static bool SupportsLiveRegions { get; } = DetectLiveRegions();

    private static bool DetectLiveRegions()
    {
#if WINDOWS
        return Windows.Foundation.Metadata.ApiInformation.IsMethodPresent(
            "Microsoft.UI.Xaml.Automation.AutomationProperties", "SetLiveSetting");
#else
        // A statically known Type and method signature are analyzable by the
        // browser trimmer. Building AssemblyQualifiedName dynamically is not.
        // Uno marks unavailable members with NotImplementedAttribute; absence
        // must remain false rather than invoking the platform's generated stub.
        var method = typeof(AutomationProperties).GetMethod("SetLiveSetting",
            new[] { typeof(DependencyObject), typeof(AutomationLiveSetting) });
        return method is not null && !method.IsDefined(typeof(global::Uno.NotImplementedAttribute), false);
#endif
    }
}
