using System;
using System.Buffers;
using System.Collections.Generic;

namespace Uno.Controls.Presentation;

/// <summary>View-only sizing; source column definitions and row identity are unchanged.</summary>
internal static class ColumnWidths
{
    public static double[] Calculate(IReadOnlyList<CellColumn> columns, double available)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var count = columns.Count;
        var result = count == 0 ? Array.Empty<double>() : new double[count];
        Calculate(columns, available, result.AsSpan());
        return result;
    }

    /// <summary>
    /// Snapshots application-owned inputs once, then fills exact-length caller
    /// storage only after successful calculation. No input or output is retained.
    /// </summary>
    public static void Calculate(IReadOnlyList<CellColumn> columns, double available, Span<double> result)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var length = columns.Count;
        if (result.Length != length) throw new ArgumentException("The result must match the column count.", nameof(result));
        if (!double.IsFinite(available)) available = 0;
        available = Math.Max(0, available);
        WidthState[]? rented = null;
        // Primitive-only storage: no model/control references survive in the pool.
        // Each nested call has its own snapshot. Stack usage is bounded at 4 KiB.
        Span<WidthState> states = length <= 128 ? stackalloc WidthState[length] :
            (rented = ArrayPool<WidthState>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            var count = 0;
            var maxWeight = 0d;
            for (var i = 0; i < length; ++i)
            {
                var column = columns[i];
                var width = column.Model.Width;
                var natural = width.IsAuto ? column.AutoWidth : width.Value;
                var minimum = column.MinimumWidth;
                var maximum = column.MaximumWidth;
                if (!double.IsFinite(minimum) || minimum < 0 || double.IsNaN(maximum) || maximum < 0)
                    throw new ArgumentOutOfRangeException(nameof(columns), "Column width constraints must be nonnegative numbers.");
                var floor = !column.HasWidthMeasurement && column.RequiresUnconstrainedWidthMeasurement ? 1d : 0d;
                // Maximum wins a conflicting minimum. The existing discovery
                // floor applies after constraints, including an Auto maximum of 0.
                minimum = Math.Max(floor, Math.Min(maximum, minimum));
                maximum = Math.Max(floor, maximum);
                var star = width.IsStar && width.Value > 0;
                states[i] = new WidthState
                {
                    Weight = star ? width.Value : 0,
                    Minimum = minimum,
                    Maximum = maximum,
                    Width = star ? 0 : Math.Min(maximum, Math.Max(minimum, width.IsStar ? 0 : natural)),
                };
                if (star)
                {
                    ++count;
                    maxWeight = Math.Max(maxWeight, width.Value);
                }
                else available -= states[i].Width;
            }
            available = Math.Max(0, available);
            // No application getters, indexers or model access after capture.
            // Resolve only constraints on the side indicated by the total error;
            // freezing both sides together can strand distributable free space.
            while (count > 0)
            {
                var weights = 0d;
                foreach (ref readonly var state in states)
                    if (state.Weight > 0) weights += state.Weight / maxWeight;
                var total = 0d;
                foreach (ref var state in states)
                {
                    if (state.Weight <= 0) continue;
                    var proposed = available * (state.Weight / maxWeight) / weights;
                    state.Width = Math.Min(state.Maximum, Math.Max(state.Minimum, proposed));
                    total += state.Width;
                }
                if (Math.Abs(total - available) <= Math.Max(1, available) * 1e-12) break;
                var constrained = 0;
                var frozenWidth = 0d;
                var nextMaxWeight = 0d;
                foreach (ref var state in states)
                {
                    if (state.Weight <= 0) continue;
                    var proposed = available * (state.Weight / maxWeight) / weights;
                    if (total > available ? state.Width > proposed : state.Width < proposed)
                    {
                        state.Weight = 0;
                        --count;
                        ++constrained;
                        frozenWidth += state.Width;
                    }
                    else nextMaxWeight = Math.Max(nextMaxWeight, state.Weight);
                }
                if (constrained == 0) break;
                available = Math.Max(0, available - frozenWidth);
                // Renormalize surviving weights: tiny weights must recover once
                // huge weights saturate, even when their original ratio underflows.
                maxWeight = nextMaxWeight;
            }
            for (var i = 0; i < length; ++i) result[i] = states[i].Width;
        }
        finally { if (rented is not null) ArrayPool<WidthState>.Shared.Return(rented); }
    }

    private struct WidthState
    {
        public double Weight;
        public double Minimum;
        public double Maximum;
        public double Width;
    }
}
