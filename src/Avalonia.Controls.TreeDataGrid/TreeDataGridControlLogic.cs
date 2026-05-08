using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
#if TREE_DATAGRID_UNO
using Uno.Controls.Models.TreeDataGrid;
#else
using Avalonia.Controls.Models.TreeDataGrid;
#endif
#if TREE_DATAGRID_UNO
using Uno.Controls.Primitives;
#else
using Avalonia.Controls.Primitives;
#endif
using Avalonia.Input;

#if TREE_DATAGRID_UNO

namespace Uno.Controls

#else

namespace Avalonia.Controls

#endif
{
    internal static class TreeDataGridControlLogic
    {
        public static bool QueryCancelSelection(object sender, CancelEventHandler? selectionChanging)
        {
            if (selectionChanging is null)
                return false;

            var e = new CancelEventArgs();
            selectionChanging(sender, e);
            return e.Cancel;
        }

        public static bool TryGetRowModel<TModel>(
            ITreeDataGridSource? source,
            TreeDataGridRow? row,
            [NotNullWhen(true)] out TModel? result)
            where TModel : notnull
        {
            if (source is not null &&
                row is not null &&
                row.RowIndex >= 0 &&
                row.RowIndex < source.Rows.Count &&
                source.Rows[row.RowIndex] is IRow<TModel> rowWithModel)
            {
                result = rowWithModel.Model;
                return true;
            }

            result = default;
            return false;
        }

        public static ListSortDirection GetNextSortDirection(ListSortDirection? currentDirection)
        {
            return currentDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }

        public static DragDropEffects GetAllowedRowDragEffects(bool autoDragDropRows, ITreeDataGridSource source)
        {
            return autoDragDropRows && !source.IsSorted
                ? DragDropEffects.Move
                : DragDropEffects.None;
        }

        public static bool TryGetAutoDrop(
            bool autoDragDropRows,
            ITreeDataGridSource? source,
            IEnumerable<IndexPath>? draggedIndexes,
            IndexPath? targetIndex,
            double rowRelativeY,
            out TreeDataGridRowDropPosition position)
        {
            if (!autoDragDropRows ||
                source is null ||
                source.IsSorted ||
                draggedIndexes is null ||
                targetIndex is null)
            {
                position = TreeDataGridRowDropPosition.None;
                return false;
            }

            position = GetDropPosition(source, rowRelativeY);

            foreach (var sourceIndex in draggedIndexes)
            {
                if (sourceIndex.IsAncestorOf(targetIndex.Value) ||
                    (sourceIndex == targetIndex.Value && position == TreeDataGridRowDropPosition.Inside))
                {
                    position = TreeDataGridRowDropPosition.None;
                    return false;
                }
            }

            return true;
        }

        public static TreeDataGridRowDropPosition GetDropPosition(ITreeDataGridSource source, double rowRelativeY)
        {
            if (source.IsHierarchical)
            {
                if (rowRelativeY < 0.33)
                    return TreeDataGridRowDropPosition.Before;
                if (rowRelativeY > 0.66)
                    return TreeDataGridRowDropPosition.After;
                return TreeDataGridRowDropPosition.Inside;
            }

            return rowRelativeY < 0.5
                ? TreeDataGridRowDropPosition.Before
                : TreeDataGridRowDropPosition.After;
        }
    }
}
