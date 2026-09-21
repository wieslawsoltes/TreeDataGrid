using System;
using Uno.Controls.Primitives;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    public event EventHandler<TreeDataGridCellEventArgs>? CellPrepared;
    public event EventHandler<TreeDataGridCellEventArgs>? CellClearing;
    public event EventHandler<TreeDataGridCellEventArgs>? CellValueChanged;
    internal void RaiseCellPrepared(TreeDataGridCell cell, int columnIndex, int rowIndex) =>
        CellPrepared?.Invoke(this, new(cell, columnIndex, rowIndex));
    internal void RaiseCellClearing(TreeDataGridCell cell, int columnIndex, int rowIndex) =>
        CellClearing?.Invoke(this, new(cell, columnIndex, rowIndex));
    internal void RaiseCellValueChanged(TreeDataGridCell cell, int columnIndex, int rowIndex) =>
        CellValueChanged?.Invoke(this, new(cell, columnIndex, rowIndex));
}
