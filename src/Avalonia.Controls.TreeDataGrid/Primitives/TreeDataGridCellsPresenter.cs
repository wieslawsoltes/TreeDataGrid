using System;
using System.Linq;
using System.Xml.Linq;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Avalonia.Controls.Primitives
{
    public class TreeDataGridCellsPresenter : TreeDataGridColumnarPresenterBase<IColumn>, IChildIndexProvider
    {
        public static readonly DirectProperty<TreeDataGridCellsPresenter, IRows?> RowsProperty =
            AvaloniaProperty.RegisterDirect<TreeDataGridCellsPresenter, IRows?>(
                nameof(Rows),
                o => o.Rows,
                (o, v) => o.Rows = v);

        private IRows? _rows;
        private bool _rebindRealizedCells;

        public event EventHandler<ChildIndexChangedEventArgs>? ChildIndexChanged;

        public IRows? Rows
        {
            get => _rows;
            set
            {
                if (!ReferenceEquals(_rows, value))
                {
                    RecycleDeferredCells();
                    SetAndRaise(RowsProperty, ref _rows, value);
                }
            }
        }

        public int RowIndex { get; private set; } = -1;

        protected override Orientation Orientation => Orientation.Horizontal;

        public void Realize(int index)
        {
            if (RowIndex != -1)
                throw new InvalidOperationException("Row is already realized.");

            RowIndex = index;

            if (_rebindRealizedCells)
            {
                _rebindRealizedCells = false;
                RebindRealizedCells();
            }

            InvalidateMeasure();
        }

        public void Unrealize()
        {
            if (RowIndex == -1)
                throw new InvalidOperationException("Row is not realized.");
            RowIndex = -1;

            // A row leaving one edge of the viewport is commonly reused at the other edge in
            // the same measure pass. Keep its cells realized until we know whether the row is
            // rebound or actually detached, avoiding a recycle/factory/re-realize round-trip
            // for every column.
            _rebindRealizedCells = true;
        }

        public void UpdateRowIndex(int index)
        {
            if (index < 0 || Rows is null || index >= Rows.Count)
                return;

            if (RowIndex == -1)
                return;

            RowIndex = index;

            foreach (var element in RealizedElements)
            {
                if (element is TreeDataGridCell { RowIndex: >= 0, ColumnIndex: >= 0 } cell)
                    cell.UpdateRowIndex(index);
            }
        }

        protected override Rect? GetParentPresenterViewPort()
        {
            var parentRowPresenter = this.GetVisualAncestors().OfType<TreeDataGridRowsPresenter>().FirstOrDefault();

            return parentRowPresenter?.Viewport;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return RowIndex == -1 ? default : base.MeasureOverride(availableSize);
        }

        protected override Size MeasureElement(int index, Control element, Size availableSize)
        {
            // Optimization: Skip measure if the element was already measured with the same constraint
            // and its measure is still valid. This significantly improves reattachment performance.
            var previousConstraint = LayoutInformation.GetPreviousMeasureConstraint(element);
            if (previousConstraint.HasValue &&
                previousConstraint.Value == availableSize &&
                element.IsMeasureValid &&
                element.DesiredSize != default)
            {
                return ((IColumns)Items!).CellMeasured(index, RowIndex, element.DesiredSize);
            }

            element.Measure(availableSize);
            return ((IColumns)Items!).CellMeasured(index, RowIndex, element.DesiredSize);
        }

        protected override Control GetElementFromFactory(IColumn column, int index)
        {
            var model = _rows!.RealizeCell(column, index, RowIndex);
            var cell = (TreeDataGridCell)GetElementFromFactory(model, index, this);
            cell.Realize(ElementFactory!, GetSelection(), model, index, RowIndex);
            return cell;
        }

        protected override void RealizeElement(Control element, IColumn column, int index)
        {
            var cell = (TreeDataGridCell)element;

            if (cell.ColumnIndex == index && cell.RowIndex == RowIndex)
            {
                ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, index));
            }
            else if (cell.ColumnIndex == -1 && cell.RowIndex == -1)
            {
                var model = _rows!.RealizeCell(column, index, RowIndex);
                ((TreeDataGridCell)element).Realize(ElementFactory!, GetSelection(), model, index, RowIndex);
                ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, index));
            }
            else
            {
                throw new InvalidOperationException("Cell already realized");
            }
        }

        protected override void UnrealizeElement(Control element)
        {
            var cell = (TreeDataGridCell)element;
            _rows!.UnrealizeCell(cell.Model!, cell.ColumnIndex, cell.RowIndex);
            cell.Unrealize();
            ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, cell.RowIndex));
        }

        protected override void UpdateElementIndex(Control element, int oldIndex, int newIndex)
        {
            ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, newIndex));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BackgroundProperty)
                InvalidateVisual();
        }

        internal void UpdateSelection(ITreeDataGridSelectionInteraction? selection)
        {
            foreach (var element in RealizedElements)
            {
                if (element is TreeDataGridCell { RowIndex: >= 0, ColumnIndex: >= 0 } cell)
                    cell.UpdateSelection(selection);
            }
        }

        internal void UnrealizeOnRowRemoved()
        {
            if (RowIndex == -1)
                throw new InvalidOperationException("Row is not realized.");
            RowIndex = -1;
            _rebindRealizedCells = false;
            RecycleAllElementsOnItemRemoved(
                preserveVisualTreeMembership: true,
                preserveLogicalTreeMembership: true);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // If the owning row was not reused during the layout pass, release the deferred cell
            // models while their controls are still parented to this presenter. The entire row is
            // already leaving the visual tree, so preserving the internal subtree avoids removing
            // and re-adding the same controls (and reapplying their styles) on reattachment.
            RecycleDeferredCells();
            base.OnDetachedFromVisualTree(e);
        }

        private void RebindRealizedCells()
        {
            var items = Items;
            var elements = RealizedElements;
            var firstIndex = FirstIndex;

            if (items is null || _rows is null || ElementFactory is null ||
                firstIndex < 0 || firstIndex + elements.Count > items.Count)
            {
                RecycleAllElements(
                    preserveVisualTreeMembership: true,
                    preserveLogicalTreeMembership: true);
                return;
            }

            for (var i = 0; i < elements.Count; ++i)
            {
                if (elements[i] is not TreeDataGridCell cell)
                    continue;

                var columnIndex = firstIndex + i;
                var oldColumnIndex = cell.ColumnIndex;
                var oldRowIndex = cell.RowIndex;
                var model = cell.Model!;

                cell.Unrealize();
                ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(cell, cell.RowIndex));

                if (_rows is IReusableCellRows reusableRows &&
                    reusableRows.TryReuseCell(items[columnIndex], model, RowIndex))
                {
                    cell.Realize(ElementFactory, GetSelection(), model, columnIndex, RowIndex);
                }
                else
                {
                    _rows.UnrealizeCell(model, oldColumnIndex, oldRowIndex);
                    model = _rows.RealizeCell(items[columnIndex], columnIndex, RowIndex);
                    cell.Realize(ElementFactory, GetSelection(), model, columnIndex, RowIndex);
                }

                ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(cell, columnIndex));
            }
        }

        private void RecycleDeferredCells()
        {
            if (!_rebindRealizedCells)
                return;

            _rebindRealizedCells = false;
            RecycleAllElements(
                preserveVisualTreeMembership: true,
                preserveLogicalTreeMembership: true);
        }

        private ITreeDataGridSelectionInteraction? GetSelection()
        {
            return this.FindAncestorOfType<TreeDataGrid>()?.SelectionInteraction;
        }

        public int GetChildIndex(ILogical child)
        {
            if (child is TreeDataGridCell cell)
            {
                return cell.ColumnIndex;
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
