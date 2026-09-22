using System;
using System.Collections.Generic;
using System.Linq;

namespace Uno.Controls.Presentation;

/// <summary>
/// Estimated uniform rows plus sparse measured deviations. Uniform queries are
/// O(1); measured prefix queries and height changes are O(log row count).
/// Unknown rows require no per-row storage and no source/model is retained.
/// </summary>
internal sealed class RowGeometry
{
    private Dictionary<int, double> _heights = new();
    private readonly Dictionary<int, double> _tree = new();
    public int Count { get; private set; }
    public double Estimate { get; private set; } = 28;
    public int MeasuredCount => _heights.Count;
    public double TotalHeight => Start(Count);

    public void Reset(int count, double estimate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (!double.IsFinite(estimate) || estimate <= 0 || !double.IsFinite(count * estimate))
            throw new ArgumentOutOfRangeException(nameof(estimate));
        Count = count;
        Estimate = estimate;
        _heights.Clear();
        _tree.Clear();
    }
    public double Start(int row)
    {
        if ((uint)row > (uint)Count) throw new ArgumentOutOfRangeException(nameof(row));
        var result = row * Estimate;
        if (_heights.Count == 0) return result;
        for (var index = row; index > 0; index -= index & -index)
            if (_tree.TryGetValue(index, out var delta)) result += delta;
        return result;
    }
    public double Height(int row)
    {
        if ((uint)row >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(row));
        return _heights.GetValueOrDefault(row, Estimate);
    }
    public bool SetHeight(int row, double height)
    {
        if (!double.IsFinite(height) || height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        var previous = Height(row);
        if (Math.Abs(previous - height) < 1e-7) return false;
        var delta = height - previous;
        if (!double.IsFinite(TotalHeight + delta)) throw new ArgumentOutOfRangeException(nameof(height));
        if (height == Estimate) _heights.Remove(row); else _heights[row] = height;
        // Once all deviations disappear, clear accumulated floating-point
        // residuals as well. Future uniform queries use the exact base geometry.
        if (_heights.Count == 0) _tree.Clear(); else AddDelta(row, delta);
        return true;
    }
    private void AddDelta(int row, double delta)
    {
        for (long index = row + 1L; index <= Count; index += index & -index)
        {
            var key = (int)index;
            var next = _tree.GetValueOrDefault(key) + delta;
            if (Math.Abs(next) < 1e-10) _tree.Remove(key); else _tree[key] = next;
        }
    }
    /// <summary>Clamps negative offsets to the first row; Count denotes the end.</summary>
    public int RowAt(double offset)
    {
        if (double.IsNaN(offset)) throw new ArgumentOutOfRangeException(nameof(offset));
        if (offset <= 0 || Count == 0) return 0;
        if (_heights.Count == 0)
        {
            if (offset >= Count * Estimate) return Count;
            var result = (int)Math.Min(Count - 1d, Math.Floor(offset / Estimate));
            // Division can round across an integer at a boundary. Compare with
            // the same multiplication used by Start so exact starts, and their
            // adjacent representable offsets, map consistently in both directions.
            if (result > 0 && result * Estimate > offset) --result;
            else if (result < Count - 1 && (result + 1d) * Estimate <= offset) ++result;
            return result;
        }
        var row = 0;
        var prefix = 0d;
        var bit = 1;
        while (bit <= Count / 2) bit <<= 1;
        for (; bit > 0; bit >>= 1)
        {
            var next = (long)row + bit;
            if (next > Count) continue;
            var candidate = prefix + bit * Estimate + _tree.GetValueOrDefault((int)next);
            if (candidate <= offset) { row = (int)next; prefix = candidate; }
        }
        return row;
    }
    public void Insert(int index, int count)
    {
        if ((uint)index > (uint)Count || count < 0 || count > int.MaxValue - Count) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) return;
        var nextCount = Count + count;
        // Preserve Reset's finite-base invariant and reject an overflowing
        // extent before changing either indexes or retained measurements.
        if (!double.IsFinite(nextCount * Estimate) || !double.IsFinite(TotalHeight + count * Estimate))
            throw new ArgumentOutOfRangeException(nameof(count), "The resulting row extent must be finite.");
        if (_heights.Count == 0) { Count = nextCount; return; }
        _heights = _heights.ToDictionary(x => x.Key >= index ? x.Key + count : x.Key, x => x.Value);
        Count = nextCount;
        Rebuild();
    }
    public void Remove(int index, int count)
    {
        if (index < 0 || count < 0 || index > Count - count) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) return;
        if (_heights.Count == 0) { Count -= count; return; }
        _heights = _heights.Where(x => x.Key < index || x.Key >= index + count)
            .ToDictionary(x => x.Key >= index + count ? x.Key - count : x.Key, x => x.Value);
        Count -= count;
        Rebuild();
    }
    public void Move(int oldIndex, int newIndex, int count)
    {
        if (count < 0 || oldIndex < 0 || newIndex < 0 || oldIndex > Count - count || newIndex > Count - count)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0 || oldIndex == newIndex || _heights.Count == 0) return;
        _heights = _heights.ToDictionary(x => MapMove(x.Key, oldIndex, newIndex, count), x => x.Value);
        Rebuild();
    }
    public static int MapMove(int index, int oldIndex, int newIndex, int count)
    {
        if (index >= oldIndex && index < oldIndex + count) return newIndex + index - oldIndex;
        var removed = index >= oldIndex + count ? index - count : index;
        return removed >= newIndex ? removed + count : removed;
    }
    public void Invalidate(int index, int count)
    {
        if (index < 0 || count < 0 || index > Count - count) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0 || _heights.Count == 0) return;
        if (index == 0 && count == Count)
        {
            _heights.Clear();
            _tree.Clear();
            return;
        }
        if (count <= _heights.Count)
        {
            // A cell notification usually invalidates one row. Do not scan
            // every previously measured row, allocate a closure or snapshot keys.
            var end = index + count;
            for (var row = index; row < end; ++row)
                if (_heights.ContainsKey(row)) SetHeight(row, Estimate);
        }
        else
        {
            // .NET permits Dictionary.Remove during enumeration. SetHeight only
            // removes from this dictionary when restoring the uniform estimate;
            // the Fenwick mutations are in a separate dictionary.
            foreach (var pair in _heights)
                if (pair.Key >= index && pair.Key - index < count) SetHeight(pair.Key, Estimate);
        }
    }
    private void Rebuild()
    {
        _tree.Clear();
        foreach (var pair in _heights) AddDelta(pair.Key, pair.Value - Estimate);
    }
}
