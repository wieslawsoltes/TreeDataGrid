using System;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinRect = Windows.Foundation.Rect;

namespace Avalonia.Controls.Selection
{
    public partial class TreeDataGridRowSelectionModel<TModel> : ITreeDataGridSelectionInteraction
        where TModel : class
    {
        private static readonly Point s_InvalidPoint = new(double.NegativeInfinity, double.NegativeInfinity);
        private EventHandler? _viewSelectionChanged;
        private Point _pressedPoint = s_InvalidPoint;
        private bool _raiseViewSelectionChanged;

        partial void OnConstructed()
        {
            SelectionChanged += (s, e) =>
            {
                if (!IsSourceCollectionChanging)
                    _viewSelectionChanged?.Invoke(this, e);
                else
                    _raiseViewSelectionChanged = true;
            };
        }

        event EventHandler? ITreeDataGridSelectionInteraction.SelectionChanged
        {
            add => _viewSelectionChanged += value;
            remove => _viewSelectionChanged -= value;
        }

        bool ITreeDataGridSelectionInteraction.IsRowSelected(IRow rowModel)
        {
            if (rowModel is IModelIndexableRow indexable)
                return IsSelected(indexable.ModelIndexPath);

            return false;
        }

        bool ITreeDataGridSelectionInteraction.IsRowSelected(int rowIndex)
        {
            if (rowIndex >= 0 && rowIndex < _source.Rows.Count &&
                _source.Rows[rowIndex] is IModelIndexableRow indexable)
            {
                return IsSelected(indexable.ModelIndexPath);
            }

            return false;
        }

        void ITreeDataGridSelectionInteraction.OnKeyDown(TreeDataGrid sender, KeyEventArgs e)
        {
            if (sender.RowsPresenter is null || e.Handled)
                return;

            var direction = e.Key.ToNavigationDirection();
            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            if (!direction.HasValue)
                return;

            var anchorRowIndex = _source.Rows.ModelIndexToRowIndex(AnchorIndex);
            sender.RowsPresenter.BringIntoView(anchorRowIndex);

            var anchor = sender.TryGetRow(anchorRowIndex);

            if (anchor is not null && !ctrl)
                e.Handled = TryKeyExpandCollapse(sender, direction.Value, anchor);

            if (!e.Handled && (!ctrl || shift))
                e.Handled = MoveSelection(sender, direction.Value, shift, anchor);

            if (!e.Handled &&
                direction == NavigationDirection.Left &&
                anchor?.Rows is HierarchicalRows<TModel> hierarchicalRows &&
                anchorRowIndex > 0)
            {
                var newIndex = hierarchicalRows.GetParentRowIndex(AnchorIndex);
                UpdateSelection(sender, newIndex, rangeModifier: shift);
                FocusRow(sender, sender.RowsPresenter.BringIntoView(newIndex));
                e.Handled = true;
            }

            if (!e.Handled &&
                direction == NavigationDirection.Right &&
                anchor?.Rows is HierarchicalRows<TModel> hierarchicalRows2 &&
                hierarchicalRows2[anchorRowIndex].IsExpanded)
            {
                var newIndex = anchorRowIndex + 1;
                UpdateSelection(sender, newIndex, rangeModifier: shift);
                FocusRow(sender, sender.RowsPresenter.BringIntoView(newIndex));
                e.Handled = true;
            }
        }

        void ITreeDataGridSelectionInteraction.OnPreviewKeyDown(TreeDataGrid sender, KeyEventArgs e)
        {
            static bool IsRowFullyVisibleToUser(TreeDataGrid sender, TreeDataGridRow row)
            {
                if (sender.Scroll is null || sender.RowsPresenter is null || row.ActualHeight <= 0)
                    return false;

                var transform = row.TransformToVisual(sender.RowsPresenter);
                var bounds = transform.TransformBounds(new WinRect(0, 0, row.ActualWidth, row.ActualHeight));
                var viewportTop = sender.Scroll.VerticalOffset;
                var viewportBottom = viewportTop + sender.Scroll.ViewportHeight;

                return bounds.Top >= viewportTop && bounds.Bottom <= viewportBottom;
            }

            static bool GetRowIndexIfFullyVisible(TreeDataGrid sender, TreeDataGridRow? row, out int index)
            {
                if (row is not null && IsRowFullyVisibleToUser(sender, row))
                {
                    index = row.RowIndex;
                    return true;
                }

                index = -1;
                return false;
            }

            void UpdateSelectionAndBringIntoView(int newIndex)
            {
                UpdateSelection(sender, newIndex);
                FocusRow(sender, sender.RowsPresenter?.BringIntoView(newIndex));
                e.Handled = true;
            }

            if ((e.Key != Key.PageDown && e.Key != Key.PageUp) || sender.RowsPresenter?.Items is null)
                return;

            var children = sender.RowsPresenter.RealizedRows;
            var childrenCount = children.Count;

            if (childrenCount == 0)
                return;

            var newIndex = 0;
            var isIndexSet = false;
            var selectedIndex = Math.Max(sender.Rows?.ModelIndexToRowIndex(SelectedIndex) ?? -1, 0);

            if (e.Key == Key.PageDown)
            {
                for (var i = childrenCount - 1; i >= 0; --i)
                {
                    if (GetRowIndexIfFullyVisible(sender, children[i], out var index))
                    {
                        newIndex = index;
                        isIndexSet = true;
                        break;
                    }
                }

                if (isIndexSet &&
                    selectedIndex != newIndex &&
                    sender.TryGetRow(selectedIndex) is TreeDataGridRow row &&
                    IsRowFullyVisibleToUser(sender, row))
                {
                    UpdateSelectionAndBringIntoView(newIndex);
                    return;
                }
                else if (childrenCount + selectedIndex - 1 <= sender.RowsPresenter.Items.Count)
                {
                    newIndex = childrenCount + selectedIndex - 2;
                }
                else
                {
                    newIndex = sender.RowsPresenter.Items.Count - 1;
                }
            }
            else
            {
                for (var i = 0; i < childrenCount; ++i)
                {
                    if (GetRowIndexIfFullyVisible(sender, children[i], out var index))
                    {
                        newIndex = index;
                        isIndexSet = true;
                        break;
                    }
                }

                if (isIndexSet &&
                    selectedIndex != newIndex &&
                    sender.TryGetRow(selectedIndex) is TreeDataGridRow row &&
                    IsRowFullyVisibleToUser(sender, row))
                {
                    UpdateSelectionAndBringIntoView(newIndex);
                    return;
                }
                else if (isIndexSet && selectedIndex - childrenCount + 2 > 0)
                {
                    newIndex = selectedIndex - childrenCount + 2;
                }
                else
                {
                    newIndex = 0;
                }
            }

            UpdateSelectionAndBringIntoView(newIndex);
        }

        void ITreeDataGridSelectionInteraction.OnPointerPressed(TreeDataGrid sender, PointerPressedEventArgs e)
        {
            // Keep row pointer-selection behavior aligned with the Avalonia implementation:
            // select immediately for mouse clicks and pen secondary clicks, otherwise defer
            // selection until release so touch scroll starts do not unexpectedly reselect rows.
            var pointerSupportSelectionOnPress = e.Pointer.Type switch
            {
                PointerType.Mouse => true,
                PointerType.Pen => e.GetCurrentPoint(null).Properties.IsRightButtonPressed,
                _ => false
            };

            if (!e.Handled &&
                pointerSupportSelectionOnPress &&
                e.Source is Control source &&
                sender.TryGetRow(source, out var row) &&
                _source.Rows.RowIndexToModelIndex(row.RowIndex) is { } modelIndex &&
                !IsSelected(modelIndex))
            {
                PointerSelect(sender, row, e);
                _pressedPoint = s_InvalidPoint;
            }
            else
            {
                _pressedPoint = e.GetPosition(sender);
            }
        }

        void ITreeDataGridSelectionInteraction.OnPointerReleased(TreeDataGrid sender, PointerReleasedEventArgs e)
        {
            if (!e.Handled &&
                _pressedPoint != s_InvalidPoint &&
                e.Source is Control source &&
                sender.TryGetRow(source, out var row))
            {
                var p = e.GetPosition(sender);

                if (Math.Abs(p.X - _pressedPoint.X) <= 3 || Math.Abs(p.Y - _pressedPoint.Y) <= 3)
                    PointerSelect(sender, row, e);
            }
        }

        protected override void OnSourceCollectionChangeFinished()
        {
            base.OnSourceCollectionChangeFinished();

            if (_raiseViewSelectionChanged)
            {
                _viewSelectionChanged?.Invoke(this, EventArgs.Empty);
                _raiseViewSelectionChanged = false;
            }
        }

        private void PointerSelect(TreeDataGrid sender, TreeDataGridRow row, PointerEventArgs e)
        {
            var point = e.GetCurrentPoint(sender);
            var toggleModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                e.KeyModifiers.HasFlag(KeyModifiers.Meta);
            var isRightButton = point.Properties.PointerUpdateKind is PointerUpdateKind.RightButtonPressed or
                PointerUpdateKind.RightButtonReleased;

            UpdateSelection(
                sender,
                row.RowIndex,
                rangeModifier: e.KeyModifiers.HasFlag(KeyModifiers.Shift),
                toggleModifier: toggleModifier,
                rightButton: isRightButton);
            e.Handled = true;
        }

        private bool TryKeyExpandCollapse(
            TreeDataGrid treeDataGrid,
            NavigationDirection direction,
            TreeDataGridRow focused)
        {
            if (treeDataGrid.RowsPresenter is null || focused.RowIndex < 0)
                return false;

            var row = _source.Rows[focused.RowIndex];

            if (row is IExpander expander)
            {
                if (direction == NavigationDirection.Right && !expander.IsExpanded)
                {
                    expander.IsExpanded = true;
                    return true;
                }
                else if (direction == NavigationDirection.Left && expander.IsExpanded)
                {
                    expander.IsExpanded = false;
                    return true;
                }
            }

            return false;
        }

        private bool MoveSelection(
            TreeDataGrid treeDataGrid,
            NavigationDirection direction,
            bool rangeModifier,
            TreeDataGridRow? focused)
        {
            if (treeDataGrid.RowsPresenter is null || _source.Columns.Count == 0 || _source.Rows.Count == 0)
                return false;

            var currentRowIndex = focused?.RowIndex ?? _source.Rows.ModelIndexToRowIndex(SelectedIndex);
            int newRowIndex;

            if (direction == NavigationDirection.First || direction == NavigationDirection.Last)
            {
                newRowIndex = direction == NavigationDirection.First ? 0 : _source.Rows.Count - 1;
            }
            else
            {
                (_, var y) = direction switch
                {
                    NavigationDirection.Up => (0, -1),
                    NavigationDirection.Down => (0, 1),
                    NavigationDirection.Left => (-1, 0),
                    NavigationDirection.Right => (1, 0),
                    _ => (0, 0)
                };

                newRowIndex = Math.Max(0, Math.Min(currentRowIndex + y, _source.Rows.Count - 1));
            }

            if (newRowIndex != currentRowIndex)
                UpdateSelection(treeDataGrid, newRowIndex, rangeModifier);

            if (newRowIndex == currentRowIndex)
                return false;

            treeDataGrid.RowsPresenter.BringIntoView(newRowIndex);
            FocusRow(treeDataGrid, treeDataGrid.TryGetRow(newRowIndex));
            return true;
        }

        private void UpdateSelection(
            TreeDataGrid treeDataGrid,
            int rowIndex,
            bool rangeModifier = false,
            bool toggleModifier = false,
            bool rightButton = false)
        {
            var modelIndex = _source.Rows.RowIndexToModelIndex(rowIndex);

            if (modelIndex == default || treeDataGrid.QueryCancelSelection())
                return;

            var mode = SingleSelect ? SelectionMode.Single : SelectionMode.Multiple;
            var multi = (mode & SelectionMode.Multiple) != 0;
            var toggle = toggleModifier;
            var range = multi && rangeModifier;

            if (rightButton && IsSelected(modelIndex))
            {
                return;
            }
            else if (range)
            {
                var anchor = RangeAnchorIndex != default ? RangeAnchorIndex : AnchorIndex;
                var i = Math.Max(_source.Rows.ModelIndexToRowIndex(anchor), 0);
                var step = i < rowIndex ? 1 : -1;

                using (BatchUpdate())
                {
                    Clear();

                    while (true)
                    {
                        var current = _source.Rows.RowIndexToModelIndex(i);
                        Select(current);
                        anchor = current;

                        if (i == rowIndex)
                            break;

                        i += step;
                    }
                }
            }
            else if (toggle)
            {
                if (!IsSelected(modelIndex))
                    Select(modelIndex);
                else if (!SingleSelect)
                    Deselect(modelIndex);
            }
            else if (SelectedIndex != modelIndex || Count > 1)
            {
                SelectedIndex = modelIndex;
            }
        }

        private static void FocusRow(TreeDataGrid owner, TreeDataGridRow? row)
        {
            if (row?.CellsPresenter is null)
                return;

            if (owner.XamlRoot is not null &&
                Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(owner.XamlRoot) is Control currentFocus &&
                owner.TryGetCell(currentFocus, out var currentCell) &&
                row.TryGetCell(currentCell.ColumnIndex) is { } newCell &&
                newCell.IsTabStop)
            {
                newCell.Focus(FocusState.Programmatic);
                return;
            }

            foreach (var cell in row.CellsPresenter.RealizedCells)
            {
                if (!cell.IsTabStop)
                    continue;

                cell.Focus(FocusState.Programmatic);
                break;
            }
        }
    }
}
