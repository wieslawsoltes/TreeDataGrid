using System;

#if TREE_DATAGRID_UNO

namespace Uno.Controls.Selection

#else

namespace Avalonia.Controls.Selection

#endif
{
    public class TreeSelectionModelSourceResetEventArgs : EventArgs
    {
        public TreeSelectionModelSourceResetEventArgs(IndexPath parentIndex)
        {
            ParentIndex = parentIndex;
        }

        public IndexPath ParentIndex { get; }
    }
}
