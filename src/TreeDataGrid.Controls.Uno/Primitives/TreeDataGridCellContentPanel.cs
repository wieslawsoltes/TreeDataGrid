using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Uno.Controls.Primitives;

/// <summary>
/// Hosts a cell's content and vertically centers its stretched <c>PART_Text</c> text block.
/// </summary>
/// <remarks>
/// A text block re-creates its text layout during arrange whenever the arranged size differs
/// from its measure constraint. With vertical centering that happens for every cell. The
/// template therefore stretches the text block, so it is arranged with the size it was
/// measured with, and this panel moves it to the offset that centering would produce.
/// </remarks>
public class TreeDataGridCellContentPanel : Grid
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        var children = Children;
        for (var i = 0; i < children.Count; ++i)
        {
            if (children[i] is TextBlock { Name: "PART_Text", Visibility: Visibility.Visible, VerticalAlignment: VerticalAlignment.Stretch } text)
            {
                var margin = text.Margin;
                var client = finalSize.Height - margin.Top - margin.Bottom;
                var content = text.DesiredSize.Height - margin.Top - margin.Bottom;
                // Content taller than the cell keeps the normal stretched, clipped arrangement.
                if (content < client)
                    text.Arrange(new Rect(0, (client - content) / 2, finalSize.Width, finalSize.Height));
            }
        }
        return result;
    }
}
