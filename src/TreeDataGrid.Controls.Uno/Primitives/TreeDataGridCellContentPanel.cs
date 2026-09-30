using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

namespace Uno.Controls.Primitives;

/// <summary>
/// Hosts a cell's overlapping content layers and vertically centers its <c>PART_Text</c>
/// text block without re-creating its text layout.
/// </summary>
/// <remarks>
/// A text block re-creates its text layout during arrange whenever the arranged size differs
/// from its measure constraint, which vertical centering causes whenever the cell has vertical
/// slack (fixed row heights). In that case the text block is stretched, so it is arranged with
/// the size it was measured with, and moved to the offset centering would produce. A cell sized
/// to its text has no slack and keeps the native centered arrangement, so its geometry is
/// exactly that of a single-cell <see cref="Grid"/>.
/// </remarks>
public class TreeDataGridCellContentPanel : Grid
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        TextBlock? stretched = null;
        var children = Children;
        for (var i = 0; i < children.Count; ++i)
        {
            if (children[i] is TextBlock { Name: "PART_Text" } text &&
                text.VerticalAlignment is VerticalAlignment.Center or VerticalAlignment.Stretch)
            {
                var margin = text.Margin;
                var client = finalSize.Height - margin.Top - margin.Bottom;
                var content = text.DesiredSize.Height - margin.Top - margin.Bottom;
                var stretch = client - content > 0.01;
                var alignment = stretch ? VerticalAlignment.Stretch : VerticalAlignment.Center;
                if (text.VerticalAlignment != alignment) text.VerticalAlignment = alignment;
                if (stretch) stretched = text;
                break;
            }
        }
        var result = base.ArrangeOverride(finalSize);
        if (stretched is not null)
        {
            // Use the slot the grid assigned (including its layout rounding), so the
            // position equals what VerticalAlignment.Center would produce in that slot.
            var slot = LayoutInformation.GetLayoutSlot(stretched);
            var margin = stretched.Margin;
            var client = slot.Height - margin.Top - margin.Bottom;
            var content = stretched.DesiredSize.Height - margin.Top - margin.Bottom;
            if (client > content)
            {
                // Centering rounds offset + margin once. Arrange also rounds the slot
                // position before adding the margin, so pass the already-rounded result:
                // a midpoint offset would otherwise round twice and land one pixel higher.
                var top = slot.Y + (client - content) / 2;
                if (UseLayoutRounding)
                {
                    var scale = XamlRoot?.RasterizationScale ?? 1;
                    top = Math.Round((top + margin.Top) * scale) / scale - margin.Top;
                }
                stretched.Arrange(new Rect(slot.X, top, slot.Width, slot.Height));
            }
        }
        return result;
    }
}
