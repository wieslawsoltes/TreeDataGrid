using System;
using System.Collections.Generic;
using System.Linq;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinSize = Windows.Foundation.Size;
using WinRect = Windows.Foundation.Rect;

namespace Uno.Controls.Primitives
{
    public class TreeDataGridRowsPresenter : Panel
    {
        private readonly List<TreeDataGridRow> _rows = new();
        private readonly Action<Control, int> _recycleRow;
        private IReadOnlyList<double>? _columnWidths;
        private RealizedStackElements? _measureElements;
        private RealizedStackElements? _realizedElements;
        private double _estimatedRowHeight = 25;
        private double _totalWidth;
        private double _viewportOffset;
        private double _viewportHeight;

        public TreeDataGridRowsPresenter()
        {
            _recycleRow = RecycleRow;
        }

        internal TreeDataGrid? Owner { get; set; }
        internal TreeDataGridElementFactory? ElementFactory { get; set; }
        internal IColumns? Columns { get; set; }
        internal IRows? Items { get; set; }
        internal IReadOnlyList<TreeDataGridRow> RealizedRows => _rows;

        public IEnumerable<TreeDataGridRow> GetRealizedElements() => _rows;

        public void Realize()
        {
            InvalidateMeasure();
            InvalidateArrange();
        }

        public void Unrealize()
        {
            _realizedElements?.RecycleAllElements(_recycleRow);
            _realizedElements?.ResetForReuse();
            _measureElements?.ResetForReuse();
            _rows.Clear();
            Children.Clear();
        }

        public void ApplyColumnWidths(IReadOnlyList<double> widths)
        {
            _columnWidths = widths;
            _totalWidth = widths.Count == 0 ? 0 : System.Linq.Enumerable.Sum(widths);

            foreach (var row in _rows)
                row.ApplyColumnWidths(widths);

            Width = widths.Count == 0 ? double.NaN : _totalWidth;
            InvalidateMeasure();
            InvalidateArrange();
        }

        public void UpdateSelection()
        {
            foreach (var row in _rows)
                row.UpdateSelection();
        }

        public void UpdateDragState(bool canDrag)
        {
            foreach (var row in _rows)
                row.UpdateDragState(canDrag);
        }

        public void UpdateViewport(double verticalOffset, double viewportHeight)
        {
            if (DoubleUtils.AreClose(_viewportOffset, verticalOffset) &&
                DoubleUtils.AreClose(_viewportHeight, viewportHeight))
            {
                return;
            }

            _viewportOffset = verticalOffset;
            _viewportHeight = viewportHeight;
            InvalidateMeasure();
            InvalidateArrange();
        }

        public TreeDataGridRow? TryGetElement(int rowIndex)
        {
            return _realizedElements?.GetElement(rowIndex) as TreeDataGridRow;
        }

        public TreeDataGridRow? BringIntoView(int rowIndex)
        {
            var row = TryGetElement(rowIndex);

            if (row is not null)
            {
                row.StartBringIntoView();
                return row;
            }

            if (Owner?.Scroll is null)
                return null;

            var targetY = _realizedElements?.GetOrEstimateElementU(rowIndex, ref _estimatedRowHeight) ??
                rowIndex * _estimatedRowHeight;
            var targetOffset = Math.Max(0, targetY - Math.Max(0, Owner.Scroll.ViewportHeight - _estimatedRowHeight) / 2);
            Owner.Scroll.ChangeView(null, targetOffset, null, true);
            UpdateViewport(targetOffset, Owner.Scroll.ViewportHeight);
            Owner.UpdateLayout();

            row = TryGetElement(rowIndex);
            row?.StartBringIntoView();
            return row;
        }

        public TreeDataGridRow? BringIntoView(int rowIndex, global::Avalonia.Rect? rect)
        {
            _ = rect;
            return BringIntoView(rowIndex);
        }

        protected override WinSize MeasureOverride(WinSize availableSize)
        {
            if (Owner is null || ElementFactory is null || Columns is null || Items is null || Items.Count == 0)
            {
                _rows.Clear();
                return new WinSize(_totalWidth, 0);
            }

            _realizedElements ??= new RealizedStackElements();
            _measureElements ??= new RealizedStackElements();

            var viewportHeight = GetViewportHeight(availableSize);
            var viewportStart = Math.Max(0, _viewportOffset);
            var viewportEnd = viewportStart + viewportHeight;
            var overscan = Math.Max(_estimatedRowHeight * 4, viewportHeight * 0.5);
            viewportStart = Math.Max(0, viewportStart - overscan);
            viewportEnd += overscan;

            var (anchorIndex, anchorY) = _realizedElements.GetOrEstimateAnchorElementForViewport(
                viewportStart,
                viewportEnd,
                Items.Count,
                ref _estimatedRowHeight);

            if (anchorIndex < 0)
            {
                _rows.Clear();
                return new WinSize(_totalWidth, 0);
            }

            if (anchorIndex < _realizedElements.FirstIndex || anchorIndex > _realizedElements.LastIndex)
                _realizedElements.RecycleAllElements(_recycleRow);

            var width = 0d;
            var lastIndex = anchorIndex - 1;
            var realizedEndY = anchorY;

            var index = anchorIndex;
            var y = anchorY;

            do
            {
                var row = GetOrCreateRow(index);
                var desired = MeasureRow(row, index, availableSize);
                var rowHeight = GetRowHeight(index, desired.Height);
                _measureElements.Add(index, row, y, rowHeight);
                width = Math.Max(width, desired.Width);
                y += rowHeight;
                lastIndex = index;
                ++index;
            } while (y < viewportEnd && index < Items.Count);

            realizedEndY = y;
            _realizedElements.RecycleElementsAfter(lastIndex, _recycleRow);

            index = anchorIndex - 1;
            y = anchorY;

            while (y > viewportStart && index >= 0)
            {
                var row = GetOrCreateRow(index);
                var desired = MeasureRow(row, index, availableSize);
                var rowHeight = GetRowHeight(index, desired.Height);
                y -= rowHeight;
                _measureElements.Add(index, row, y, rowHeight);
                width = Math.Max(width, desired.Width);
                --index;
            }

            _realizedElements.RecycleElementsBefore(index + 1, _recycleRow);

            (_measureElements, _realizedElements) = (_realizedElements, _measureElements);
            _measureElements.ResetForReuse();
            RefreshRealizedRows();

            var remaining = Items.Count - lastIndex - 1;
            var totalHeight = remaining > 0
                ? realizedEndY + (remaining * EstimateRowHeight())
                : realizedEndY;

            if (_totalWidth > 0)
                width = Math.Max(width, _totalWidth);

            return new WinSize(width, Math.Max(0, totalHeight));
        }

        protected override WinSize ArrangeOverride(WinSize finalSize)
        {
            if (_realizedElements is null)
                return finalSize;

            var y = _realizedElements.StartU;
            var width = _totalWidth > 0 ? _totalWidth : finalSize.Width;

            for (var i = 0; i < _realizedElements.Count; ++i)
            {
                if (_realizedElements.Elements[i] is not TreeDataGridRow row)
                    continue;

                var rowHeight = _realizedElements.SizeU[i];
                row.Arrange(new WinRect(0, y, width, rowHeight));
                y += rowHeight;
            }

            return finalSize;
        }

        private TreeDataGridRow GetOrCreateRow(int rowIndex)
        {
            var row = _realizedElements?.GetElement(rowIndex) as TreeDataGridRow;

            if (row is not null)
                return row;

            row = (TreeDataGridRow)ElementFactory!.GetOrCreateElement(Items![rowIndex]);

            if (row.Parent is null)
                Children.Add(row);

            row.Realize(Owner!, ElementFactory!, Columns!, Items!, rowIndex);

            if (_columnWidths is not null)
                row.ApplyColumnWidths(_columnWidths);

            return row;
        }

        private WinSize MeasureRow(TreeDataGridRow row, int rowIndex, WinSize availableSize)
        {
            var constraintWidth = _totalWidth > 0
                ? _totalWidth
                : !double.IsInfinity(availableSize.Width) && availableSize.Width > 0
                    ? availableSize.Width
                    : Owner?.ActualWidth ?? 0;
            var rowModel = Items![rowIndex];
            var rowHeight = rowModel.Height.IsAbsolute ? rowModel.Height.Value : double.NaN;
            var constraintHeight = double.IsNaN(rowHeight) ? double.PositiveInfinity : rowHeight;
            row.Measure(new WinSize(constraintWidth, constraintHeight));
            return row.DesiredSize;
        }

        private double GetRowHeight(int rowIndex, double desiredHeight)
        {
            var rowHeight = Items![rowIndex].Height;

            if (rowHeight.IsAbsolute)
                return rowHeight.Value;

            return desiredHeight > 0 ? desiredHeight : EstimateRowHeight();
        }

        private double GetViewportHeight(WinSize availableSize)
        {
            if (_viewportHeight > 0)
                return _viewportHeight;

            if (!double.IsInfinity(availableSize.Height) && availableSize.Height > 0)
                return availableSize.Height;

            if (Owner?.Scroll?.ViewportHeight > 0)
                return Owner.Scroll.ViewportHeight;

            return Owner?.ActualHeight > 0 ? Owner.ActualHeight : EstimateRowHeight() * 10;
        }

        private double EstimateRowHeight()
        {
            var estimate = _realizedElements?.EstimateElementSizeU() ?? -1;

            if (estimate > 0)
                _estimatedRowHeight = estimate;

            return _estimatedRowHeight > 0 ? _estimatedRowHeight : 25;
        }

        private void RefreshRealizedRows()
        {
            _rows.Clear();

            if (_realizedElements is null)
                return;

            foreach (var row in _realizedElements.Elements.OfType<TreeDataGridRow>())
                _rows.Add(row);
        }

        private void RecycleRow(Control element, int rowIndex)
        {
            _ = rowIndex;

            if (element is not TreeDataGridRow row)
                return;

            row.Unrealize();

            if (row.Parent == this)
                Children.Remove(row);

            ElementFactory?.RecycleElement(row);
        }
    }
}
