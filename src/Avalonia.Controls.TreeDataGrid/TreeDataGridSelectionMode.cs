using System;

#if TREE_DATAGRID_UNO

namespace Uno.Controls

#else

namespace Avalonia.Controls

#endif
{
    [Flags]
    public enum TreeDataGridSelectionMode
    {
        Row = 0x01,
        Cell = 0x02,
        Multiple = 0x04,
    }
}
