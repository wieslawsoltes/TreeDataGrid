using System;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Internal;
using Avalonia.Controls.Models.TreeDataGrid;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using WinPoint = Windows.Foundation.Point;

namespace Avalonia.Controls.Primitives
{
    public class TreeDataGridRow : Control
    {
        private const double DragDistance = 3;
        private static readonly WinPoint s_invalidPoint = new(double.NaN, double.NaN);
        private TreeDataGrid? _owner;
        private TreeDataGridElementFactory? _elementFactory;
        private IColumns? _columns;
        private IRows? _rows;
        private System.Collections.Generic.IReadOnlyList<double>? _columnWidths;
        private WinPoint _dragStartPoint = s_invalidPoint;
        private bool _canStartDrag;

        public TreeDataGridRow()
        {
            DefaultStyleKey = typeof(TreeDataGridRow);
            Tapped += OnRowTapped;
            DragStarting += OnRowDragStarting;
            DropCompleted += OnRowDropCompleted;
            // WinUI drag initiation is source-element specific, so listen to handled child input too.
            AddHandler(PointerPressedEvent, new PointerEventHandler(OnRowPointerPressed), true);
            AddHandler(PointerMovedEvent, new PointerEventHandler(OnRowPointerMoved), true);
            AddHandler(PointerReleasedEvent, new PointerEventHandler(OnRowPointerReleased), true);
            PointerCaptureLost += OnRowPointerCaptureLost;
        }

        public TreeDataGridCellsPresenter? CellsPresenter { get; private set; }
        public int RowIndex { get; private set; } = -1;
        public global::Avalonia.Controls.IndexPath ModelIndex { get; private set; }
        public bool IsSelected { get; private set; }
        public object? Model => DataContext;
        public IRows? Rows => _rows;

        public void Realize(TreeDataGrid owner, TreeDataGridElementFactory elementFactory, IColumns columns, IRows rows, int rowIndex)
        {
            _owner = owner;
            _elementFactory = elementFactory;
            _columns = columns;
            _rows = rows;
            RowIndex = rowIndex;
            DataContext = rows[rowIndex].Model;
            ModelIndex = owner.GetModelIndex(rows[rowIndex], rowIndex);
            UpdateSelection();
            UpdateDragState(owner.CanStartRowDrag);
            ApplyTemplate();
            CellsPresenter?.Realize(rowIndex);
            if (_columnWidths is not null)
                CellsPresenter?.ApplyColumnWidths(_columnWidths);
            InvalidateMeasure();
            InvalidateArrange();
            _owner.RaiseRowPrepared(this, rowIndex);
        }

        public void Unrealize()
        {
            if (_owner is not null && RowIndex >= 0)
                _owner.RaiseRowClearing(this, RowIndex);

            CellsPresenter?.Unrealize();
            DataContext = null;
            RowIndex = -1;
            _owner = null;
            _elementFactory = null;
            _columns = null;
            _rows = null;
            _columnWidths = null;
            IsSelected = false;
            _canStartDrag = false;
            ResetDragTracking();
            CanDrag = false;
        }

        public void ApplyColumnWidths(System.Collections.Generic.IReadOnlyList<double> widths)
        {
            _columnWidths = widths;
            CellsPresenter?.ApplyColumnWidths(widths);
        }

        public void UpdateSelection()
        {
            IsSelected = _owner?.IsRowSelected(RowIndex) ?? false;
            Background = _owner?.GetRowBackground(RowIndex, ModelIndex);
            CellsPresenter?.UpdateSelection();
        }

        public void UpdateDragState(bool canDrag)
        {
            _canStartDrag = canDrag;
            ResetDragTracking();
            CanDrag = false;
        }

        public TreeDataGridCell? TryGetCell(int columnIndex)
        {
            return CellsPresenter?.TryGetElement(columnIndex);
        }

        protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        {
            return new TreeDataGridRowAutomationPeer(this);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            CellsPresenter = GetTemplateChild("PART_CellsPresenter") as TreeDataGridCellsPresenter;
            if (CellsPresenter is not null)
            {
                CellsPresenter.Owner = _owner;
                CellsPresenter.ElementFactory = _elementFactory;
                CellsPresenter.Items = _columns;
                CellsPresenter.Rows = _rows;

                if (_columnWidths is not null)
                    CellsPresenter.ApplyColumnWidths(_columnWidths);

                if (RowIndex >= 0)
                    CellsPresenter.Realize(RowIndex);

                if (_columnWidths is not null)
                    CellsPresenter.ApplyColumnWidths(_columnWidths);
            }
        }

        private void OnRowTapped(object sender, TappedRoutedEventArgs e)
        {
            if (IsInteractiveSource(e.OriginalSource as DependencyObject))
                return;

            _owner?.HandleRowTapped(this);
        }

        private void OnRowDragStarting(UIElement sender, DragStartingEventArgs args)
        {
            _owner?.HandleRowDragStarting(this, args);
        }

        private void OnRowDropCompleted(UIElement sender, DropCompletedEventArgs args)
        {
            _owner?.HandleRowDropCompleted(this);
        }

        private void OnRowPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _ = sender;

            if (!_canStartDrag || IsInteractiveSource(e.OriginalSource as DependencyObject))
            {
                ResetDragTracking();
                return;
            }

            _dragStartPoint = e.GetCurrentPoint(this).Position;
        }

        private void OnRowPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            _ = sender;

            if (!_canStartDrag ||
                double.IsNaN(_dragStartPoint.X) ||
                IsInteractiveSource(e.OriginalSource as DependencyObject))
            {
                return;
            }

            var currentPoint = e.GetCurrentPoint(this);
            var deltaX = Math.Abs(currentPoint.Position.X - _dragStartPoint.X);
            var deltaY = Math.Abs(currentPoint.Position.Y - _dragStartPoint.Y);
            var pointerSupportsDrag = e.Pointer.PointerDeviceType switch
            {
                PointerDeviceType.Mouse => currentPoint.Properties.IsLeftButtonPressed,
                PointerDeviceType.Pen => currentPoint.Properties.IsRightButtonPressed,
                _ => false,
            };

            if (!pointerSupportsDrag ||
                deltaX < DragDistance && deltaY < DragDistance)
            {
                return;
            }

            ResetDragTracking();
            _ = StartDragAsync(currentPoint);
        }

        private void OnRowPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _ = sender;
            _ = e;
            ResetDragTracking();
        }

        private void OnRowPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _ = sender;
            _ = e;
            ResetDragTracking();
        }

        private void ResetDragTracking()
        {
            _dragStartPoint = s_invalidPoint;
        }

        private static bool IsInteractiveSource(DependencyObject? source)
        {
            return VisualTreeHelpers.FindAncestorOrSelf<ToggleButton>(source) is not null ||
                VisualTreeHelpers.FindAncestorOrSelf<TextBox>(source) is not null ||
                VisualTreeHelpers.FindAncestorOrSelf<TreeDataGridCell>(source)?.IsEditing == true;
        }
    }
}
