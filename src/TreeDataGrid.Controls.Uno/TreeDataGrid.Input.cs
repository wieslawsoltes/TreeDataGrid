using System;
using System.ComponentModel;
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
    private int _selectionOperation;
    private Point? _pressedPoint;
    private uint _pressedPointer;
    private int _tapRow = -1;
    private int _tapColumn = -1;
    private bool _tapAllowed;
    private EventHandler<TreeDataGridSelectionChangedEventArgs>? _selectionChanged;
    public event EventHandler<TreeDataGridSelectionChangedEventArgs>? SelectionChanged
    {
        add
        {
            var subscribe = _selectionChanged is null && value is not null;
            _selectionChanged += value;
            if (subscribe && _presentation is not null)
                _presentation.Selection.SelectionChanged += OnDetailedSelectionChanged;
        }
        remove
        {
            _selectionChanged -= value;
            if (_selectionChanged is null && _presentation is not null)
                _presentation.Selection.SelectionChanged -= OnDetailedSelectionChanged;
        }
    }
    public event CancelEventHandler? SelectionChanging;
    public bool QueryCancelSelection()
    {
        if (SelectionChanging is not { } changing) return false;
        var args = new CancelEventArgs();
        changing(this, args);
        return args.Cancel;
    }
    public bool SelectCell(int row, int column, bool extend = false, bool toggle = false, bool preserve = false)
    {
        if (_presentation is not { } presentation) return false;
        var operation = ++_selectionOperation;
        var structure = _textSearchStructureRevision;
        if (QueryCancelSelection() || !Current()) return false;
        if (EditingCell is { } editing && (editing.RowIndex != row || editing.ColumnIndex != column) && !CommitEdit()) return false;
        if (!Current()) return false;
        var previousColumn = _currentColumn;
        // Core notifications run synchronously. Observers must see the intended
        // current cell, and a nested SelectCell must not be overwritten later.
        _currentColumn = column;
        if (!presentation.Selection.Select(row, column, extend, toggle, preserve))
        {
            if (Current()) _currentColumn = previousColumn;
            return false;
        }
        if (!Current()) return false;
        _presenter?.RefreshSelection();
        return true;
        bool Current() => operation == _selectionOperation && structure == _textSearchStructureRevision && ReferenceEquals(_presentation, presentation);
    }
    internal bool IsCurrentCell(int row, int column)
    {
        if (_presentation is null) return false;
        var anchor = _presentation.Selection.GetAnchor(true);
        return row == anchor.Row && column == (_presentation.Selection.IsCellSelection ? anchor.Column : _currentColumn);
    }
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (!_loaded || !ReferenceEquals(sender, _selectionInteraction)) return;
        _presenter?.RefreshSelection();
        NotifyAutomationSelectionChanged();
    }
    private void OnDetailedSelectionChanged(object? sender, TreeDataGridSelectionChangedEventArgs e)
    {
        if (!_loaded || !ReferenceEquals(sender, _presentation?.Selection)) return;
        _presenter?.RefreshSelection();
        _selectionChanged?.Invoke(this, e);
    }
    public bool BringCellIntoView(int row, int column)
    {
        if (_scroll is not { } scroll || _presentation is not { } presentation || (uint)row >= (uint)presentation.Rows.Count ||
            (uint)column >= (uint)_geometry.Count || _presenter is not { } presenter) return false;
        var structure = _textSearchStructureRevision;
        // Measuring a distant variable-height viewport changes both its row
        // positions and ScrollViewer's extent. Converge using committed geometry
        // instead of issuing a second request against a still-stale extent.
        // The bound protects against application callbacks which oscillate layout.
        for (var pass = 0; pass < 8; ++pass)
        {
            var x = _geometry.Start(column);
            var right = x + _geometry.Width(column);
            var y = presenter.GetRowStart(row);
            var height = presenter.GetRowHeight(row);
            var bottom = y + height;
            var horizontal = x < scroll.HorizontalOffset ? x : right > scroll.HorizontalOffset + scroll.ViewportWidth
                ? Math.Max(x, right - scroll.ViewportWidth) : scroll.HorizontalOffset;
            var vertical = y < scroll.VerticalOffset ? y : bottom > scroll.VerticalOffset + scroll.ViewportHeight
                ? Math.Max(y, bottom - scroll.ViewportHeight) : scroll.VerticalOffset;
            _pendingVerticalAnchor = null;
            presenter.CancelPendingAnchor();
            scroll.ChangeView(horizontal, vertical, null, true);
            if (!Current()) return false;
            UpdateViewport();
            if (!Current()) return false;
            UpdateLayout();
            if (!Current()) return false;
            y = presenter.GetRowStart(row);
            height = presenter.GetRowHeight(row);
            var current = _pendingVerticalAnchor ?? scroll.VerticalOffset;
            var visible = height > scroll.ViewportHeight
                ? Math.Abs(y - current) < 0.5
                : y >= current - 0.5 && y + height <= current + scroll.ViewportHeight + 0.5;
            if (visible && presenter.TryGetElement(row) is not null) return true;
        }
        return Current();
        bool Current() => structure == _textSearchStructureRevision && ReferenceEquals(_presentation, presentation) &&
            ReferenceEquals(_presenter, presenter) && ReferenceEquals(_scroll, scroll) &&
            (uint)row < (uint)presentation.Rows.Count && (uint)column < (uint)_geometry.Count;
    }
    public bool MoveSelection(TreeDataGridNavigation direction, bool extend = false)
    {
        if (_presentation is null || _presentation.Rows.Count == 0 || _presentation.Columns.Count == 0) return false;
        var selection = _presentation.Selection;
        var (row, column) = selection.GetAnchor(extend);
        if (!selection.IsCellSelection) column = FindCell(FocusedElement)?.ColumnIndex ?? _currentColumn;
        if ((uint)row < (uint)_presentation.Rows.Count && !selection.IsCellSelection && ActiveSource?.IsHierarchical == true)
        {
            if (_presentation.Rows[row] is IExpander expander)
            {
                if (direction == TreeDataGridNavigation.Right && expander.ShowExpander && !expander.IsExpanded)
                { expander.IsExpanded = true; return true; }
                if (direction == TreeDataGridNavigation.Left && expander.IsExpanded)
                { expander.IsExpanded = false; return true; }
                if (direction == TreeDataGridNavigation.Right && expander.IsExpanded && row + 1 < _presentation.Rows.Count)
                { return SelectAndFocusCell(row + 1, column); }
            }
            if (direction == TreeDataGridNavigation.Left)
            {
                var index = _presentation.Rows.RowIndexToModelIndex(row);
                var parent = index.Count > 1 ? _presentation.Rows.ModelIndexToRowIndex(index[..^1]) : -1;
                if (parent >= 0) return SelectAndFocusCell(parent, column);
            }
        }
        // Row-selection Left/Right are hierarchy actions, not horizontal cell
        // navigation. Keep the current column for subsequent vertical movement.
        if (!selection.IsCellSelection && direction is TreeDataGridNavigation.Left or TreeDataGridNavigation.Right) return false;
        var nextRow = row < 0 ? 0 : row;
        var nextColumn = column < 0 ? 0 : column;
        if (direction == TreeDataGridNavigation.Home) nextRow = 0;
        else if (direction == TreeDataGridNavigation.End) nextRow = _presentation.Rows.Count - 1;
        else if (row >= 0)
        {
            switch (direction)
            {
                case TreeDataGridNavigation.Up: --nextRow; break;
                case TreeDataGridNavigation.Down: ++nextRow; break;
                case TreeDataGridNavigation.Left: --nextColumn; break;
                case TreeDataGridNavigation.Right: ++nextColumn; break;
                case TreeDataGridNavigation.Home: nextRow = 0; break;
                case TreeDataGridNavigation.End: nextRow = _presentation.Rows.Count - 1; break;
                case TreeDataGridNavigation.PageUp:
                    nextRow = Math.Min(row - 1, (_presenter?.GetRowAt(Math.Max(0, _presenter.GetRowStart(row) - (_scroll?.ViewportHeight ?? 0))) ?? row) + 1);
                    break;
                case TreeDataGridNavigation.PageDown:
                    nextRow = Math.Max(row + 1, (_presenter?.GetRowAt(_presenter.GetRowStart(row) + (_scroll?.ViewportHeight ?? 0)) ?? row) - 1);
                    break;
            }
        }
        nextRow = Math.Clamp(nextRow, 0, _presentation.Rows.Count - 1);
        nextColumn = Math.Clamp(nextColumn, 0, _presentation.Columns.Count - 1);
        return SelectAndFocusCell(nextRow, nextColumn, extend);
    }
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsOwnInput(e.OriginalSource as DependencyObject)) return;
        var revision = _presentationRevision;
        if (!e.Handled && e.Key == VirtualKey.Escape && EditingCell is { } cancelling)
        {
            var realization = cancelling.RealizationVersion;
            CancelEdit(); ReturnFocusAfterEditing(cancelling, realization); e.Handled = true;
        }
        if (!e.Handled && e.Key == VirtualKey.Enter && EditingCell is { } committing)
        {
            var realization = committing.RealizationVersion;
            if (CommitEdit()) ReturnFocusAfterEditing(committing, realization);
            e.Handled = true;
        }
        // Editing is a cell gesture even when row/cell selection is disabled.
        if (!e.Handled && !IsEditor(e.OriginalSource as DependencyObject) && e.Key == VirtualKey.F2)
            e.Handled = BeginKeyboardEdit();
        if (revision == _presentationRevision) _selectionInteraction?.OnKeyDown(this, e);
    }
    internal void ProcessSelectionKeyDown(KeyRoutedEventArgs e)
    {
        if (e.Handled || IsEditor(e.OriginalSource as DependencyObject) || !IsOwnInput(e.OriginalSource as DependencyObject)) return;
        var shift = IsKeyDown(VirtualKey.Shift);
        var control = IsKeyDown(VirtualKey.Control) || IsKeyDown(VirtualKey.LeftWindows) || IsKeyDown(VirtualKey.RightWindows);
        if (control && e.Key == VirtualKey.A)
        {
            if (!QueryCancelSelection() && CommitEdit()) _presentation?.Selection.SelectAll();
            e.Handled = true;
            return;
        }
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
        if (!IsOwnInput(e.OriginalSource as DependencyObject)) return;
        var revision = _presentationRevision;
        _pressedPoint = null;
        _tapAllowed = false;
        _rowDragCandidate = null;
        if (!e.Handled && _presentation is not null && FindCell(e.OriginalSource as DependencyObject) is { } cell)
        {
            var point = e.GetCurrentPoint(this);
            _tapRow = cell.RowIndex;
            _tapColumn = cell.ColumnIndex;
            _tapAllowed = !point.Properties.IsRightButtonPressed && AllowsEditGesture(cell.RowIndex, cell.ColumnIndex, global::Uno.Controls.Models.TreeDataGrid.BeginEditGestures.Tap);
            ArmRowDrag(cell, e);
        }
        if (revision == _presentationRevision) _selectionInteraction?.OnPointerPressed(this, e);
    }
    internal void ProcessSelectionPointerPressed(PointerRoutedEventArgs e)
    {
        if (e.Handled || _presentation is null || FindCell(e.OriginalSource as DependencyObject) is not { } cell) return;
        var point = e.GetCurrentPoint(this);
        var right = point.Properties.IsRightButtonPressed;
        if ((e.Pointer.PointerDeviceType == PointerDeviceType.Mouse || right) &&
            !_presentation.Selection.IsSelected(cell.RowIndex, cell.ColumnIndex)) PointerSelect(cell, e, right);
        else { _pressedPoint = point.Position; _pressedPointer = e.Pointer.PointerId; }
    }
    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        _rowDragCandidate = null;
        base.OnPointerReleased(e);
        if (IsOwnInput(e.OriginalSource as DependencyObject)) _selectionInteraction?.OnPointerReleased(this, e);
    }
    internal void ProcessSelectionPointerReleased(PointerRoutedEventArgs e)
    {
        var pressed = _pressedPoint;
        _pressedPoint = null;
        if (e.Handled || pressed is not { } start || e.Pointer.PointerId != _pressedPointer ||
            FindCell(e.OriginalSource as DependencyObject) is not { } cell) return;
        var point = e.GetCurrentPoint(this);
        if (Math.Abs(point.Position.X - start.X) <= 3 && Math.Abs(point.Position.Y - start.Y) <= 3)
            PointerSelect(cell, e, point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased);
    }
    protected override void OnPointerCanceled(PointerRoutedEventArgs e) { _pressedPoint = null; _tapAllowed = false; _rowDragCandidate = null; base.OnPointerCanceled(e); }
    protected override void OnPointerCaptureLost(PointerRoutedEventArgs e) { _pressedPoint = null; _rowDragCandidate = null; base.OnPointerCaptureLost(e); }
    private void PointerSelect(TreeDataGridCell cell, PointerRoutedEventArgs e, bool right)
    {
        var shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
        var toggle = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control) || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Windows);
        if (SelectCell(cell.RowIndex, cell.ColumnIndex, !right && shift, !right && toggle, right))
        {
            // Keep focus in interactive template content that already received
            // the pointer; otherwise focus the actual cell, not only the grid.
            if (!ContainsFocus(cell, FocusedElement) && !cell.Focus(FocusState.Pointer)) Focus(FocusState.Pointer);
            e.Handled = true;
        }
    }
    private TreeDataGridCell? FindCell(DependencyObject? source)
    {
        return TryGetCell(source, out var cell) ? cell : null;
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
