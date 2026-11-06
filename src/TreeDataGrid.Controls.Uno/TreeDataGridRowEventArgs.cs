using Uno.Controls.Primitives;

namespace Uno.Controls;

public class TreeDataGridRowEventArgs(TreeDataGridRow row, int rowIndex)
{
    public TreeDataGridRow Row { get; } = row;
    public int RowIndex { get; } = rowIndex;
}
