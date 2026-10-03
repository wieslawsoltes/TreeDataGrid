using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;

namespace Uno.Controls.Primitives;

internal partial class RealizedStackElements
{
    /// <summary>
    /// Publishes the final realization range for a count-changing replacement
    /// before invoking any index or retirement callback. Surviving controls are
    /// never temporarily assigned the indices of a remove-then-insert sequence.
    /// </summary>
    public void ItemsReplaced(int index, int oldCount, int newCount,
        Action<Control, int, int> updateElementIndex,
        Action<Control> recycleRemovedElement,
        Action<Control, int> recycleDisplacedElement)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (oldCount < 0 || oldCount > int.MaxValue - index) throw new ArgumentOutOfRangeException(nameof(oldCount));
        if (newCount < 0 || newCount > int.MaxValue - index) throw new ArgumentOutOfRangeException(nameof(newCount));
        ArgumentNullException.ThrowIfNull(updateElementIndex);
        ArgumentNullException.ThrowIfNull(recycleRemovedElement);
        ArgumentNullException.ThrowIfNull(recycleDisplacedElement);
        if (oldCount == newCount)
        {
            ItemsReplaced(index, oldCount, recycleRemovedElement);
            return;
        }
        if (_elements is not { Count: > 0 } previous || index > LastIndex) return;
        var first = _firstIndex;
        var oldEnd = index + oldCount;
        var delta = newCount - oldCount;

        if (oldEnd <= first)
        {
            // No affected controls: shift the complete retained window in place.
            _firstIndex = checked(first + delta);
            _startUUnstable = true;
            for (var i = 0; i < previous.Count; ++i)
                if (previous[i] is { } element)
                    updateElementIndex(element, first + i, checked(first + i + delta));
            return;
        }

        var sizes = _sizes!;
        var prefixCount = Math.Clamp(index - first, 0, previous.Count);
        var suffixStart = Math.Clamp(oldEnd - first, 0, previous.Count);
        var suffixCount = previous.Count - suffixStart;
        // A very large insertion must not allocate a list proportional to the
        // source change. The displaced suffix uses ordinary recycling so a
        // focused survivor can remain in the presenter's separate focus lease.
        var retainSuffix = prefixCount == 0 || newCount < suffixCount;
        var gap = prefixCount > 0 && retainSuffix && suffixCount > 0 ? newCount : 0;
        var retainedCount = checked(prefixCount + gap + (retainSuffix ? suffixCount : 0));
        var nextElements = new List<Control?>(retainedCount);
        var nextSizes = new List<double>(retainedCount);
        for (var i = 0; i < prefixCount; ++i)
        {
            nextElements.Add(previous[i]);
            nextSizes.Add(sizes[i]);
        }
        for (var i = 0; i < gap; ++i)
        {
            nextElements.Add(null);
            nextSizes.Add(double.NaN);
        }
        if (retainSuffix)
            for (var i = suffixStart; i < previous.Count; ++i)
            {
                nextElements.Add(previous[i]);
                nextSizes.Add(sizes[i]);
            }
        var nextFirst = prefixCount > 0 ? first :
            retainSuffix && suffixCount > 0 ? checked(first + suffixStart + delta) : 0;
        if (retainedCount > 0) _ = checked(nextFirst + retainedCount - 1);

        // Publish owned storage once. The old lists are detached snapshots.
        _elements = nextElements;
        _sizes = nextSizes;
        _firstIndex = nextFirst;
        _startUUnstable = true;
        if (retainedCount == 0) _startU = 0;

        for (var i = prefixCount; i < suffixStart; ++i)
            if (previous[i] is { } element) recycleRemovedElement(element);
        for (var i = suffixStart; i < previous.Count; ++i)
        {
            if (previous[i] is not { } element) continue;
            var oldIndex = first + i;
            var newIndex = checked(oldIndex + delta);
            updateElementIndex(element, oldIndex, newIndex);
            if (!retainSuffix) recycleDisplacedElement(element, newIndex);
        }
    }
}
