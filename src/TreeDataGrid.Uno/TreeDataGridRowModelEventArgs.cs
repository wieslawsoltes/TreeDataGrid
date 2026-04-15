using System;

namespace Avalonia.Controls
{
    public class TreeDataGridRowModelEventArgs : EventArgs
    {
        public TreeDataGridRowModelEventArgs(TreeDataGridRowModel row)
        {
            Row = row;
        }

        public TreeDataGridRowModel Row { get; }
    }
}
