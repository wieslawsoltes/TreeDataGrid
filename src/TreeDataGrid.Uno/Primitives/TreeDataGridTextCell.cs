using System.ComponentModel;
using Uno.Controls.Models.TreeDataGrid;
using AvaloniaTextAlignment = Avalonia.Media.TextAlignment;
using AvaloniaTextTrimming = Avalonia.Media.TextTrimming;
using AvaloniaTextWrapping = Avalonia.Media.TextWrapping;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Uno.Controls.Primitives
{
    public class TreeDataGridTextCell : TreeDataGridCell
    {
        private TextBlock? _displayText;
        private TextBox? _editText;
        private bool _isUpdating;
        private string? _value;
        private AvaloniaTextAlignment _textAlignment = AvaloniaTextAlignment.Left;
        private AvaloniaTextWrapping _textWrapping = AvaloniaTextWrapping.NoWrap;
        private AvaloniaTextTrimming _textTrimming = AvaloniaTextTrimming.CharacterEllipsis;

        public TreeDataGridTextCell()
        {
            DefaultStyleKey = typeof(TreeDataGridTextCell);
        }

        public override void Realize(TreeDataGrid owner, TreeDataGridElementFactory factory, ICell model, int columnIndex, int rowIndex)
        {
            if (model is ITextCell textCell)
            {
                _value = textCell.Text;
                _textAlignment = textCell.TextAlignment;
                _textWrapping = textCell.TextWrapping;
                _textTrimming = textCell.TextTrimming;
            }
            else
            {
                _value = model.Value?.ToString();
                _textAlignment = AvaloniaTextAlignment.Left;
                _textWrapping = AvaloniaTextWrapping.NoWrap;
                _textTrimming = AvaloniaTextTrimming.CharacterEllipsis;
            }

            base.Realize(owner, factory, model, columnIndex, rowIndex);
            SyncVisuals();
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_editText is not null)
            {
                _editText.TextChanged -= OnEditTextChanged;
                _editText.LostFocus -= OnEditLostFocus;
                _editText.KeyDown -= OnEditKeyDown;
            }

            _displayText = GetTemplateChild("PART_DisplayText") as TextBlock;
            _editText = GetTemplateChild("PART_EditText") as TextBox;

            if (_editText is not null)
            {
                _editText.TextChanged += OnEditTextChanged;
                _editText.LostFocus += OnEditLostFocus;
                _editText.KeyDown += OnEditKeyDown;
            }

            SyncVisuals();
            UpdateEditingState();
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
            if (e.PropertyName == nameof(ITextCell.Text) || string.IsNullOrEmpty(e.PropertyName))
                UpdateValueFromModel();
        }

        protected override void UpdateValueFromModel()
        {
            _value = (Model as ITextCell)?.Text ?? Model?.Value?.ToString();
            SyncVisuals();
        }

        protected override void UpdateEditingState()
        {
            if (_displayText is not null)
                _displayText.Visibility = IsEditing ? Visibility.Collapsed : Visibility.Visible;

            if (_editText is not null)
            {
                _editText.Visibility = IsEditing ? Visibility.Visible : Visibility.Collapsed;

                if (IsEditing)
                {
                    _editText.Focus(FocusState.Programmatic);
                    _editText.SelectAll();
                }
            }
        }

        public override double MeasureDesiredWidth()
        {
            return System.Math.Max(64, base.MeasureDesiredWidth());
        }

        private void SyncVisuals()
        {
            if (_displayText is not null)
            {
                _displayText.Text = _value ?? string.Empty;
                _displayText.TextAlignment = ToTextAlignment(_textAlignment);
                _displayText.TextWrapping = ToTextWrapping(_textWrapping);
                _displayText.TextTrimming = ToTextTrimming(_textTrimming);
            }

            if (_editText is not null)
            {
                _isUpdating = true;
                _editText.Text = _value ?? string.Empty;
                _editText.TextAlignment = ToTextAlignment(_textAlignment);
                _editText.TextWrapping = ToTextWrapping(_textWrapping);
                _isUpdating = false;
            }
        }

        private void OnEditTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || Model is not ITextCell textCell || _editText is null)
                return;

            _value = _editText.Text;
            textCell.Text = _value;
            if (_displayText is not null)
                _displayText.Text = _value ?? string.Empty;
        }

        private void OnEditLostFocus(object sender, RoutedEventArgs e)
        {
            if (IsEditing)
                EndEdit();
        }

        private void OnEditKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                EndEdit();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
        }

        private static Microsoft.UI.Xaml.TextAlignment ToTextAlignment(AvaloniaTextAlignment alignment)
        {
            return alignment switch
            {
                AvaloniaTextAlignment.Center => Microsoft.UI.Xaml.TextAlignment.Center,
                AvaloniaTextAlignment.Right => Microsoft.UI.Xaml.TextAlignment.Right,
                AvaloniaTextAlignment.Justify => Microsoft.UI.Xaml.TextAlignment.Justify,
                _ => Microsoft.UI.Xaml.TextAlignment.Left,
            };
        }

        private static Microsoft.UI.Xaml.TextWrapping ToTextWrapping(AvaloniaTextWrapping wrapping)
        {
            return wrapping == AvaloniaTextWrapping.NoWrap
                ? Microsoft.UI.Xaml.TextWrapping.NoWrap
                : Microsoft.UI.Xaml.TextWrapping.Wrap;
        }

        private static Microsoft.UI.Xaml.TextTrimming ToTextTrimming(AvaloniaTextTrimming trimming)
        {
            return trimming == AvaloniaTextTrimming.None
                ? Microsoft.UI.Xaml.TextTrimming.None
                : Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;
        }
    }
}
