using System;

namespace Uno.Controls.Models.TreeDataGrid;

[Flags]
public enum BeginEditGestures
{
    None = 0,
    F2 = 1,
    Tap = 2,
    DoubleTap = 4,
    WhenSelected = 0x1000,
    Default = F2 | DoubleTap,
}
