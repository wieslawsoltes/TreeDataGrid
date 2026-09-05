using System;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Core = global::TreeDataGridCore;

namespace Avalonia.Controls.Selection
{
    /// <summary>View input and visible-index mapping for native Core cell selection.</summary>
    internal sealed class TreeDataGridCellSelectionInteraction<TModel> : ITreeDataGridSelectionInteraction, IDisposable
        where TModel : class
    {
        private readonly Core.ITreeDataGridSource<TModel> _source;
        private readonly Core.Selection.ITreeDataGridCellSelectionModel<TModel> _selection;
        private Point? _pressedPoint;

        public TreeDataGridCellSelectionInteraction(Core.ITreeDataGridSource<TModel> source,
            Core.Selection.ITreeDataGridCellSelectionModel<TModel> selection)
        {
            _source = source;
            _selection = selection;
            selection.SelectionChanged += OnSelectionChanged;
        }
        public event EventHandler? SelectionChanged;
        public void Dispose() => _selection.SelectionChanged -= OnSelectionChanged;
        private void OnSelectionChanged(object? sender, EventArgs e) => SelectionChanged?.Invoke(this, e);

        public bool IsCellSelected(int columnIndex, int rowIndex) =>
            TryGetIndex(columnIndex, rowIndex, out var index) && _selection.IsSelected(index);

        public void OnKeyDown(TreeDataGrid sender, KeyEventArgs e)
        {
            if (e.Handled || sender.RowsPresenter is null || sender.Columns is not { Count: > 0 } columns ||
                _source.Rows.Count == 0) return;
            var (dx, dy) = e.Key switch
            {
                Key.Up => (0, -1), Key.Down => (0, 1),
                Key.Left => (-1, 0), Key.Right => (1, 0), _ => (0, 0)
            };
            if (dx == 0 && dy == 0) return;
            var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !_selection.SingleSelect;
            var current = shift ? _selection.RangeAnchorIndex : _selection.SelectedIndex;
            var column = VisibleColumnIndex(current.ColumnIndex);
            var row = _source.Rows.ModelIndexToRowIndex(current.RowIndex);
            var nextColumn = Math.Clamp(column < 0 ? 0 : column + dx, 0, columns.Count - 1);
            var nextRow = Math.Clamp(row < 0 ? 0 : row + dy, 0, _source.Rows.Count - 1);
            if (!TryGetIndex(nextColumn, nextRow, out var target) || sender.QueryCancelSelection()) return;
            Select(target, shift);
            sender.ColumnHeadersPresenter?.BringIntoView(nextColumn);
            var realized = sender.RowsPresenter.BringIntoView(nextRow);
            (realized as TreeDataGridRow)?.TryGetCell(nextColumn)?.Focus();
            e.Handled = true;
        }

        public void OnPointerPressed(TreeDataGrid sender, PointerPressedEventArgs e)
        {
            _pressedPoint = null;
            var selectOnPress = e.Pointer.Type == PointerType.Mouse ||
                e.Pointer.Type == PointerType.Pen && e.GetCurrentPoint(sender).Properties.IsRightButtonPressed;
            if (!e.Handled && selectOnPress && e.Source is Control control &&
                sender.TryGetCell(control, out var cell) && !IsCellSelected(cell.ColumnIndex, cell.RowIndex))
                PointerSelect(sender, cell.ColumnIndex, cell.RowIndex, e);
            else if (!e.Handled)
                _pressedPoint = e.GetPosition(sender);
        }

        public void OnPointerReleased(TreeDataGrid sender, PointerReleasedEventArgs e)
        {
            var pressed = _pressedPoint;
            _pressedPoint = null;
            if (!e.Handled && pressed is { } start && e.Source is Control control &&
                sender.TryGetCell(control, out var cell))
            {
                var end = e.GetPosition(sender);
                if (Math.Abs(end.X - start.X) <= 3 && Math.Abs(end.Y - start.Y) <= 3)
                    PointerSelect(sender, cell.ColumnIndex, cell.RowIndex, e);
            }
        }

        private void PointerSelect(TreeDataGrid sender, int column, int row, PointerEventArgs e)
        {
            if (!TryGetIndex(column, row, out var target)) return;
            var rightButton = e.GetCurrentPoint(sender).Properties.PointerUpdateKind is
                PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased;
            if ((!rightButton || !_selection.IsSelected(target)) && !sender.QueryCancelSelection())
                Select(target, !rightButton && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !_selection.SingleSelect);
            e.Handled = true;
        }

        private void Select(Core.CellIndex target, bool range)
        {
            var anchor = _selection.AnchorIndex;
            var anchorRow = _source.Rows.ModelIndexToRowIndex(anchor.RowIndex);
            if (!range || anchor.ColumnIndex < 0 || anchorRow < 0)
                _selection.SelectedIndex = target;
            else
            {
                var row = _source.Rows.ModelIndexToRowIndex(target.RowIndex);
                _selection.SetSelectedRange(anchor, InclusiveCount(target.ColumnIndex - anchor.ColumnIndex),
                    InclusiveCount(row - anchorRow));
            }
        }
        private static int InclusiveCount(int delta) => delta >= 0 ? delta + 1 : delta - 1;
        private bool TryGetIndex(int column, int row, out Core.CellIndex index)
        {
            var modelColumn = ModelColumnIndex(column);
            if (modelColumn < 0 || (uint)row >= (uint)_source.Rows.Count)
            {
                index = default;
                return false;
            }
            index = new(modelColumn, _source.Rows.RowIndexToModelIndex(row));
            return true;
        }
        private int ModelColumnIndex(int visible)
        {
            if (visible < 0) return -1;
            for (var i = 0; i < _source.Columns.Count; ++i)
                if (_source.Columns[i].IsVisible && visible-- == 0) return i;
            return -1;
        }
        private int VisibleColumnIndex(int model)
        {
            if ((uint)model >= (uint)_source.Columns.Count || !_source.Columns[model].IsVisible) return -1;
            var visible = 0;
            for (var i = 0; i < model; ++i)
                if (_source.Columns[i].IsVisible) ++visible;
            return visible;
        }
    }
}
