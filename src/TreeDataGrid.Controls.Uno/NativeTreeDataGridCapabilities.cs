using Windows.Foundation.Metadata;

namespace Uno.Controls;

/// <summary>Implemented native contracts, resolved once with linker-visible type identities.</summary>
internal static class NativeTreeDataGridCapabilities
{
#if WINDOWS
    // Windows' ApiInformation consumes WinRT names, not CLR assembly identities.
    private const string ControlType = "Microsoft.UI.Xaml.Controls.Control";
    private const string ElementType = "Microsoft.UI.Xaml.UIElement";
    private const string AnchorType = "Microsoft.UI.Xaml.Controls.IScrollAnchorProvider";
#else
    // Uno's implementation resolves managed metadata. A constant, qualified
    // identity lets ILLink preserve the inspected members without IL2122 or
    // an opaque Type.AssemblyQualifiedName computed at runtime.
    private const string ControlType = "Microsoft.UI.Xaml.Controls.Control, Uno.UI";
    private const string ElementType = "Microsoft.UI.Xaml.UIElement, Uno.UI";
    private const string AnchorType = "Microsoft.UI.Xaml.Controls.IScrollAnchorProvider, Uno.UI";
#endif

    internal static bool CharacterSpacing { get; } =
        ApiInformation.IsPropertyPresent(ControlType, "CharacterSpacingProperty");
    internal static bool TextScaleFactor { get; } =
        ApiInformation.IsPropertyPresent(ControlType, "IsTextScaleFactorEnabledProperty");
    internal static bool CharacterReceived { get; } =
        ApiInformation.IsEventPresent(ElementType, "CharacterReceived");
    internal static bool ScrollAnchoring { get; } =
        ApiInformation.IsMethodPresent(AnchorType, "RegisterAnchorCandidate") &&
        ApiInformation.IsMethodPresent(AnchorType, "UnregisterAnchorCandidate");
}
