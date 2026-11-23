using System;
using Microsoft.UI.Xaml;
using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Presentation;

/// <summary>A stack-only caller token; constrained calls never box its implementation.</summary>
internal interface IColumnResizeGuard
{
    bool IsCurrent { get; }
}

/// <summary>Computes and commits a resize only while all callback-owned inputs still belong to it.</summary>
internal static class ColumnResizeTransaction
{
    internal static bool Resize<TGuard>(IColumns columns, IColumn column, int index,
        double actualWidth, double delta, TGuard guard) where TGuard : struct, IColumnResizeGuard
    {
        if (!double.IsFinite(delta) || !guard.IsCurrent) return false;
        // A custom column can replace the source or recursively resize from any
        // getter. Read each property once and revalidate before the next callback.
        var width = column.Width;
        if (!guard.IsCurrent) return false;
        var next = (width.IsAbsolute ? width.Value : actualWidth) + delta;
        if (!double.IsFinite(next)) return false;
        var minimum = 0d;
        var maximum = double.PositiveInfinity;
        if (column is IUpdateColumnLayout layout)
        {
            minimum = Math.Max(0, layout.MinActualWidth);
            if (!guard.IsCurrent) return false;
            maximum = layout.MaxActualWidth;
            if (!guard.IsCurrent) return false;
        }
        // Preserve the established solver precedence: maximum wins when Min > Max.
        return SetWidth(columns, column, index, new GridLength(Math.Min(maximum, Math.Max(minimum, next))), guard);
    }

    internal static bool SetWidth<TGuard>(IColumns columns, IColumn column, int index,
        GridLength width, TGuard guard) where TGuard : struct, IColumnResizeGuard
    {
        if (!guard.IsCurrent) return false;
        var count = columns.Count;
        if (!guard.IsCurrent || (uint)index >= (uint)count) return false;
        var current = columns[index];
        if (!guard.IsCurrent || !ReferenceEquals(current, column)) return false;
        columns.SetColumnWidth(index, width);
        return true;
    }
}
