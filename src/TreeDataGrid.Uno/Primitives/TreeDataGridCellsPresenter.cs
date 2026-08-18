using System.Collections.Generic;
using Uno.Controls.Models.TreeDataGrid;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinSize = Windows.Foundation.Size;
using WinRect = Windows.Foundation.Rect;

namespace Uno.Controls.Primitives
{
    public class TreeDataGridCellsPresenter : Panel
    {
        private readonly List<TreeDataGridCell> _cells = new();
        private IReadOnlyList<double>? _columnWidths;

        internal TreeDataGrid? Owner { get; set; }
        internal TreeDataGridElementFactory? ElementFactory { get; set; }
        internal IColumns? Items { get; set; }
        internal IRows? Rows { get; set; }
        internal int RowIndex { get; private set; } = -1;
        internal IReadOnlyList<TreeDataGridCell> RealizedCells => _cells;

        public void Realize(int rowIndex)
        {
            Unrealize();
            RowIndex = rowIndex;

            if (Owner is null || ElementFactory is null || Items is null || Rows is null)
                return;

            for (var columnIndex = 0; columnIndex < Items.Count; ++columnIndex)
            {
                var model = Rows.RealizeCell(Items[columnIndex], columnIndex, rowIndex);
                var cell = (TreeDataGridCell)ElementFactory.GetOrCreateElement(model);
                _cells.Add(cell);
                Children.Add(cell);
                cell.Realize(Owner, ElementFactory, model, columnIndex, rowIndex);
            }

            InvalidateMeasure();
            InvalidateArrange();
        }

        public void Unrealize()
        {
            if (Rows is not null)
            {
                foreach (var cell in _cells)
                {
                    if (cell.Model is not null && cell.ColumnIndex >= 0 && RowIndex >= 0)
                        Rows.UnrealizeCell(cell.Model, cell.ColumnIndex, RowIndex);
                    cell.Unrealize();
                    ElementFactory?.RecycleElement(cell);
                }
            }
            else
            {
                foreach (var cell in _cells)
                {
                    cell.Unrealize();
                    ElementFactory?.RecycleElement(cell);
                }
            }

            _cells.Clear();
            Children.Clear();
            RowIndex = -1;
        }

        public void UpdateRowIndex(int rowIndex)
        {
            RowIndex = rowIndex;
            foreach (var cell in _cells)
                cell.UpdateRowIndex(rowIndex);
        }

        public void UpdateSelection()
        {
            foreach (var cell in _cells)
                cell.UpdateSelection();
        }

        public void ApplyColumnWidths(IReadOnlyList<double> widths)
        {
            _columnWidths = widths;
            Width = widths.Count == 0 ? double.NaN : System.Linq.Enumerable.Sum(widths);
            InvalidateMeasure();
            InvalidateArrange();
        }

        public TreeDataGridCell? TryGetElement(int columnIndex)
        {
            return columnIndex >= 0 && columnIndex < _cells.Count ? _cells[columnIndex] : null;
        }

        protected override WinSize MeasureOverride(WinSize availableSize)
        {
            double width = 0;
            double height = 0;

            for (var i = 0; i < _cells.Count; ++i)
            {
                var child = _cells[i];
                var childWidth = _columnWidths is not null && i < _columnWidths.Count
                    ? _columnWidths[i]
                    : double.PositiveInfinity;
                child.Measure(new WinSize(childWidth, availableSize.Height));
                width += _columnWidths is not null && i < _columnWidths.Count ? _columnWidths[i] : child.DesiredSize.Width;
                height = System.Math.Max(height, child.DesiredSize.Height);
            }

            return new WinSize(width, height);
        }

        protected override WinSize ArrangeOverride(WinSize finalSize)
        {
            double x = 0;

            for (var i = 0; i < _cells.Count; ++i)
            {
                var child = _cells[i];
                var width = _columnWidths is not null && i < _columnWidths.Count ? _columnWidths[i] : child.DesiredSize.Width;
                child.Arrange(new WinRect(x, 0, width, finalSize.Height));
                x += width;
            }

            return finalSize;
        }
    }
}
