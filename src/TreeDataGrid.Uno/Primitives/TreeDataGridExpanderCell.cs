using System.ComponentModel;
using Avalonia.Controls.Models.TreeDataGrid;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Avalonia.Controls.Primitives
{
    public class TreeDataGridExpanderCell : TreeDataGridCell
    {
        private static readonly Thickness s_zeroThickness = new(0);
        private Border? _contentHost;
        private FrameworkElement? _indentSpacer;
        private Microsoft.UI.Xaml.Controls.Primitives.ToggleButton? _toggleButton;
        private TextBlock? _glyphText;
        private TreeDataGridElementFactory? _factory;
        private IExpanderCell? _expanderModel;
        private TreeDataGridCell? _innerCell;
        private bool _isUpdatingToggle;

        public TreeDataGridExpanderCell()
        {
            DefaultStyleKey = typeof(TreeDataGridExpanderCell);
        }

        public override void Realize(TreeDataGrid owner, TreeDataGridElementFactory factory, ICell model, int columnIndex, int rowIndex)
        {
            if (model is not IExpanderCell expanderCell)
                throw new System.InvalidOperationException("TreeDataGridExpanderCell requires an IExpanderCell model.");

            _factory = factory;
            _expanderModel = expanderCell;

            base.Realize(owner, factory, model, columnIndex, rowIndex);
            UpdateExpanderVisuals();
            UpdateContent();
        }

        public override void Unrealize()
        {
            ClearInnerCell();
            base.Unrealize();
            _expanderModel = null;
            _factory = null;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_toggleButton is not null)
            {
                _toggleButton.Click -= OnToggleButtonClick;
                _toggleButton.Tapped -= OnToggleButtonTapped;
            }

            _indentSpacer = GetTemplateChild("PART_IndentSpacer") as FrameworkElement;
            _toggleButton = GetTemplateChild("PART_ExpanderToggle") as Microsoft.UI.Xaml.Controls.Primitives.ToggleButton;
            _glyphText = GetTemplateChild("PART_ExpanderGlyph") as TextBlock;
            _contentHost = GetTemplateChild("PART_ContentHost") as Border;

            if (_toggleButton is not null)
            {
                _toggleButton.Click += OnToggleButtonClick;
                _toggleButton.Tapped += OnToggleButtonTapped;
            }

            UpdateExpanderVisuals();
            UpdateContent();
        }

        protected override void OnModelAttached()
        {
            if (_expanderModel is INotifyPropertyChanged inpc)
                inpc.PropertyChanged += OnModelPropertyChanged;
        }

        protected override void OnModelDetached()
        {
            if (_expanderModel is INotifyPropertyChanged inpc)
                inpc.PropertyChanged -= OnModelPropertyChanged;
        }

        protected override void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_expanderModel is null)
                return;

            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(IExpanderCell.IsExpanded) || e.PropertyName == nameof(IExpanderCell.ShowExpander))
                UpdateExpanderVisuals();
        }

        private void UpdateExpanderVisuals()
        {
            if (_expanderModel is null)
                return;

            if (_indentSpacer is not null)
            {
                var indentLevel = (_expanderModel.Row as IIndentedRow)?.Indent ?? 0;
                _indentSpacer.Width = indentLevel * (Owner?.IndentWidth ?? 20d);
            }

            if (_toggleButton is not null)
            {
                _toggleButton.Visibility = Visibility.Visible;
                _toggleButton.Opacity = _expanderModel.ShowExpander ? 1d : 0d;
                _toggleButton.IsHitTestVisible = _expanderModel.ShowExpander;
                _toggleButton.IsEnabled = _expanderModel.ShowExpander;
                _isUpdatingToggle = true;
                _toggleButton.IsChecked = _expanderModel.IsExpanded;
                _isUpdatingToggle = false;
            }

            if (_glyphText is not null)
            {
                _glyphText.Text = _expanderModel.IsExpanded
                    ? Owner?.GetThemeString(Avalonia.Controls.Themes.TreeDataGridThemeResources.ExpandedGlyphKey, "▾") ?? "▾"
                    : Owner?.GetThemeString(Avalonia.Controls.Themes.TreeDataGridThemeResources.CollapsedGlyphKey, "▸") ?? "▸";
            }
        }

        private void UpdateContent()
        {
            if (_contentHost is null || _factory is null || _expanderModel is null)
                return;

            if (_expanderModel.Content is not ICell innerModel)
            {
                ClearInnerCell();
                return;
            }

            if (_innerCell?.GetType() != GetExpectedCellType(innerModel))
                ClearInnerCell();

            if (_innerCell is null)
                _innerCell = (TreeDataGridCell)_factory.GetOrCreateElement(innerModel);

            if (!ReferenceEquals(_contentHost.Child, _innerCell))
                _contentHost.Child = _innerCell;

            _innerCell.Realize(Owner!, _factory, innerModel, ColumnIndex, RowIndex);
            ApplyInnerCellChrome();
            _innerCell.InvalidateMeasure();
            _innerCell.InvalidateArrange();
            _contentHost.InvalidateMeasure();
            _contentHost.InvalidateArrange();
            InvalidateMeasure();
            InvalidateArrange();
        }

        private void ClearInnerCell()
        {
            if (_innerCell is null || _factory is null)
                return;

            _innerCell.Unrealize();
            ResetInnerCellChrome(_innerCell);
            _factory.RecycleElement(_innerCell);
            _innerCell = null;

            if (_contentHost is not null)
                _contentHost.Child = null;
        }

        private void ApplyInnerCellChrome()
        {
            if (_innerCell is null)
                return;

            _innerCell.BorderThickness = s_zeroThickness;
            _innerCell.Padding = s_zeroThickness;
            _innerCell.BorderBrush = null;
            _innerCell.Background = null;
        }

        private static void ResetInnerCellChrome(TreeDataGridCell innerCell)
        {
            innerCell.ClearValue(Control.BorderThicknessProperty);
            innerCell.ClearValue(Control.PaddingProperty);
            innerCell.ClearValue(Control.BorderBrushProperty);
            innerCell.ClearValue(Control.BackgroundProperty);
        }

        private void OnToggleButtonClick(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingToggle || _toggleButton is null || _expanderModel is null)
                return;

            _expanderModel.IsExpanded = _toggleButton.IsChecked == true;
            UpdateExpanderVisuals();
        }

        private void OnToggleButtonTapped(object sender, TappedRoutedEventArgs e)
        {
            _ = sender;
            e.Handled = true;
        }

        private static System.Type GetExpectedCellType(ICell innerModel)
        {
            return innerModel switch
            {
                CheckBoxCell => typeof(TreeDataGridCheckBoxCell),
                TemplateCell => typeof(TreeDataGridTemplateCell),
                IExpanderCell => typeof(TreeDataGridExpanderCell),
                _ => typeof(TreeDataGridTextCell),
            };
        }
    }
}
