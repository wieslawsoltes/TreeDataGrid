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
/// panel therefore stretches the text block when the cell has vertical slack, so it is
/// arranged with the size it was measured with, and places it at the offset centering would
/// produce. Like a single-cell <see cref="Grid"/>, every child otherwise fills the panel.
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
            if (child is TextBlock { Name: "PART_Text" } text && text.VerticalAlignment != VerticalAlignment.Top &&
                text.VerticalAlignment != VerticalAlignment.Bottom)
            {
                var margin = text.Margin;
                var client = finalSize.Height - margin.Top - margin.Bottom;
                var content = text.DesiredSize.Height - margin.Top - margin.Bottom;
                // With vertical slack (fixed row heights), stretch the text so it keeps its
                // measured layout and place it at the centering offset. A cell sized to its
                // text has no slack; native centering then gives the exact reference geometry.
                var stretch = client - content > 0.01;
                var alignment = stretch ? VerticalAlignment.Stretch : VerticalAlignment.Center;
                if (text.VerticalAlignment != alignment) text.VerticalAlignment = alignment;
                if (stretch)
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
