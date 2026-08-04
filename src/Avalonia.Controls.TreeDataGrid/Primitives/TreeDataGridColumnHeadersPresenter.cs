using System;
using System.Collections.Generic;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Layout;
using Avalonia.LogicalTree;

namespace Avalonia.Controls.Primitives
{
    public class TreeDataGridColumnHeadersPresenter : TreeDataGridColumnarPresenterBase<IColumn>, IChildIndexProvider
    {
        private bool _isAttachedToVisualTree;
        private IColumns? _layoutInvalidatedColumns;

        public event EventHandler<ChildIndexChangedEventArgs>? ChildIndexChanged;

        protected override Orientation Orientation => Orientation.Horizontal;

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TreeDataGridColumnHeadersPresenterAutomationPeer(this);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            (Items as IColumns)?.CommitActualWidths();
            return base.ArrangeOverride(finalSize);
        }

        protected override Size MeasureElement(int index, Control element, Size availableSize)
        {
            var columns = (IColumns)Items!;
            element.Measure(availableSize);
            return columns.CellMeasured(index, -1, element.DesiredSize);
        }

        protected override void RealizeElement(Control element, IColumn column, int index)
        {
            ((TreeDataGridColumnHeader)element).Realize((IColumns)Items!, index);
            ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, index));
        }

        protected override void UpdateElementIndex(Control element, int oldIndex, int newIndex)
        {
            ((TreeDataGridColumnHeader)element).UpdateColumnIndex(newIndex);
            ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, newIndex));
        }

        protected override void UnrealizeElement(Control element)
        {
            ((TreeDataGridColumnHeader)element).Unrealize();
            ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, ((TreeDataGridColumnHeader)element).ColumnIndex));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == ItemsProperty)
            {
                var oldValue = change.GetOldValue<IReadOnlyList<IColumn>?>();
                var newValue = change.GetNewValue<IReadOnlyList<IColumn>?>();

                if (oldValue is IColumns oldColumns)
                    UnsubscribeFromColumnLayoutInvalidated(oldColumns);
                if (newValue is IColumns newColumns)
                    SubscribeToColumnLayoutInvalidated(newColumns);
            }

            base.OnPropertyChanged(change);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _isAttachedToVisualTree = true;

            if (Items is IColumns columns)
                SubscribeToColumnLayoutInvalidated(columns);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_layoutInvalidatedColumns is { } columns)
                UnsubscribeFromColumnLayoutInvalidated(columns);

            _isAttachedToVisualTree = false;
            base.OnDetachedFromVisualTree(e);
        }

        private void SubscribeToColumnLayoutInvalidated(IColumns columns)
        {
            if (!_isAttachedToVisualTree || ReferenceEquals(_layoutInvalidatedColumns, columns))
                return;

            if (_layoutInvalidatedColumns is { } oldColumns)
                UnsubscribeFromColumnLayoutInvalidated(oldColumns);

            columns.LayoutInvalidated += OnColumnLayoutInvalidated;
            _layoutInvalidatedColumns = columns;
        }

        private void UnsubscribeFromColumnLayoutInvalidated(IColumns columns)
        {
            if (!ReferenceEquals(_layoutInvalidatedColumns, columns))
                return;

            columns.LayoutInvalidated -= OnColumnLayoutInvalidated;
            _layoutInvalidatedColumns = null;
        }

        private void OnColumnLayoutInvalidated(object? sender, EventArgs e)
        {
            InvalidateMeasure();
        }

        public int GetChildIndex(ILogical child)
        {
            if (child is TreeDataGridColumnHeader header)
            {
                return header.ColumnIndex;
            }
            return -1;
        }

        public bool TryGetTotalCount(out int count)
        {
            if (Items is null)
            {
                count = 0;
                return false;
            }

            count = Items.Count;
            return true;
        }
    }
}
