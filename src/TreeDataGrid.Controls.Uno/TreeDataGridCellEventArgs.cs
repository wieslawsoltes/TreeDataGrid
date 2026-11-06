using Microsoft.UI.Xaml.Controls;

namespace Uno.Controls;

public class TreeDataGridCellEventArgs(Control cell, int columnIndex, int rowIndex)
{
    public Control Cell { get; } = cell;
    public int ColumnIndex { get; } = columnIndex;
    public int RowIndex { get; } = rowIndex;
}
