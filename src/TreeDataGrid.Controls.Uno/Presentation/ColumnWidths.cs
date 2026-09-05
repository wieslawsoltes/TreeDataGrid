using System;
using System.Collections.Generic;

namespace Uno.Controls.Presentation;

/// <summary>View-only sizing; source column definitions and row identity are unchanged.</summary>
internal static class ColumnWidths
{
    public static double[] Calculate(IReadOnlyList<CellColumn> columns, double available)
    {
        if (!double.IsFinite(available)) available = 0;
        available = Math.Max(0, available);
        var result = new double[columns.Count];
        var active = new bool[columns.Count];
        var count = 0;
        for (var i = 0; i < columns.Count; ++i)
        {
            var column = columns[i];
            var width = column.Model.Width;
            if (width.IsStar && width.Value > 0)
            {
                active[i] = true;
                ++count;
            }
            else
            {
                result[i] = Constrain(column, width.IsAuto ? column.AutoWidth : width.IsStar ? 0 : width.Value);
                available -= result[i];
            }
        }
        available = Math.Max(0, available);
        // Resolve only constraints on the side indicated by the total error.
        // Freezing minima and maxima together can incorrectly strand free space.
        while (count > 0)
        {
            var maxWeight = 0d;
            for (var i = 0; i < columns.Count; ++i)
                if (active[i]) maxWeight = Math.Max(maxWeight, columns[i].Model.Width.Value);
            var weights = 0d;
            for (var i = 0; i < columns.Count; ++i)
                if (active[i]) weights += columns[i].Model.Width.Value / maxWeight;
            var total = 0d;
            for (var i = 0; i < columns.Count; ++i)
            {
                if (!active[i]) continue;
                var proposed = weights > 0 ? available * (columns[i].Model.Width.Value / maxWeight) / weights : 0;
                result[i] = Constrain(columns[i], proposed);
                total += result[i];
            }
            if (Math.Abs(total - available) <= Math.Max(1, available) * 1e-12) break;
            var constrained = 0;
            var frozenWidth = 0d;
            for (var i = 0; i < columns.Count; ++i)
            {
                if (!active[i]) continue;
                var proposed = weights > 0 ? available * (columns[i].Model.Width.Value / maxWeight) / weights : 0;
                if (total > available ? result[i] > proposed : result[i] < proposed)
                {
                    active[i] = false;
                    --count;
                    ++constrained;
                    frozenWidth += result[i];
                    // Subtract below, after all proposals have used the same pool.
                }
            }
            if (constrained == 0) break;
            available = Math.Max(0, available - frozenWidth);
        }
        return result;
    }

    private static double Constrain(CellColumn column, double width)
    {
        var minimum = column.MinimumWidth;
        var maximum = column.MaximumWidth;
        if (!double.IsFinite(minimum) || minimum < 0 || double.IsNaN(maximum) || maximum < 0)
            throw new ArgumentOutOfRangeException(nameof(column), "Column width constraints must be nonnegative numbers.");
        // Match Avalonia: maximum wins when minimum exceeds maximum.
        var result = Math.Min(maximum, Math.Max(minimum, width));
        // A yet-unmeasured Auto constraint needs a discovery slot, otherwise a
        // zero-width column can never enter the viewport to be measured.
        return !column.HasWidthMeasurement && column.RequiresUnconstrainedWidthMeasurement ? Math.Max(1, result) : result;
    }
}
