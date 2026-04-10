using System;

namespace Avalonia.Controls
{
    [Flags]
    public enum TreeDataGridSelectionMode
    {
        Row = 0x01,
        Cell = 0x02,
        Multiple = 0x04,
    }
}
