using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Uno.Controls.Primitives;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    private TreeDataGridCell? _editingCell;
    public TreeDataGridCell? EditingCell => _editingCell?.IsEditing == true ? _editingCell : null;
    public bool BeginEdit(int row, int column)
    {
        if (_editingCell?.IsEditing == true && !CommitEdit()) return false;
        if (!SelectCell(row, column) || !BringCellIntoView(row, column)) return false;
        UpdateLayout();
        var cell = _presenter?.RealizedCells.FirstOrDefault(x => x.RowIndex == row && x.ColumnIndex == column);
        if (cell?.BeginEdit() != true) return false;
        _editingCell = cell;
        return true;
    }
    public bool BeginEdit()
    {
        if (_presentation is null) return false;
        var (row, column) = _presentation.Selection.GetAnchor(true);
        return BeginEdit(row, _presentation.Selection.IsCellSelection ? column : _currentColumn);
    }
    public bool CommitEdit()
    {
        if (_editingCell is not { } cell) return true;
        if (!cell.CommitEdit()) return false;
        _editingCell = null;
        return true;
    }
    public void CancelEdit()
    {
        var cell = _editingCell;
        _editingCell = null;
        cell?.CancelEdit();
    }
    protected override void OnDoubleTapped(DoubleTappedRoutedEventArgs e)
    {
        base.OnDoubleTapped(e);
        if (!e.Handled && !IsEditor(e.OriginalSource as DependencyObject) && FindCell(e.OriginalSource as DependencyObject) is { } cell)
            e.Handled = BeginEdit(cell.RowIndex, cell.ColumnIndex);
    }
}
