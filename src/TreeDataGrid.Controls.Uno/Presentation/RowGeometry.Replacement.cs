using System;
using System.Collections.Generic;

namespace Uno.Controls.Presentation;

internal sealed partial class RowGeometry
{
    /// <summary>Replaces a contiguous range, preserving measured prefix and suffix rows.</summary>
    /// <remarks>
    /// Uniform replacements require no per-row storage. Unequal sparse replacements
    /// prepare both indexes before publication, so invalid extents cannot leave a
    /// partially shifted height map. Equal-size replacements retain Invalidate's fast path.
    /// </remarks>
    public void Replace(int index, int oldCount, int newCount)
    {
        if (oldCount < 0 || oldCount > Count) throw new ArgumentOutOfRangeException(nameof(oldCount));
        if (index < 0 || index > Count - oldCount) throw new ArgumentOutOfRangeException(nameof(index));
        if (newCount < 0 || newCount > int.MaxValue - (Count - oldCount))
            throw new ArgumentOutOfRangeException(nameof(newCount));
        var nextCount = Count - oldCount + newCount;
        if (!double.IsFinite(nextCount * Estimate))
            throw new ArgumentOutOfRangeException(nameof(newCount), "The resulting row extent must be finite.");
        if (oldCount == newCount)
        {
            Invalidate(index, oldCount);
            return;
        }
        if (_heights.Count == 0)
        {
            Count = nextCount;
            return;
        }

        var oldEnd = index + oldCount;
        var delta = newCount - oldCount;
        var heights = new Dictionary<int, double>(_heights.Count);
        foreach (var pair in _heights)
        {
            if (pair.Key >= index && pair.Key < oldEnd) continue;
            heights.Add(pair.Key >= oldEnd ? pair.Key + delta : pair.Key, pair.Value);
        }
        // Sum positive absolute heights independently of the signed Fenwick
        // deviations. No callback, model access or old-store mutation occurs here.
        var extent = (nextCount - heights.Count) * Estimate;
        foreach (var height in heights.Values)
        {
            extent += height;
            if (!double.IsFinite(extent))
                throw new ArgumentOutOfRangeException(nameof(newCount), "The resulting row extent must be finite.");
        }
        var tree = new Dictionary<int, double>();
        foreach (var pair in heights)
        {
            var difference = pair.Value - Estimate;
            for (long node = pair.Key + 1L; node <= nextCount; node += node & -node)
            {
                var key = (int)node;
                var value = tree.GetValueOrDefault(key) + difference;
                if (Math.Abs(value) < 1e-10) tree.Remove(key); else tree[key] = value;
            }
        }
        // Check the same arithmetic order that Start(Count) will expose.
        extent = nextCount * Estimate;
        for (var node = nextCount; node > 0; node -= node & -node)
            if (tree.TryGetValue(node, out var difference)) extent += difference;
        if (!double.IsFinite(extent))
            throw new ArgumentOutOfRangeException(nameof(newCount), "The resulting row extent must be finite.");

        _heights = heights;
        _tree = tree;
        Count = nextCount;
    }
}
