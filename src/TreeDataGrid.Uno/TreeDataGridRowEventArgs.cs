using System;
using Uno.Controls.Primitives;

namespace Uno.Controls
{
    public sealed class TreeDataGridRowEventArgs : EventArgs
    {
        public TreeDataGridRowEventArgs(TreeDataGridRow row, int rowIndex)
        {
            Row = row;
            RowIndex = rowIndex;
        }

        public TreeDataGridRow Row { get; }
        public int RowIndex { get; }
    }
}
