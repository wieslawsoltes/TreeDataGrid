using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Avalonia.Controls.Internal
{
    internal static class VisualTreeHelpers
    {
        public static T? FindAncestorOrSelf<T>(DependencyObject? current)
            where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T match)
                    return match;

                current = VisualTreeHelper.GetParent(current);
            }

            return default;
        }
    }
}
