using System;

namespace Uno.Controls.Presentation;

/// <summary>Allocation-free row buffer geometry, independent of the native visual tree.</summary>
internal readonly record struct RowViewport(double Start, double End)
{
    public static RowViewport Calculate(double offset, double height, double extent, double cacheLength)
    {
        if (cacheLength is not (>= 0 and <= 2)) throw new ArgumentOutOfRangeException(nameof(cacheLength));
        offset = Math.Clamp(offset, 0, extent);
        height = Math.Max(0, height);
        var bottom = Math.Min(extent, offset + height);
        if (cacheLength == 0 || height == 0) return new(offset, bottom);
        var buffer = height * cacheLength;
        var start = Math.Max(0, offset - buffer);
        var end = Math.Min(extent, bottom + buffer);
        var missingBefore = buffer - (offset - start);
        var missingAfter = buffer - (end - bottom);
        // Avalonia redistributes the unavailable buffer to the opposite edge.
        if (missingBefore > 0) end = Math.Min(extent, end + missingBefore);
        else if (missingAfter > 0) start = Math.Max(0, start - missingAfter);
        return new(start, end);
    }

    public (int First, int End) GetRows(RowGeometry rows)
    {
        var first = rows.RowAt(Start);
        if (End <= Start) return (first, first);
        var end = rows.RowAt(End);
        // End is exclusive: do not realize another row at an exact boundary.
        if (end < rows.Count && rows.Start(end) < End) ++end;
        return (first, Math.Max(first, end));
    }
}
