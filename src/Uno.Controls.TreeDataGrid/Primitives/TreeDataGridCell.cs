using System;
using System.ComponentModel;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Models.TreeDataGrid;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinPoint = Windows.Foundation.Point;
using WinRect = Windows.Foundation.Rect;
using WinSize = Windows.Foundation.Size;
using Windows.System;

namespace Avalonia.Controls.Primitives
{
    public abstract class TreeDataGridCell : Control, ITreeDataGridCell
    {
        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(
                nameof(IsSelected),
                typeof(bool),
                typeof(TreeDataGridCell),
                new PropertyMetadata(false, OnIsSelectedChanged));

        private WinPoint _pressedPoint = new(double.NaN, double.NaN);
        private bool _isEditing;

        protected TreeDataGridCell()
        {
            DefaultStyleKey = typeof(TreeDataGridCell);
            IsTabStop = true;
            DoubleTapped += OnCellDoubleTapped;
            PointerPressed += OnCellPointerPressed;
            PointerReleased += OnCellPointerReleased;
            KeyDown += OnCellKeyDown;
        }

        public int ColumnIndex { get; private set; } = -1;
        public int RowIndex { get; private set; } = -1;
        public ICell? Model { get; private set; }
        public bool IsEditing => _isEditing;
        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            private set => SetValue(IsSelectedProperty, value);
        }

        protected TreeDataGrid? Owner { get; private set; }

        public virtual void Realize(TreeDataGrid owner, TreeDataGridElementFactory factory, ICell model, int columnIndex, int rowIndex)
        {
            Owner = owner;
            Model = model;
            ColumnIndex = columnIndex;
            RowIndex = rowIndex;
            ApplyTemplate();
            UpdateSelection();
            OnModelAttached();
            InvalidateMeasure();
            InvalidateArrange();
            Owner.RaiseCellPrepared(this, columnIndex, rowIndex);
        }

        public virtual void Unrealize()
        {
            if (Owner is not null && ColumnIndex >= 0 && RowIndex >= 0)
                Owner.RaiseCellClearing(this, ColumnIndex, RowIndex);

            OnModelDetached();
            Owner = null;
            Model = null;
            ColumnIndex = -1;
            RowIndex = -1;
            IsSelected = false;
            _isEditing = false;
            UpdateEditingState();
        }

        public virtual double MeasureDesiredWidth()
        {
            ApplyTemplate();
            Measure(new WinSize(double.PositiveInfinity, double.PositiveInfinity));
            return Math.Max(48, DesiredSize.Width);
        }

        public void UpdateRowIndex(int rowIndex)
        {
            RowIndex = rowIndex;
            UpdateSelection();
        }

        public void UpdateSelection()
        {
            IsSelected = Owner?.IsCellSelected(RowIndex, ColumnIndex) ?? false;
            UpdateSelectionState();
        }

        protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        {
            return new TreeDataGridCellAutomationPeer(this);
        }

        protected void BeginEdit()
        {
            if (_isEditing || Model?.CanEdit != true)
                return;

            _isEditing = true;
            (Model as IEditableObject)?.BeginEdit();
            UpdateEditingState();
        }

        protected void CancelEdit()
        {
            if (!EndEditCore())
                return;

            (Model as IEditableObject)?.CancelEdit();
            UpdateValueFromModel();
        }

        protected void EndEdit()
        {
            if (!EndEditCore())
                return;

            (Model as IEditableObject)?.EndEdit();
            UpdateValueFromModel();
            if (Owner is not null && ColumnIndex >= 0 && RowIndex >= 0)
                Owner.RaiseCellValueChanged(this, ColumnIndex, RowIndex);
        }

        protected virtual void UpdateSelectionState()
        {
            Background = Owner?.GetCellBackground(RowIndex, ColumnIndex);
        }

        protected virtual void UpdateEditingState()
        {
        }

        protected virtual void UpdateValueFromModel()
        {
        }

        protected virtual void OnModelAttached()
        {
        }

        protected virtual void OnModelDetached()
        {
        }

        protected virtual void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
        }

        protected virtual bool IsEffectivelySelected()
        {
            return IsSelected || Owner?.IsRowSelected(RowIndex) == true;
        }

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeDataGridCell)d).UpdateSelectionState();
        }

        private void OnCellDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (Model is null || _isEditing || !Model.CanEdit)
                return;

            if (IsEnabledEditGesture(BeginEditGestures.DoubleTap, Model.EditGestures))
            {
                BeginEdit();
                e.Handled = true;
            }
        }

        private void OnCellPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (Model is null || _isEditing || !Model.CanEdit || !IsEnabledEditGesture(BeginEditGestures.Tap, Model.EditGestures))
            {
                _pressedPoint = new WinPoint(double.NaN, double.NaN);
                return;
            }

            _pressedPoint = e.GetCurrentPoint(this).Position;
        }

        private void OnCellPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (Model is null || _isEditing || !Model.CanEdit || !IsEnabledEditGesture(BeginEditGestures.Tap, Model.EditGestures) || double.IsNaN(_pressedPoint.X))
                return;

            var point = e.GetCurrentPoint(this).Position;
            var tapRect = new WinRect(_pressedPoint.X, _pressedPoint.Y, 4, 4);

            if (point.X >= tapRect.X && point.X <= tapRect.X + tapRect.Width && point.Y >= tapRect.Y && point.Y <= tapRect.Y + tapRect.Height)
            {
                BeginEdit();
                e.Handled = true;
            }

            _pressedPoint = new WinPoint(double.NaN, double.NaN);
        }

        private void OnCellKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (Model is null)
                return;

            if (!_isEditing && Model.CanEdit && e.Key == VirtualKey.F2 && IsEnabledEditGesture(BeginEditGestures.F2, Model.EditGestures))
            {
                BeginEdit();
                e.Handled = true;
            }
            else if (_isEditing && e.Key == VirtualKey.Enter)
            {
                EndEdit();
                e.Handled = true;
            }
            else if (_isEditing && e.Key == VirtualKey.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
        }

        private bool EndEditCore()
        {
            if (!_isEditing)
                return false;

            _isEditing = false;
            UpdateEditingState();
            Focus(FocusState.Programmatic);
            return true;
        }

        private bool IsEnabledEditGesture(BeginEditGestures gesture, BeginEditGestures enabledGestures)
        {
            if (!enabledGestures.HasFlag(gesture))
                return false;

            return enabledGestures.HasFlag(BeginEditGestures.WhenSelected)
                ? IsEffectivelySelected()
                : true;
        }
    }
}
