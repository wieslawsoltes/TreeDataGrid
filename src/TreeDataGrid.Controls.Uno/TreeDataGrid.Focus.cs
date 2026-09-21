using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Uno.Controls.Primitives;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    internal DependencyObject? FocusedElement => XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null;

    // Retention must include focus inside nested controls/grids. Unlike input
    // routing, walking through a nested grid here does not transfer its actions.
    internal static bool ContainsFocus(DependencyObject container, DependencyObject? focused)
    {
        for (var current = focused; current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(container, current)) return true;
        return false;
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        _presenter?.ReleaseFocusRetention();
        _headers?.ReleaseFocusRetention();
    }

    private bool SelectAndFocusCell(int row, int column, bool extend = false)
    {
        if (!SelectCell(row, column, extend) || !BringCellIntoView(row, column)) return false;
        var presentation = _presentation;
        var structure = _textSearchStructureRevision;
        if (TryGetCell(column, row) is { } target && target.Focus(FocusState.Keyboard)) return true;
        // Match the reference row-navigation fallback for custom non-focusable
        // cells. Cell-selection navigation does not substitute a different cell.
        if (ReferenceEquals(_presentation, presentation) && structure == _textSearchStructureRevision &&
            presentation?.Selection.IsCellSelection == false && TryGetRow(row)?.CellsPresenter is { } cells)
            foreach (var candidate in cells.GetRealizedElements())
                if (candidate.Focus(FocusState.Keyboard) || !ReferenceEquals(_presentation, presentation) || structure != _textSearchStructureRevision) break;
        return true;
    }

    internal void ReturnFocusAfterEditing(TreeDataGridCell cell, int realization)
    {
        // A commit handler may have started another editor or replaced the row.
        // The old key handler must not take focus back from that new operation.
        if (EditingCell is not null || cell.RealizationVersion != realization || cell.RowIndex < 0 || cell.ColumnIndex < 0) return;
        var target = cell.OwningCell ?? cell;
        if (!ReferenceEquals(TryGetCell(target.ColumnIndex, target.RowIndex), target)) return;
        if (!target.Focus(FocusState.Keyboard)) Focus(FocusState.Keyboard);
    }
}
