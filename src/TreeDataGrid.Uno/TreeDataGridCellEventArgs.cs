using System;
using Avalonia.Controls.Primitives;

namespace Avalonia.Controls
{
    public sealed class TreeDataGridCellEventArgs : EventArgs
    {
        public TreeDataGridCellEventArgs(TreeDataGridCell cell, int columnIndex, int rowIndex)
        {
            Cell = cell;
            ColumnIndex = columnIndex;
            RowIndex = rowIndex;
        }

        public TreeDataGridCell Cell { get; }
        public int ColumnIndex { get; }
        public int RowIndex { get; }
    }
}
