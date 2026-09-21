using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Uno.Controls.Primitives;
using Uno.Controls.Presentation;
using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    private TreeDataGridCell? _editingCell;
    private TreeDataGridCell? _beginningCell;
    private TreeDataGridCell? _committingCell;
    private int _editOperation;
    public TreeDataGridCell? EditingCell => _editingCell?.IsEditing == true ? _editingCell : null;
    public bool BeginEdit(int row, int column)
    {
        // IEditableObject.BeginEdit is application code. Reject recursive begin
        // requests while its first transaction is still being constructed.
        if (_beginningCell is not null || _presentation is not { } presentation) return false;
        var operation = ++_editOperation;
        var structure = _textSearchStructureRevision;
        if (_editingCell?.IsEditing == true && !CommitEditCore()) return false;
        if (!Current() || !SelectCell(row, column) || !Current() || !BringCellIntoView(row, column) || !Current()) return false;
        UpdateLayout();
        if (!Current() || TryGetCell(column, row) is not TreeDataGridCell outer) return false;
        var cell = outer.GetEditingTarget();
        var realization = cell.RealizationVersion;
        _beginningCell = cell;
        try
        {
            if (!cell.BeginEdit()) return false;
            if (Current() && realization == cell.RealizationVersion && cell.IsEditing)
            {
                _editingCell = cell;
                return true;
            }
            // Cancellation/source changes during BeginEdit can precede assigning
            // _editingCell. Release only the transaction this call just created.
            if (realization == cell.RealizationVersion && cell.IsEditing && !ReferenceEquals(_editingCell, cell))
                cell.CancelEdit();
            return false;
        }
        catch (Exception error)
        {
            if (realization == cell.RealizationVersion && cell.IsEditing && !ReferenceEquals(_editingCell, cell))
            {
                try { cell.CancelEdit(); }
                catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            }
            throw;
        }
        finally { _beginningCell = null; }
        bool Current() => operation == _editOperation && structure == _textSearchStructureRevision && ReferenceEquals(_presentation, presentation);
    }
    public bool BeginEdit()
    {
        if (_presentation is null) return false;
        var (row, column) = _presentation.Selection.GetAnchor(true);
        return BeginEdit(row, _presentation.Selection.IsCellSelection ? column : _currentColumn);
    }
    public bool CommitEdit()
    {
        ++_editOperation;
        return CommitEditCore();
    }
    private bool CommitEditCore()
    {
        if (_editingCell is not { } cell) return true;
        if (!cell.IsEditing) { _editingCell = null; return true; }
        if (ReferenceEquals(_committingCell, cell)) return false;
        var previous = _committingCell;
        var operation = _editOperation;
        var realization = cell.RealizationVersion;
        _committingCell = cell;
        try
        {
            if (!cell.CommitEdit()) return false;
            // CellValueChanged may start another edit, even on the same control.
            if ((operation == _editOperation || !cell.IsEditing) && realization == cell.RealizationVersion && ReferenceEquals(_editingCell, cell))
                _editingCell = null;
            return true;
        }
        finally { _committingCell = previous; }
    }
    public void CancelEdit()
    {
        ++_editOperation;
        var cell = _editingCell;
        _editingCell = null;
        cell?.CancelEdit();
    }
    private bool AllowsEditGesture(int row, int column, BeginEditGestures gesture)
    {
        if (_presentation is null || (uint)column >= (uint)_presentation.NativeColumns.Count) return false;
        var gestures = (TryGetCell(column, row) as TreeDataGridCell)?.Model?.EditGestures ?? _presentation.NativeColumns[column].EditGestures;
        return (gestures & gesture) != 0 && ((gestures & BeginEditGestures.WhenSelected) == 0 ||
            _presentation.Selection.IsSelected(row, column));
    }
    private bool BeginKeyboardEdit()
    {
        if (_presentation is null) return false;
        var (row, column) = _presentation.Selection.GetAnchor(true);
        if (!_presentation.Selection.IsCellSelection) column = _currentColumn;
        if (FindCell(FocusedElement) is { } focused) { row = focused.RowIndex; column = focused.ColumnIndex; }
        return AllowsEditGesture(row, column, BeginEditGestures.F2) && BeginEdit(row, column);
    }
    protected override void OnTapped(TappedRoutedEventArgs e)
    {
        base.OnTapped(e);
        var allowed = _tapAllowed;
        _tapAllowed = false;
        if (!e.Handled && allowed && !IsEditor(e.OriginalSource as DependencyObject) &&
            FindCell(e.OriginalSource as DependencyObject) is { } cell &&
            cell.RowIndex == _tapRow && cell.ColumnIndex == _tapColumn)
            e.Handled = BeginEdit(cell.RowIndex, cell.ColumnIndex);
    }
    protected override void OnDoubleTapped(DoubleTappedRoutedEventArgs e)
    {
        base.OnDoubleTapped(e);
        if (!e.Handled && !IsEditor(e.OriginalSource as DependencyObject) && FindCell(e.OriginalSource as DependencyObject) is { } cell)
            e.Handled = AllowsEditGesture(cell.RowIndex, cell.ColumnIndex, BeginEditGestures.DoubleTap) && BeginEdit(cell.RowIndex, cell.ColumnIndex);
    }
}
