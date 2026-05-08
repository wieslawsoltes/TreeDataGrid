using System;

#if TREE_DATAGRID_UNO

namespace Uno.Controls

#else

namespace Avalonia.Controls

#endif
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
