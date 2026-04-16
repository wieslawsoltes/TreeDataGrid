using System.ComponentModel;
using Uno.Controls.Automation.Peers;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;

namespace Uno.Controls.Primitives
{
    public class TreeDataGridColumnHeader : Button
    {
        private Border? _rootBorder;
        private Path? _sortAscendingGlyph;
        private Path? _sortDescendingGlyph;
        private Microsoft.UI.Xaml.Controls.Primitives.Thumb? _resizer;
        private TreeDataGrid? _owner;
        private IColumns? _columns;
        private IColumn? _model;
        private bool _isPointerOver;
        private bool _isPressed;

        public TreeDataGridColumnHeader()
        {
            DefaultStyleKey = typeof(TreeDataGridColumnHeader);
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            PointerEntered += (_, _) => { _isPointerOver = true; UpdateVisualState(); };
            PointerExited += (_, _) => { _isPointerOver = false; _isPressed = false; UpdateVisualState(); };
            PointerPressed += (_, _) => { _isPressed = true; UpdateVisualState(); };
            PointerReleased += (_, _) => { _isPressed = false; UpdateVisualState(); };
        }

        public int ColumnIndex { get; private set; } = -1;

        public object? Header
        {
            get => Content;
            private set => Content = value;
        }

        public bool CanUserResize { get; private set; }
        public ListSortDirection? SortDirection { get; private set; }
        public global::Avalonia.Rect Bounds => new(0, 0, ActualWidth, ActualHeight);

        public void Realize(TreeDataGrid owner, IColumns columns, int columnIndex)
        {
            if (_model is INotifyPropertyChanged oldInpc)
                oldInpc.PropertyChanged -= OnModelPropertyChanged;

            _owner = owner;
            _columns = columns;
            ColumnIndex = columnIndex;
            _model = columns[columnIndex];

            if (_model is INotifyPropertyChanged newInpc)
                newInpc.PropertyChanged += OnModelPropertyChanged;

            UpdatePropertiesFromModel();
            UpdateVisualState();
        }

        public void Unrealize()
        {
            if (_model is INotifyPropertyChanged oldInpc)
                oldInpc.PropertyChanged -= OnModelPropertyChanged;

            _model = null;
            _columns = null;
            _owner = null;
            ColumnIndex = -1;
            Header = null;
            SortDirection = null;
            CanUserResize = false;
            UpdateVisualState();
        }

        public double MeasureDesiredWidth()
        {
            ApplyTemplate();
            Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            return System.Math.Max(56, DesiredSize.Width);
        }

        protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        {
            return new TreeDataGridColumnHeaderAutomationPeer(this);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_resizer is not null)
            {
                _resizer.DragDelta -= OnResizerDragDelta;
                _resizer.DoubleTapped -= OnResizerDoubleTapped;
            }

            _rootBorder = GetTemplateChild("PART_RootBorder") as Border;
            _sortAscendingGlyph = GetTemplateChild("PART_SortAscendingGlyph") as Path;
            _sortDescendingGlyph = GetTemplateChild("PART_SortDescendingGlyph") as Path;
            _resizer = GetTemplateChild("PART_Resizer") as Microsoft.UI.Xaml.Controls.Primitives.Thumb;

            if (_resizer is not null)
            {
                _resizer.DragDelta += OnResizerDragDelta;
                _resizer.DoubleTapped += OnResizerDoubleTapped;
            }

            UpdateVisualState();
        }

        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName == nameof(IColumn.CanUserResize) ||
                e.PropertyName == nameof(IColumn.Header) ||
                e.PropertyName == nameof(IColumn.SortDirection))
            {
                UpdatePropertiesFromModel();
                UpdateVisualState();
            }
        }

        private void UpdatePropertiesFromModel()
        {
            Header = _model?.Header;
            SortDirection = _model?.SortDirection;
            CanUserResize = _model?.CanUserResize ?? _owner?.CanUserResizeColumns ?? false;
        }

        private void UpdateVisualState()
        {
            if (_owner is null)
                return;

            var backgroundKey = _isPressed
                ? TreeDataGridThemeResources.HeaderBackgroundPressedBrushKey
                : _isPointerOver
                    ? TreeDataGridThemeResources.HeaderBackgroundPointerOverBrushKey
                    : TreeDataGridThemeResources.HeaderBackgroundBrushKey;
            var borderKey = _isPressed
                ? TreeDataGridThemeResources.HeaderBorderBrushPressedBrushKey
                : _isPointerOver
                    ? TreeDataGridThemeResources.HeaderBorderBrushPointerOverBrushKey
                    : TreeDataGridThemeResources.HeaderBorderBrushKey;
            var foregroundKey = _isPressed
                ? TreeDataGridThemeResources.HeaderForegroundPressedBrushKey
                : _isPointerOver
                    ? TreeDataGridThemeResources.HeaderForegroundPointerOverBrushKey
                    : TreeDataGridThemeResources.HeaderForegroundBrushKey;

            Background = _owner.GetThemeBrush(backgroundKey);
            BorderBrush = _owner.GetThemeBrush(borderKey);
            Foreground = _owner.GetThemeBrush(foregroundKey);

            if (_rootBorder is not null)
            {
                _rootBorder.Background = Background;
                _rootBorder.BorderBrush = BorderBrush;
            }

            if (_sortAscendingGlyph is not null)
                _sortAscendingGlyph.Visibility = SortDirection == ListSortDirection.Ascending ? Visibility.Visible : Visibility.Collapsed;

            if (_sortDescendingGlyph is not null)
                _sortDescendingGlyph.Visibility = SortDirection == ListSortDirection.Descending ? Visibility.Visible : Visibility.Collapsed;

            if (_resizer is not null)
                _resizer.Visibility = CanUserResize ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnResizerDragDelta(object sender, Microsoft.UI.Xaml.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (_columns is not { } columns || _model is not { } model || e.HorizontalChange == 0)
                return;

            var pixelWidth = model.Width.IsAbsolute ? model.Width.Value : ActualWidth;
            if (double.IsNaN(pixelWidth) || double.IsInfinity(pixelWidth))
                return;

            var width = System.Math.Max(24, pixelWidth + e.HorizontalChange);
            columns.SetColumnWidth(ColumnIndex, new global::Avalonia.GridLength(width, global::Avalonia.GridUnitType.Pixel));
        }

        private void OnResizerDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            _columns?.SetColumnWidth(ColumnIndex, global::Avalonia.GridLength.Auto);
            e.Handled = true;
        }
    }
}
