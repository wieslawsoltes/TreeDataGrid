using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Uno.Controls.Primitives;

/// <summary>
/// Hosts a cell's overlapping content layers and vertically centers its stretched
/// <c>PART_Text</c> text block.
/// </summary>
/// <remarks>
/// A text block re-creates its text layout during arrange whenever the arranged size differs
/// from its measure constraint. With vertical centering that happens for every cell. The
/// template therefore stretches the text block, so it is arranged with the size it was
/// measured with, and this panel places it at the offset centering would produce. Like a
/// single-cell <see cref="Grid"/>, every child otherwise fills the panel.
/// </remarks>
public class TreeDataGridCellContentPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            var desired = child.DesiredSize;
            width = Math.Max(width, desired.Width);
            height = Math.Max(height, desired.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var bounds = new Rect(0, 0, finalSize.Width, finalSize.Height);
        foreach (var child in Children)
        {
            if (child is TextBlock { Name: "PART_Text", VerticalAlignment: VerticalAlignment.Stretch } text)
            {
                var margin = text.Margin;
                var client = finalSize.Height - margin.Top - margin.Bottom;
                var content = text.DesiredSize.Height - margin.Top - margin.Bottom;
                // Same offset as VerticalAlignment.Center; content taller than the cell
                // keeps the normal stretched, clipped arrangement.
                if (content < client)
                {
                    text.Arrange(new Rect(0, (client - content) / 2, finalSize.Width, finalSize.Height));
                    continue;
                }
            }
            child.Arrange(bounds);
        }
        return finalSize;
    }
}
