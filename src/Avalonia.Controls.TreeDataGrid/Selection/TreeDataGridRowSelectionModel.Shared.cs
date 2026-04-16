using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Avalonia.Controls.Selection
{
    public partial class TreeDataGridRowSelectionModel<TModel> : TreeSelectionModelBase<TModel>,
        ITreeDataGridRowSelectionModel<TModel>,
        ITreeDataGridSingleSelectSupport,
        ITreeDataGridSelectionInteraction
        where TModel : class
    {
        private static readonly Point s_InvalidPoint = new(double.NegativeInfinity, double.NegativeInfinity);

        protected readonly ITreeDataGridSource<TModel> _source;
        private EventHandler? _viewSelectionChanged;
        private Point _pressedPoint = s_InvalidPoint;
        private bool _raiseViewSelectionChanged;

        public TreeDataGridRowSelectionModel(ITreeDataGridSource<TModel> source)
            : base(source.Items)
        {
            _source = source;
            SelectionChanged += OnSelectionChanged;
        }

        event EventHandler? ITreeDataGridSelectionInteraction.SelectionChanged
        {
            add => _viewSelectionChanged += value;
            remove => _viewSelectionChanged -= value;
        }

        IEnumerable? ITreeDataGridSelection.Source
        {
            get => Source;
            set => Source = value;
        }

        protected internal override IEnumerable<TModel>? GetChildren(TModel node)
        {
            if (_source is HierarchicalTreeDataGridSource<TModel> treeSource)
                return treeSource.GetModelChildren(node);

            return null;
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
            _ = BringRowIntoView(sender, anchorRowIndex);

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
                FocusRow(sender, BringRowIntoView(sender, newIndex));
                e.Handled = true;
            }

            if (!e.Handled &&
                direction == NavigationDirection.Right &&
                anchor?.Rows is HierarchicalRows<TModel> hierarchicalRows2 &&
                hierarchicalRows2[anchorRowIndex].IsExpanded)
            {
                var newIndex = anchorRowIndex + 1;
                UpdateSelection(sender, newIndex, rangeModifier: shift);
                FocusRow(sender, BringRowIntoView(sender, newIndex));
                e.Handled = true;
            }
        }

        void ITreeDataGridSelectionInteraction.OnPreviewKeyDown(TreeDataGrid sender, KeyEventArgs e)
        {
            void UpdateSelectionAndBringIntoView(int newIndex)
            {
                UpdateSelection(sender, newIndex);
                FocusRow(sender, BringRowIntoView(sender, newIndex));
                e.Handled = true;
            }

            if ((e.Key != Key.PageDown && e.Key != Key.PageUp) || sender.RowsPresenter?.Items is null)
                return;

            var children = GetRealizedRows(sender);
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
                    if (IsRowFullyVisibleToUser(sender, children[i]))
                    {
                        newIndex = children[i].RowIndex;
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

                if (childrenCount + selectedIndex - 1 <= sender.RowsPresenter.Items.Count)
                    newIndex = childrenCount + selectedIndex - 2;
                else
                    newIndex = sender.RowsPresenter.Items.Count - 1;
            }
            else
            {
                for (var i = 0; i < childrenCount; ++i)
                {
                    if (IsRowFullyVisibleToUser(sender, children[i]))
                    {
                        newIndex = children[i].RowIndex;
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

                if (isIndexSet && selectedIndex - childrenCount + 2 > 0)
                    newIndex = selectedIndex - childrenCount + 2;
                else
                    newIndex = 0;
            }

            UpdateSelectionAndBringIntoView(newIndex);
        }

        void ITreeDataGridSelectionInteraction.OnPointerPressed(TreeDataGrid sender, PointerPressedEventArgs e)
        {
            var pointerSupportSelectionOnPress = e.Pointer.Type switch
            {
                PointerType.Mouse => true,
                PointerType.Pen => e.GetCurrentPoint(null).Properties.IsRightButtonPressed,
                _ => false
            };

            if (!e.Handled &&
                pointerSupportSelectionOnPress &&
                e.Source is object source &&
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
                e.Source is object source &&
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

        private void OnSelectionChanged(object? sender, TreeDataGridSelectionChangedEventArgs<TModel> e)
        {
            if (!IsSourceCollectionChanging)
                _viewSelectionChanged?.Invoke(this, e);
            else
                _raiseViewSelectionChanged = true;
        }

        private void PointerSelect(TreeDataGrid sender, TreeDataGridRow row, PointerEventArgs e)
        {
            var point = e.GetCurrentPoint(sender);
            var toggleModifier = GetToggleModifier(sender, e);
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

                if (direction == NavigationDirection.Left && expander.IsExpanded)
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

            FocusRow(treeDataGrid, BringRowIntoView(treeDataGrid, newRowIndex));
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

            if (modelIndex == default)
                return;

            var multi = !SingleSelect;
            var toggle = toggleModifier;
            var range = multi && rangeModifier;

            if (rightButton && IsSelected(modelIndex))
            {
                return;
            }
            else if (range)
            {
                if (treeDataGrid.QueryCancelSelection())
                    return;

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

                        if (i == rowIndex)
                            break;

                        i += step;
                    }
                }
            }
            else if (toggle)
            {
                if (treeDataGrid.QueryCancelSelection())
                    return;

                if (!IsSelected(modelIndex))
                    Select(modelIndex);
                else if (!SingleSelect)
                    Deselect(modelIndex);
            }
            else if (SelectedIndex != modelIndex || Count > 1)
            {
                if (treeDataGrid.QueryCancelSelection())
                    return;

                SelectedIndex = modelIndex;
            }
        }

        private static TreeDataGridRow? BringRowIntoView(TreeDataGrid owner, int rowIndex)
        {
            _ = owner.RowsPresenter?.BringIntoView(rowIndex);
            return owner.TryGetRow(rowIndex);
        }

        private static partial IReadOnlyList<TreeDataGridRow> GetRealizedRows(TreeDataGrid sender);
        private static partial bool IsRowFullyVisibleToUser(TreeDataGrid sender, TreeDataGridRow row);
        private static partial bool GetToggleModifier(TreeDataGrid sender, PointerEventArgs e);
        private static partial void FocusRow(TreeDataGrid owner, TreeDataGridRow? row);
    }
}
