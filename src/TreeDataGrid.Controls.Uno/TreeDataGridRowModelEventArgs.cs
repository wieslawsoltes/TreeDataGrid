using System;

namespace Uno.Controls;

/// <summary>Provides a captured row model to event subscribers.</summary>
public class TreeDataGridRowModelEventArgs : EventArgs
{
    public TreeDataGridRowModelEventArgs(TreeDataGridRowModel row) => Row = row;

    public TreeDataGridRowModel Row { get; }
}
