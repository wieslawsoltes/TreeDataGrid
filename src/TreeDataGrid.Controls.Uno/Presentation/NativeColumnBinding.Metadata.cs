using Microsoft.UI.Xaml;

namespace Uno.Controls.Presentation;

internal sealed partial class NativeColumnBinding
{
    static NativeColumnBinding()
    {
        // RelativeSource Self is resolved natively to this library's private
        // probe. Its inherited DataContext is not a generated model endpoint.
        // Preserve that one owned contract for trimmed/AOT writeback without
        // reopening arbitrary reflection or changing user-model registrations.
        TreeDataGridBindingRegistry.RegisterProperty<Probe, object?>(nameof(FrameworkElement.DataContext),
            static probe => probe.DataContext,
            static (probe, value) => probe.DataContext = value);
    }
}
