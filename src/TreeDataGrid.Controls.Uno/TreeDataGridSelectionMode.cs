using System;

namespace Uno.Controls;

/// <summary>Uses the same flag values as Avalonia's TreeDataGrid selection mode.</summary>
[Flags]
public enum TreeDataGridSelectionMode
{
    Row = 0x01,
    Cell = 0x02,
    Multiple = 0x04,
    /// <summary>Leave the supplied Core source's selection configuration unchanged.</summary>
    Source = 0,
    /// <summary>Explicitly disable selection.</summary>
    None = -1,
    // Aliases used by the initial, unpublished Uno checkpoint.
    SingleRow = Row,
    MultipleRows = Row | Multiple,
    SingleCell = Cell,
    MultipleCells = Cell | Multiple,
}
