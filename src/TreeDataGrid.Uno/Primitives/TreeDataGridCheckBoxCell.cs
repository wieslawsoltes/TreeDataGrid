using System.ComponentModel;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Models.TreeDataGrid;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Avalonia.Controls.Primitives
{
    public class TreeDataGridCheckBoxCell : TreeDataGridCell
    {
        private CheckBox? _checkBox;
        private bool _isUpdating;
        private bool _isReadOnly;
        private bool _isThreeState;
        private bool? _value;

        public TreeDataGridCheckBoxCell()
        {
            DefaultStyleKey = typeof(TreeDataGridCheckBoxCell);
        }

        public override void Realize(TreeDataGrid owner, TreeDataGridElementFactory factory, ICell model, int columnIndex, int rowIndex)
        {
            if (model is not CheckBoxCell checkBoxCell)
                throw new System.InvalidOperationException("TreeDataGridCheckBoxCell requires a CheckBoxCell model.");

            _isReadOnly = checkBoxCell.IsReadOnly;
            _isThreeState = checkBoxCell.IsThreeState;
            _value = checkBoxCell.Value;

            base.Realize(owner, factory, model, columnIndex, rowIndex);
            SyncVisuals();
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_checkBox is not null)
            {
                _checkBox.Checked -= OnCheckBoxChanged;
                _checkBox.Unchecked -= OnCheckBoxChanged;
                _checkBox.Indeterminate -= OnCheckBoxChanged;
            }

            _checkBox = GetTemplateChild("PART_CheckBox") as CheckBox;

            if (_checkBox is not null)
            {
                _checkBox.Checked += OnCheckBoxChanged;
                _checkBox.Unchecked += OnCheckBoxChanged;
                _checkBox.Indeterminate += OnCheckBoxChanged;
            }

            SyncVisuals();
        }

        protected override void OnModelAttached()
        {
            if (Model is INotifyPropertyChanged inpc)
                inpc.PropertyChanged += OnModelPropertyChanged;
        }

        protected override void OnModelDetached()
        {
            if (Model is INotifyPropertyChanged inpc)
                inpc.PropertyChanged -= OnModelPropertyChanged;
        }

        protected override void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (Model is not CheckBoxCell checkBoxCell)
                return;

            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(CheckBoxCell.Value))
            {
                _value = checkBoxCell.Value;
                SyncVisuals();
            }
        }

        public override double MeasureDesiredWidth()
        {
            return System.Math.Max(40, base.MeasureDesiredWidth());
        }

        protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        {
            return new TreeDataGridCheckBoxCellAutomationPeer(this);
        }

        private void SyncVisuals()
        {
            if (_checkBox is null)
                return;

            _isUpdating = true;
            _checkBox.IsEnabled = !_isReadOnly;
            _checkBox.IsThreeState = _isThreeState;
            _checkBox.IsChecked = _value;
            _isUpdating = false;
        }

        private void OnCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _checkBox is null || Model is not CheckBoxCell checkBoxCell)
                return;

            _value = _checkBox.IsChecked;
            checkBoxCell.Value = _value;
            Owner?.RaiseCellValueChanged(this, ColumnIndex, RowIndex);
        }
    }
}
