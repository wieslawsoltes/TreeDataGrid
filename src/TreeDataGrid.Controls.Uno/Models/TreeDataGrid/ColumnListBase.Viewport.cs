using System;
using System.Buffers;
using System.Collections.Generic;

namespace Uno.Controls.Models.TreeDataGrid;

public partial class ColumnListBase<TColumn> where TColumn : class, IColumn
{
    // Keep the raw strictly-positive prefix as well as the nonnegative hit-test
    // prefix. A shifted origin must add original widths in order: subtracting
    // cumulative ends or translating a prefix sum changes floating-point results.
    private readonly List<double> _viewportWidths = new();

    private (int index, double position) GetViewportAnchor(
        double viewportStart, double viewportEnd, int itemCount,
        double realizedStart, int firstRealizedIndex, ref double previousEstimate)
    {
        if (itemCount <= 0 || Count == 0) return (-1, 0);
        if (LayoutMath.IsZero(viewportStart)) return (0, 0);

        EnsureGeometry();
        // The caller's count can precede a structural mutation performed by a
        // width getter. Never publish a column index beyond the current list.
        itemCount = Math.Min(itemCount, Count);
        if (itemCount <= 0) return (-1, 0);

        var prefixCount = (uint)firstRealizedIndex < (uint)itemCount
            ? Math.Min(_viewportWidths.Count, itemCount - firstRealizedIndex) : 0;
        if (realizedStart == 0)
        {
            // Same additions as the reference scan, but an upper bound replaces
            // repeated scans of an already validated, monotonically ordered prefix.
            var low = 0;
            var high = prefixCount;
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (_columnEnds[middle] > viewportStart) high = middle;
                else low = middle + 1;
            }
            if (low < prefixCount)
            {
                var position = low == 0 ? 0 : _columnEnds[low - 1];
                if (_columnEnds[low] > viewportStart && position < viewportEnd)
                    return (firstRealizedIndex + low, position);
            }
        }
        else
        {
            // Nonzero origins retain the reference's sequential arithmetic.
            // These are snapshot values, never application-controlled getters.
            var position = realizedStart;
            for (var i = 0; i < prefixCount; ++i)
            {
                var end = position + _viewportWidths[i];
                if (end > viewportStart && position < viewportEnd)
                    return (firstRealizedIndex + i, position);
                position = end;
            }
        }

        // Unmeasured/zero-width prefixes still use all positive measured columns
        // for their mean. A nonfinite mean or caller estimate is not a usable scale.
        var estimate = _estimatedElementSize;
        if (!(estimate > 0) || !double.IsFinite(estimate)) estimate = previousEstimate;
        if (!(estimate > 0) || !double.IsFinite(estimate)) estimate = 25;
        previousEstimate = estimate;

        // Clamp in floating-point space before conversion. The result must not
        // depend on runtime-specific out-of-range double-to-int conversion rules.
        var quotient = viewportStart / estimate;
        var lastIndex = itemCount - 1;
        var index = !(quotient > 0) ? 0 : quotient >= lastIndex ? lastIndex : (int)quotient;
        var estimatedPosition = index * estimate;
        return (index, double.IsFinite(estimatedPosition) ? estimatedPosition : double.MaxValue);
    }

    private void BuildGeometrySnapshot()
    {
        var revision = _geometryRevision;
        var count = Count;
        var scratchLength = checked(count * 2);
        double[]? rented = null;
        Span<double> scratch = count <= 128 ? stackalloc double[scratchLength] :
            (rented = ArrayPool<double>.Shared.Rent(scratchLength)).AsSpan(0, scratchLength);
        var ends = scratch[..count];
        var widths = scratch[count..];
        try
        {
            var end = 0.0;
            var total = 0.0;
            var measuredCount = 0;
            var prefixCount = 0;
            var viewportCount = 0;
            var knownPrefix = true;
            var positivePrefix = true;
            for (var i = 0; i < count; ++i)
            {
                var width = this[i].ActualWidth;
                if (revision != _geometryRevision) return;
                // An ActualWidth getter can mutate columns or recursively commit
                // a new snapshot. Neither partial prefix may escape that callback.
                knownPrefix &= !double.IsNaN(width) && width >= 0;
                if (knownPrefix)
                {
                    end += width;
                    ends[prefixCount++] = end;
                }
                positivePrefix &= width > 0;
                if (positivePrefix) widths[viewportCount++] = width;
                if (!double.IsNaN(width) && width > 0)
                {
                    total += width;
                    ++measuredCount;
                }
            }

            // Reserve both buffers before replacing either committed snapshot.
            // Publication contains no application callbacks or storage allocation.
            _columnEnds.EnsureCapacity(prefixCount);
            _viewportWidths.EnsureCapacity(viewportCount);
            _columnEnds.Clear();
            _viewportWidths.Clear();
            for (var i = 0; i < prefixCount; ++i) _columnEnds.Add(ends[i]);
            for (var i = 0; i < viewportCount; ++i) _viewportWidths.Add(widths[i]);
            _estimatedElementSize = measuredCount > 0 ? total / measuredCount : -1;
            _geometryDirty = false;
            unchecked { ++_geometryRevision; }
        }
        finally { if (rented is not null) ArrayPool<double>.Shared.Return(rented); }
    }
}
