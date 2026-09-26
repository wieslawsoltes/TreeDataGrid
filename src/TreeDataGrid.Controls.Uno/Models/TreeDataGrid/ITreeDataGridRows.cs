using TreeDataGridCore.Models;

namespace Uno.Controls.Models.TreeDataGrid;

/// <summary>Adds view-owned cell realization to the original framework-neutral rows.</summary>
public interface ITreeDataGridRows : IRows
{
    (int index, double y) GetRowAt(double y);
    ICell RealizeCell(IColumn column, int columnIndex, int rowIndex);
    void UnrealizeCell(ICell cell, int columnIndex, int rowIndex);
}
