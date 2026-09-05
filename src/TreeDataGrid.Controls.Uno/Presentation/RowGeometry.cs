using System;
using System.Collections.Generic;
using System.Linq;

namespace Uno.Controls.Presentation;

/// <summary>
/// Estimated uniform rows plus sparse measured deviations. Unknown rows require no
/// per-row allocation; prefix queries and height changes take O(log row count).
/// No source or model objects are retained by the geometry.
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
        AddDelta(row, delta);
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
        _heights = _heights.ToDictionary(x => x.Key >= index ? x.Key + count : x.Key, x => x.Value);
        Count += count;
        Rebuild();
    }
    public void Remove(int index, int count)
    {
        if (index < 0 || count < 0 || index > Count - count) throw new ArgumentOutOfRangeException(nameof(count));
        _heights = _heights.Where(x => x.Key < index || x.Key >= index + count)
            .ToDictionary(x => x.Key >= index + count ? x.Key - count : x.Key, x => x.Value);
        Count -= count;
        Rebuild();
    }
    public void Move(int oldIndex, int newIndex, int count)
    {
        if (count < 0 || oldIndex < 0 || newIndex < 0 || oldIndex > Count - count || newIndex > Count - count)
            throw new ArgumentOutOfRangeException(nameof(count));
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
        foreach (var row in _heights.Keys.Where(x => x >= index && x < index + count).ToArray()) SetHeight(row, Estimate);
    }
    private void Rebuild()
    {
        _tree.Clear();
        foreach (var pair in _heights) AddDelta(pair.Key, pair.Value - Estimate);
    }
}
