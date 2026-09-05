using System;
using System.Collections.Generic;
using TreeDataGridCore;

namespace Uno.Controls.Presentation;

/// <summary>
/// Per-presentation cumulative geometry. Width commits rebuild it once; viewport
/// lookups use upper-bound search and skip zero-width columns at exact boundaries.
/// </summary>
internal sealed class ColumnGeometry
{
    private double[] _ends = [];
    public int Count => _ends.Length;
    public double TotalWidth => _ends.Length == 0 ? 0 : _ends[^1];

    public void Commit(IReadOnlyList<double> widths)
    {
        var total = 0d;
        // Validate before mutating the committed geometry.
        foreach (var width in widths)
        {
            if (!double.IsFinite(width) || width < 0)
                throw new ArgumentOutOfRangeException(nameof(widths));
            total += width;
            if (!double.IsFinite(total))
                throw new ArgumentOutOfRangeException(nameof(widths));
        }
        if (_ends.Length != widths.Count) _ends = new double[widths.Count];
        total = 0;
        for (var i = 0; i < widths.Count; ++i)
            _ends[i] = total += widths[i];
    }

    public double Start(int index) => index == 0 ? 0 : _ends[index - 1];
    public double Width(int index) => _ends[index] - Start(index);

    public int ColumnAt(double x)
    {
        if (!double.IsFinite(x) || x < 0 || x >= TotalWidth) return -1;
        var low = 0;
        var high = _ends.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_ends[middle] <= x) low = middle + 1;
            else high = middle;
        }
        return low < _ends.Length ? low : -1;
    }

    public (int Start, int End) VisibleRange(double offset, double viewportWidth)
    {
        if (!double.IsFinite(offset) || !double.IsFinite(viewportWidth) || viewportWidth <= 0)
            return (0, 0);
        var start = ColumnAt(Math.Max(0, offset));
        var right = offset + viewportWidth;
        if (start < 0 || right <= 0) return (0, 0);
        var end = right >= TotalWidth ? Count : ColumnAt(Math.BitDecrement(right)) + 1;
        return (start, Math.Max(start, end));
    }

    public static double Constrain(double width, GridLength minimum, GridLength? maximum, double measured)
    {
        var min = minimum.IsAuto ? measured : minimum.Value;
        var max = maximum is { } limit ? limit.IsAuto ? measured : limit.Value : double.PositiveInfinity;
        if (minimum.IsStar || maximum?.IsStar == true)
            throw new ArgumentException("Column minimum and maximum widths must use pixels or Auto.");
        // Avalonia applies minimum first, then maximum (maximum wins conflicts).
        return Math.Min(max, Math.Max(min, width));
    }
}
