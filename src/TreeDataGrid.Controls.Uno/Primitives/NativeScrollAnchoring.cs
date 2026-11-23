using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Uno.Controls.Primitives;

// The specialized presenters retain their own source/viewport anchors.
// This optional bridge uses native anchoring only on implemented heads.
// Cache metadata once, not for every realized row/cell.
internal static class NativeScrollAnchoring
{
    internal static bool IsSupported => NativeTreeDataGridCapabilities.ScrollAnchoring;

    internal static void Register(IScrollAnchorProvider? provider, UIElement element)
    {
        if (!IsSupported || provider is null) return;
#pragma warning disable Uno0001 // The implemented native capability is checked above.
        provider.RegisterAnchorCandidate(element);
#pragma warning restore Uno0001
    }
    internal static void Unregister(IScrollAnchorProvider? provider, UIElement element)
    {
        if (!IsSupported || provider is null) return;
#pragma warning disable Uno0001 // The implemented native capability is checked above.
        provider.UnregisterAnchorCandidate(element);
#pragma warning restore Uno0001
    }
}
