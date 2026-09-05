using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TreeDataGridCore.Models;
using Uno.Controls.Primitives;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace Uno.Controls;

public enum TreeDataGridNavigation { Up, Down, Left, Right, Home, End, PageUp, PageDown }

public partial class TreeDataGrid
{
    private int _currentColumn;
    private Point? _pressedPoint;
    private uint _pressedPointer;
    public event EventHandler? SelectionChanged;
    public bool SelectCell(int row, int column, bool extend = false, bool toggle = false, bool preserve = false)
    {
        if (_presentation is null) return false;
        _currentColumn = column;
        if (!_presentation.Selection.Select(row, column, extend, toggle, preserve)) return false;
        _presenter?.RefreshSelection();
        return true;
    }
    internal bool IsCurrentCell(int row, int column)
    {
        if (_presentation is null) return false;
        var anchor = _presentation.Selection.GetAnchor(true);
        return row == anchor.Row && column == (_presentation.Selection.IsCellSelection ? anchor.Column : _currentColumn);
    }
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        _presenter?.RefreshSelection();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    public bool BringCellIntoView(int row, int column)
    {
        if (_scroll is null || _presentation is null || (uint)row >= (uint)_presentation.Rows.Count ||
            (uint)column >= (uint)_geometry.Count || _presenter is null) return false;
        var x = _geometry.Start(column);
        var right = x + _geometry.Width(column);
        var y = row * _presenter.RowHeight;
        var bottom = y + _presenter.RowHeight;
        var horizontal = x < _scroll.HorizontalOffset ? x : right > _scroll.HorizontalOffset + _scroll.ViewportWidth
            ? Math.Max(x, right - _scroll.ViewportWidth) : _scroll.HorizontalOffset;
        var vertical = y < _scroll.VerticalOffset ? y : bottom > _scroll.VerticalOffset + _scroll.ViewportHeight
            ? Math.Max(y, bottom - _scroll.ViewportHeight) : _scroll.VerticalOffset;
        _scroll.ChangeView(horizontal, vertical, null, true);
        UpdateViewport();
        return true;
    }
    public bool MoveSelection(TreeDataGridNavigation direction, bool extend = false)
    {
        if (_presentation is null || _presentation.Rows.Count == 0 || _presentation.Columns.Count == 0) return false;
        var selection = _presentation.Selection;
        var (row, column) = selection.GetAnchor(extend);
        if (!selection.IsCellSelection) column = _currentColumn;
        if (row >= 0 && !selection.IsCellSelection && Model?.IsHierarchical == true && !extend)
        {
            if (_presentation.Rows[row] is IExpander expander)
            {
                if (direction == TreeDataGridNavigation.Right && expander.ShowExpander && !expander.IsExpanded)
                { expander.IsExpanded = true; return true; }
                if (direction == TreeDataGridNavigation.Left && expander.IsExpanded)
                { expander.IsExpanded = false; return true; }
                if (direction == TreeDataGridNavigation.Right && expander.IsExpanded && row + 1 < _presentation.Rows.Count)
                { SelectCell(row + 1, column); BringCellIntoView(row + 1, column); return true; }
            }
            if (direction == TreeDataGridNavigation.Left)
            {
                var index = _presentation.Rows.RowIndexToModelIndex(row);
                var parent = index.Count > 1 ? _presentation.Rows.ModelIndexToRowIndex(index[..^1]) : -1;
                if (parent >= 0) { SelectCell(parent, column); BringCellIntoView(parent, column); return true; }
            }
        }
        var page = Math.Max(1, (int)((_scroll?.ViewportHeight ?? 0) / (_presenter?.RowHeight ?? 28)) - 1);
        var nextRow = row < 0 ? 0 : row;
        var nextColumn = column < 0 ? 0 : column;
        if (row >= 0)
        {
            switch (direction)
            {
                case TreeDataGridNavigation.Up: --nextRow; break;
                case TreeDataGridNavigation.Down: ++nextRow; break;
                case TreeDataGridNavigation.Left: --nextColumn; break;
                case TreeDataGridNavigation.Right: ++nextColumn; break;
                case TreeDataGridNavigation.Home: nextRow = 0; break;
                case TreeDataGridNavigation.End: nextRow = _presentation.Rows.Count - 1; break;
                case TreeDataGridNavigation.PageUp: nextRow -= page; break;
                case TreeDataGridNavigation.PageDown: nextRow += page; break;
            }
        }
        nextRow = Math.Clamp(nextRow, 0, _presentation.Rows.Count - 1);
        nextColumn = Math.Clamp(nextColumn, 0, _presentation.Columns.Count - 1);
        if (!SelectCell(nextRow, nextColumn, extend)) return false;
        BringCellIntoView(nextRow, nextColumn);
        return true;
    }
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || IsEditor(e.OriginalSource as DependencyObject)) return;
        var shift = IsKeyDown(VirtualKey.Shift);
        var control = IsKeyDown(VirtualKey.Control) || IsKeyDown(VirtualKey.LeftWindows) || IsKeyDown(VirtualKey.RightWindows);
        if (control && e.Key == VirtualKey.A) { _presentation?.Selection.SelectAll(); e.Handled = true; return; }
        var direction = e.Key switch
        {
            VirtualKey.Up => TreeDataGridNavigation.Up, VirtualKey.Down => TreeDataGridNavigation.Down,
            VirtualKey.Left => TreeDataGridNavigation.Left, VirtualKey.Right => TreeDataGridNavigation.Right,
            VirtualKey.Home => TreeDataGridNavigation.Home, VirtualKey.End => TreeDataGridNavigation.End,
            VirtualKey.PageUp => TreeDataGridNavigation.PageUp, VirtualKey.PageDown => TreeDataGridNavigation.PageDown,
            _ => (TreeDataGridNavigation?)null,
        };
        if (direction is { } navigation && (!control || shift || e.Key is VirtualKey.Home or VirtualKey.End))
            e.Handled = MoveSelection(navigation, shift);
    }
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressedPoint = null;
        if (e.Handled || _presentation is null || FindCell(e.OriginalSource as DependencyObject) is not { } cell) return;
        var point = e.GetCurrentPoint(this);
        var right = point.Properties.IsRightButtonPressed;
        if ((e.Pointer.PointerDeviceType == PointerDeviceType.Mouse || right) &&
            !_presentation.Selection.IsSelected(cell.RowIndex, cell.ColumnIndex)) PointerSelect(cell, e, right);
        else { _pressedPoint = point.Position; _pressedPointer = e.Pointer.PointerId; }
    }
    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pressed = _pressedPoint;
        _pressedPoint = null;
        if (e.Handled || pressed is not { } start || e.Pointer.PointerId != _pressedPointer ||
            FindCell(e.OriginalSource as DependencyObject) is not { } cell) return;
        var point = e.GetCurrentPoint(this);
        if (Math.Abs(point.Position.X - start.X) <= 3 && Math.Abs(point.Position.Y - start.Y) <= 3)
            PointerSelect(cell, e, point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased);
    }
    protected override void OnPointerCanceled(PointerRoutedEventArgs e) { _pressedPoint = null; base.OnPointerCanceled(e); }
    protected override void OnPointerCaptureLost(PointerRoutedEventArgs e) { _pressedPoint = null; base.OnPointerCaptureLost(e); }
    private void PointerSelect(TreeDataGridCell cell, PointerRoutedEventArgs e, bool right)
    {
        var shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
        var toggle = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control) || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Windows);
        if (SelectCell(cell.RowIndex, cell.ColumnIndex, !right && shift, !right && toggle, right))
        { Focus(FocusState.Pointer); e.Handled = true; }
    }
    private TreeDataGridCell? FindCell(DependencyObject? source)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, this); current = VisualTreeHelper.GetParent(current))
            if (current is TreeDataGridCell cell) return cell.RowIndex >= 0 ? cell : null;
        return null;
    }
    private bool IsEditor(DependencyObject? source)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, this); current = VisualTreeHelper.GetParent(current))
            if (current is TextBox or PasswordBox or ComboBox) return true;
        return false;
    }
    private static bool IsKeyDown(VirtualKey key) =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;
}
