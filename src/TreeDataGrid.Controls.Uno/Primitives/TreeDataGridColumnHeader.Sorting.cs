using System;
using System.ComponentModel;
using Uno.Controls.Presentation;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridColumnHeader
{
    private int _sortRequestVersion;

    private void CycleSort()
    {
        if (_realizing || _unrealizing || _pendingUnrealize || _resizing || !IsEnabled ||
            _owner is not { CanUserSortColumns: true } owner || Column is not { } column)
            return;

        var realization = _realizationVersion;
        var request = unchecked(++_sortRequestVersion);
        var direction = SortDirection;
        var presentation = owner.Presentation;
        if (presentation is null) return;
        var columns = presentation.Columns;
        if (!Current() || !ReferenceEquals(columns, _columns)) return;

        var canSort = column.CanUserSort != false;
        if (!Current() || !canSort) return;
        // Only the descending transition consumes this extension callback. A
        // custom getter may retire this header, change permission or recursively
        // activate it. A nested activation owns the newer request even when the
        // same model/container survives or the direction returns to its old value.
        var triState = direction == ListSortDirection.Descending && column.AllowTriStateSorting;
        if (!Current()) return;
        var next = ColumnSortCycle.Next(direction, triState);

        // Do not clear a shared source because an obsolete/hidden/foreign header
        // is still in a retained visual pool. Count/indexers can be custom code.
        var index = ColumnIndex;
        var count = columns.Count;
        if (!Current() || (uint)index >= (uint)count) return;
        var currentColumn = columns[index];
        if (!Current() || !ReferenceEquals(currentColumn, column)) return;

        canSort = column.CanUserSort != false;
        if (!Current() || !canSort) return;

        if (next is { } sort) presentation.SortBy(column, sort);
        else presentation.ClearSort();
        // No state is published after calling the source: sorting notifications
        // may replace the source or start a newer sort. Core owns the glyph state.

        bool Current() => request == _sortRequestVersion && realization == _realizationVersion &&
            !_realizing && !_unrealizing && !_pendingUnrealize && !_resizing && IsEnabled &&
            owner.CanUserSortColumns && ReferenceEquals(_owner, owner) &&
            ReferenceEquals(owner.Presentation, presentation) && ReferenceEquals(Column, column) &&
            ReferenceEquals(_columns, columns) && SortDirection == direction;
    }
}

internal static class ColumnSortCycle
{
    internal static ListSortDirection? Next(ListSortDirection? current, bool triState) => current switch
    {
        ListSortDirection.Ascending => ListSortDirection.Descending,
        ListSortDirection.Descending when triState => null,
        _ => ListSortDirection.Ascending,
    };
}
