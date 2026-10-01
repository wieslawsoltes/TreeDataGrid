#if !WINDOWS
using System.Windows.Markup;
#endif

// TreeDataGrid types resolve without an xmlns prefix, as with Avalonia's
// [XmlnsDefinition("https://github.com/avaloniaui", ...)]: Uno's XAML generator searches the
// namespaces registered for its global URI after the WinUI ones (Uno.Sdk enables this through
// UnoEnableImplicitXamlNamespaces). buildTransitive/TreeDataGrid.Controls.Uno.targets covers what
// that lookup does not: TargetType and Setter.Property values, and Windows App SDK heads.
// XmlnsDefinitionAttribute is Uno's; the Windows App SDK build relies on the rewrite alone.
#if !WINDOWS
[assembly: XmlnsDefinition(Uno.Controls.TreeDataGridXamlNamespaces.Global, "Uno.Controls")]
[assembly: XmlnsDefinition(Uno.Controls.TreeDataGridXamlNamespaces.Global, "Uno.Controls.Primitives")]
#endif

namespace Uno.Controls
{
    internal static class TreeDataGridXamlNamespaces
    {
        /// <summary>Uno's global XAML namespace: types registered here resolve unprefixed.</summary>
        public const string Global = "http://schemas.microsoft.com/winfx/2006/xaml/presentation/global";
    }
}
