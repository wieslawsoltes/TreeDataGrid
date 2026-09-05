using System;

namespace TreeDataGridCore.Selection
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
