#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Internal;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Themes;
using Avalonia.Input;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;
using Brush = Microsoft.UI.Xaml.Media.Brush;

namespace Avalonia.Controls
{
    [TemplatePart(Name = "PART_HeaderScrollViewer", Type = typeof(ScrollViewer))]
    [TemplatePart(Name = "PART_ColumnHeadersPresenter", Type = typeof(TreeDataGridColumnHeadersPresenter))]
    [TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
    [TemplatePart(Name = "PART_RowsPresenter", Type = typeof(TreeDataGridRowsPresenter))]
    [TemplatePart(Name = "PART_DragIndicatorHost", Type = typeof(Canvas))]
    [TemplatePart(Name = "PART_DragIndicatorLine", Type = typeof(Border))]
    [TemplatePart(Name = "PART_DragIndicatorBox", Type = typeof(Border))]
    public class TreeDataGrid : Control
    {
        public static readonly DependencyProperty AutoDragDropRowsProperty =
            DependencyProperty.Register(
                nameof(AutoDragDropRows),
                typeof(bool),
                typeof(TreeDataGrid),
                new PropertyMetadata(false, OnAutoDragDropRowsPropertyChanged));

        public static readonly DependencyProperty CanUserResizeColumnsProperty =
            DependencyProperty.Register(
                nameof(CanUserResizeColumns),
                typeof(bool),
                typeof(TreeDataGrid),
                new PropertyMetadata(true, OnLayoutPropertyChanged));

        public static readonly DependencyProperty CanUserSortColumnsProperty =
            DependencyProperty.Register(
                nameof(CanUserSortColumns),
                typeof(bool),
                typeof(TreeDataGrid),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowColumnHeadersProperty =
            DependencyProperty.Register(
                nameof(ShowColumnHeaders),
                typeof(bool),
                typeof(TreeDataGrid),
                new PropertyMetadata(true, OnLayoutPropertyChanged));

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(
                nameof(Source),
                typeof(ITreeDataGridSource),
                typeof(TreeDataGrid),
                new PropertyMetadata(null, OnSourcePropertyChanged));

        public static readonly DependencyProperty IndentWidthProperty =
            DependencyProperty.Register(
                nameof(IndentWidth),
                typeof(double),
                typeof(TreeDataGrid),
                new PropertyMetadata(20d, OnLayoutPropertyChanged));

        private const string RowDragDataKey = "Avalonia.Controls.TreeDataGrid.RowDragInfo";
        private const double DragIndicatorThickness = 2;
        private const double ScrollSyncTolerance = 0.5;
        private static readonly SolidColorBrush s_defaultBorderBrush = new(ColorHelper.FromArgb(0xFF, 0xD9, 0xD9, 0xD9));
        private static readonly SolidColorBrush s_transparentBrush = new(Colors.Transparent);

        private readonly CompositeDisposable _subscriptions = new();
        private readonly CompositeDisposable _selectionSubscriptions = new();
        private readonly CompositeDisposable _columnPropertySubscriptions = new();
        private bool _isRebuilding;
        private bool _needsRebuildAfterCurrentPass;
        private bool _isSyncingScroll;
        private bool _isUpdatingColumnWidths;
        private bool _columnWidthUpdateQueued;
        private bool _needsVisibleLayoutRefresh;
        private int _visibleLayoutRefreshAttempts;
        private ITreeDataGridSource? _source;
        private IColumns? _columns;
        private IRows? _rows;
        private ITreeDataGridRowSelectionModel? _rowSelection;
        private ScrollViewer? _headerScrollViewer;
        private ScrollViewer? _rowsScrollViewer;
        private TreeDataGridColumnHeadersPresenter? _columnHeadersPresenter;
        private TreeDataGridRowsPresenter? _rowsPresenter;
        private Canvas? _dragIndicatorHost;
        private Border? _dragIndicatorLine;
        private Border? _dragIndicatorBox;
        private TreeDataGridElementFactory _elementFactory = new();
        private EventHandler<TreeDataGridRowDragStartedEventArgs>? _rowDragStarted;
        private EventHandler<TreeDataGridRowDragEventArgs>? _rowDragOver;
        private EventHandler<TreeDataGridRowDragEventArgs>? _rowDrop;

        public TreeDataGrid()
        {
            DefaultStyleKey = typeof(TreeDataGrid);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnTreeDataGridSizeChanged;
            LayoutUpdated += OnLayoutUpdated;
            ActualThemeChanged += OnActualThemeChanged;
            DragOver += OnDragOver;
            DragLeave += OnDragLeave;
            Drop += OnDrop;
            AddHandler(KeyDownEvent, new KeyEventHandler(OnTreeDataGridKeyDown), true);
            AddHandler(PointerPressedEvent, new PointerEventHandler(OnTreeDataGridPointerPressed), true);
            AddHandler(PointerReleasedEvent, new PointerEventHandler(OnTreeDataGridPointerReleased), true);
            RegisterPropertyChangedCallback(UIElement.VisibilityProperty, OnVisibilityPropertyChanged);
        }

        public bool AutoDragDropRows
        {
            get => (bool)GetValue(AutoDragDropRowsProperty);
            set => SetValue(AutoDragDropRowsProperty, value);
        }

        public bool CanUserResizeColumns
        {
            get => (bool)GetValue(CanUserResizeColumnsProperty);
            set => SetValue(CanUserResizeColumnsProperty, value);
        }

        public bool CanUserSortColumns
        {
            get => (bool)GetValue(CanUserSortColumnsProperty);
            set => SetValue(CanUserSortColumnsProperty, value);
        }

        public bool ShowColumnHeaders
        {
            get => (bool)GetValue(ShowColumnHeadersProperty);
            set => SetValue(ShowColumnHeadersProperty, value);
        }

        public double IndentWidth
        {
            get => (double)GetValue(IndentWidthProperty);
            set => SetValue(IndentWidthProperty, value);
        }

        public ITreeDataGridSource? Source
        {
            get => (ITreeDataGridSource?)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public IColumns? Columns => _columns;
        public IRows? Rows => _rows;
        public ScrollViewer? Scroll => _rowsScrollViewer;
        public TreeDataGridColumnHeadersPresenter? ColumnHeadersPresenter => _columnHeadersPresenter;
        public TreeDataGridRowsPresenter? RowsPresenter => _rowsPresenter;
        public ITreeDataGridCellSelectionModel? ColumnSelection => Source?.Selection as ITreeDataGridCellSelectionModel;
        public ITreeDataGridRowSelectionModel? RowSelection => _rowSelection;

        public TreeDataGridElementFactory ElementFactory
        {
            get => _elementFactory;
            set
            {
                _elementFactory = value ?? throw new ArgumentNullException(nameof(value));
                ApplyTemplateState();
                Rebuild();
            }
        }

        public event EventHandler<TreeDataGridCellEventArgs>? CellClearing;
        public event EventHandler<TreeDataGridCellEventArgs>? CellPrepared;
        public event EventHandler<TreeDataGridCellEventArgs>? CellValueChanged;
        public event EventHandler<TreeDataGridRowEventArgs>? RowClearing;
        public event EventHandler<TreeDataGridRowEventArgs>? RowPrepared;
        public event EventHandler<TreeDataGridRowDragStartedEventArgs>? RowDragStarted
        {
            add
            {
                _rowDragStarted += value;
                ApplyDragDropState();
            }
            remove
            {
                _rowDragStarted -= value;
                ApplyDragDropState();
            }
        }

        public event EventHandler<TreeDataGridRowDragEventArgs>? RowDragOver
        {
            add
            {
                _rowDragOver += value;
                ApplyDragDropState();
            }
            remove
            {
                _rowDragOver -= value;
                ApplyDragDropState();
            }
        }

        public event EventHandler<TreeDataGridRowDragEventArgs>? RowDrop
        {
            add
            {
                _rowDrop += value;
                ApplyDragDropState();
            }
            remove
            {
                _rowDrop -= value;
                ApplyDragDropState();
            }
        }
        public event CancelEventHandler? SelectionChanging;

        internal bool CanStartRowDrag => _source is not null &&
            _rowSelection is not null &&
            (AutoDragDropRows || _rowDragStarted is not null || _rowDragOver is not null || _rowDrop is not null);

        protected override void OnApplyTemplate()
        {
            if (_headerScrollViewer is not null)
                _headerScrollViewer.ViewChanged -= OnHeaderScrollViewerViewChanged;
            if (_rowsScrollViewer is not null)
            {
                _rowsScrollViewer.ViewChanged -= OnRowsScrollViewerViewChanged;
                _rowsScrollViewer.SizeChanged -= OnRowsScrollViewerSizeChanged;
            }

            base.OnApplyTemplate();

            _headerScrollViewer = GetTemplateChild("PART_HeaderScrollViewer") as ScrollViewer;
            _columnHeadersPresenter = GetTemplateChild("PART_ColumnHeadersPresenter") as TreeDataGridColumnHeadersPresenter;
            _rowsScrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
            _rowsPresenter = GetTemplateChild("PART_RowsPresenter") as TreeDataGridRowsPresenter;
            _dragIndicatorHost = GetTemplateChild("PART_DragIndicatorHost") as Canvas;
            _dragIndicatorLine = GetTemplateChild("PART_DragIndicatorLine") as Border;
            _dragIndicatorBox = GetTemplateChild("PART_DragIndicatorBox") as Border;
            ApplyDragIndicatorBrushes();

            if (_headerScrollViewer is not null)
                _headerScrollViewer.ViewChanged += OnHeaderScrollViewerViewChanged;
            if (_rowsScrollViewer is not null)
            {
                _rowsScrollViewer.ViewChanged += OnRowsScrollViewerViewChanged;
                _rowsScrollViewer.SizeChanged += OnRowsScrollViewerSizeChanged;
            }

            HideDropIndicator();
            ApplyTemplateState();
            Rebuild();
            QueueVisibleLayoutRefresh();
            RequestColumnWidthsUpdate();
        }

        protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        {
            return new TreeDataGridAutomationPeer(this);
        }

        internal global::Avalonia.Controls.IndexPath GetModelIndex(IRow row, int rowIndex)
        {
            return row is IModelIndexableRow indexedRow
                ? indexedRow.ModelIndexPath
                : new global::Avalonia.Controls.IndexPath(rowIndex);
        }

        internal Brush GetRowBackground(int rowIndex, global::Avalonia.Controls.IndexPath modelIndex)
        {
            if (_rowSelection?.IsSelected(modelIndex) == true)
                return GetThemeBrush(TreeDataGridThemeResources.SelectedRowBackgroundBrushKey, s_transparentBrush);

            return rowIndex % 2 == 0
                ? GetThemeBrush(TreeDataGridThemeResources.RowBackgroundBrushKey, s_transparentBrush)
                : GetThemeBrush(TreeDataGridThemeResources.AlternateRowBackgroundBrushKey, s_transparentBrush);
        }

        internal Brush GetCellBackground(int rowIndex, int columnIndex)
        {
            return IsCellSelected(rowIndex, columnIndex)
                ? GetThemeBrush(TreeDataGridThemeResources.SelectedCellBackgroundBrushKey, s_transparentBrush)
                : s_transparentBrush;
        }

        internal bool IsRowSelected(int rowIndex)
        {
            return _rows is not null && rowIndex >= 0 && rowIndex < _rows.Count &&
                _rowSelection?.IsSelected(_rows.RowIndexToModelIndex(rowIndex)) == true;
        }

        internal bool IsCellSelected(int rowIndex, int columnIndex)
        {
            if (_rows is null || rowIndex < 0 || rowIndex >= _rows.Count)
                return false;

            var selection = ColumnSelection;
            if (selection is null)
                return false;

            var modelIndex = _rows.RowIndexToModelIndex(rowIndex);
            var method = selection.GetType().GetMethod(nameof(ITreeDataGridCellSelectionModel<object>.IsSelected), new[] { typeof(CellIndex) });
            if (method?.Invoke(selection, new object[] { new CellIndex(columnIndex, modelIndex) }) is bool result)
                return result;

            return false;
        }

        internal Brush GetThemeBrush(string key, Brush? fallback = null)
        {
            return TryGetResourceValue(key) as Brush ?? fallback ?? s_transparentBrush;
        }

        internal string GetThemeString(string key, string fallback)
        {
            return TryGetResourceValue(key) as string ?? fallback;
        }

        private void ApplyDragIndicatorBrushes()
        {
            var brush = Foreground ?? GetThemeBrush(TreeDataGridThemeResources.HeaderForegroundBrushKey, s_defaultBorderBrush);

            if (_dragIndicatorLine is not null)
                _dragIndicatorLine.Background = brush;

            if (_dragIndicatorBox is not null)
            {
                _dragIndicatorBox.Background = s_transparentBrush;
                _dragIndicatorBox.BorderBrush = brush;
            }
        }

        private void OnTreeDataGridKeyDown(object sender, KeyRoutedEventArgs e)
        {
            _ = sender;

            if (e.Handled || _source?.Selection is not ITreeDataGridSelectionInteraction selection)
                return;

            var args = e.ToAvalonia();
            selection.OnPreviewKeyDown(this, args);

            if (!args.Handled)
                selection.OnKeyDown(this, args);
        }

        private void OnTreeDataGridPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _ = sender;

            if (e.Handled || _source?.Selection is not ITreeDataGridSelectionInteraction selection)
                return;

            selection.OnPointerPressed(this, e.ToAvaloniaPressed());
        }

        private void OnTreeDataGridPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _ = sender;

            if (e.Handled || _source?.Selection is not ITreeDataGridSelectionInteraction selection)
                return;

            selection.OnPointerReleased(this, e.ToAvaloniaReleased());
        }

        internal void RaiseCellPrepared(TreeDataGridCell cell, int columnIndex, int rowIndex)
        {
            CellPrepared?.Invoke(this, new TreeDataGridCellEventArgs(cell, columnIndex, rowIndex));
        }

        internal void RaiseCellClearing(TreeDataGridCell cell, int columnIndex, int rowIndex)
        {
            CellClearing?.Invoke(this, new TreeDataGridCellEventArgs(cell, columnIndex, rowIndex));
        }

        internal void RaiseCellValueChanged(TreeDataGridCell cell, int columnIndex, int rowIndex)
        {
            CellValueChanged?.Invoke(this, new TreeDataGridCellEventArgs(cell, columnIndex, rowIndex));
        }

        internal void RaiseRowPrepared(TreeDataGridRow row, int rowIndex)
        {
            RowPrepared?.Invoke(this, new TreeDataGridRowEventArgs(row, rowIndex));
        }

        internal void RaiseRowClearing(TreeDataGridRow row, int rowIndex)
        {
            RowClearing?.Invoke(this, new TreeDataGridRowEventArgs(row, rowIndex));
        }

        public TreeDataGridCell? TryGetCell(int columnIndex, int rowIndex)
        {
            return TryGetRow(rowIndex)?.TryGetCell(columnIndex);
        }

        public TreeDataGridRow? TryGetRow(int rowIndex)
        {
            return RowsPresenter?.TryGetElement(rowIndex);
        }

        public bool TryGetCell(DependencyObject? element, [NotNullWhen(true)] out TreeDataGridCell? result)
        {
            result = VisualTreeHelpers.FindAncestorOrSelf<TreeDataGridCell>(element);
            if (result is not null && result.ColumnIndex >= 0 && result.RowIndex >= 0)
                return true;

            result = null;
            return false;
        }

        public bool TryGetRow(DependencyObject? element, [NotNullWhen(true)] out TreeDataGridRow? result)
        {
            result = VisualTreeHelpers.FindAncestorOrSelf<TreeDataGridRow>(element);
            if (result is not null && result.RowIndex >= 0)
                return true;

            result = null;
            return false;
        }

        public bool TryGetRowModel<TModel>(DependencyObject element, [NotNullWhen(true)] out TModel? result)
            where TModel : notnull
        {
            if (Source is not null &&
                TryGetRow(element, out var row) &&
                row.RowIndex < Source.Rows.Count &&
                Source.Rows[row.RowIndex] is IRow<TModel> rowWithModel)
            {
                result = rowWithModel.Model;
                return true;
            }

            result = default;
            return false;
        }

        public bool QueryCancelSelection()
        {
            if (SelectionChanging is null)
                return false;

            var e = new CancelEventArgs();
            SelectionChanging(this, e);
            return e.Cancel;
        }

        internal void HandleRowTapped(TreeDataGridRow row)
        {
            if (_rowSelection is null || QueryCancelSelection())
                return;

            UpdateRowSelection(row);
        }

        internal void HandleHeaderClick(TreeDataGridColumnHeader header)
        {
            if (_source is null || _columns is null || !CanUserSortColumns || header.ColumnIndex < 0 || header.ColumnIndex >= _columns.Count)
                return;

            var column = _columns[header.ColumnIndex];
            var direction = column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _source.SortBy(column, direction);
        }

        internal void HandleRowDragStarting(TreeDataGridRow row, DragStartingEventArgs e)
        {
            if (_source is null || _rowSelection is null)
            {
                e.Cancel = true;
                return;
            }

            if (!_rowSelection.IsSelected(row.ModelIndex))
            {
                if (QueryCancelSelection())
                {
                    e.Cancel = true;
                    return;
                }

                _rowSelection.SelectedIndex = row.ModelIndex;
            }

            var models = _rowSelection.SelectedItems.Where(x => x is not null).Cast<object>().ToArray();
            var args = new TreeDataGridRowDragStartedEventArgs(models)
            {
                AllowedEffects = AutoDragDropRows && !_source.IsSorted
                    ? Avalonia.Input.DragDropEffects.Move
                    : Avalonia.Input.DragDropEffects.None,
            };
            _rowDragStarted?.Invoke(this, args);

            if (args.AllowedEffects == Avalonia.Input.DragDropEffects.None)
            {
                e.Cancel = true;
                return;
            }

            e.Data.Properties[RowDragDataKey] = new RowDragInfo(_source, _rowSelection.SelectedIndexes.ToList());
            e.Data.SetText("TreeDataGridRowDrag");
            e.AllowedOperations = ToDataPackageOperation(args.AllowedEffects);
        }

        internal void HandleRowDropCompleted(TreeDataGridRow row)
        {
            _ = row;
        }

        private static void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeDataGrid)d).OnSourceChanged((ITreeDataGridSource?)e.OldValue, (ITreeDataGridSource?)e.NewValue);
        }

        private static void OnAutoDragDropRowsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeDataGrid)d).ApplyDragDropState();
        }

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (TreeDataGrid)d;
            control.ApplyHeaderVisibility();
            control.Rebuild();
        }

        private void OnSourceChanged(ITreeDataGridSource? oldValue, ITreeDataGridSource? newValue)
        {
            DisconnectSourceSubscriptions();
            ClearPresenters();

            _source = newValue;
            _columns = newValue?.Columns;
            _rows = newValue?.Rows;

            ConnectSourceSubscriptions();
            ApplyTemplateState();
            Rebuild();
        }

        private void ConnectSourceSubscriptions()
        {
            if (_source is not null)
            {
                _source.PropertyChanged += OnSourceModelPropertyChanged;
                _source.Sorted += OnSourceSorted;
                _subscriptions.Add(Disposable.Create(() =>
                {
                    _source.PropertyChanged -= OnSourceModelPropertyChanged;
                    _source.Sorted -= OnSourceSorted;
                }));
            }

            if (_columns is not null)
            {
                _columns.CollectionChanged += OnColumnsCollectionChanged;
                _columns.LayoutInvalidated += OnColumnsLayoutInvalidated;
                _subscriptions.Add(Disposable.Create(() =>
                {
                    _columns.CollectionChanged -= OnColumnsCollectionChanged;
                    _columns.LayoutInvalidated -= OnColumnsLayoutInvalidated;
                }));
                RefreshColumnPropertySubscriptions();
            }

            if (_rows is not null)
            {
                _rows.CollectionChanged += OnRowsCollectionChanged;
                _subscriptions.Add(Disposable.Create(() => _rows.CollectionChanged -= OnRowsCollectionChanged));
            }

            RebindSelection();
        }

        private void DisconnectSourceSubscriptions()
        {
            _subscriptions.Clear();
            _selectionSubscriptions.Clear();
            _columnPropertySubscriptions.Clear();
        }

        private void RebindSelection()
        {
            _selectionSubscriptions.Clear();
            _rowSelection = _source?.Selection as ITreeDataGridRowSelectionModel;
            var cellSelection = ColumnSelection;

            if (_rowSelection is not null)
            {
                _rowSelection.SelectionChanged += OnRowSelectionChanged;
                _rowSelection.IndexesChanged += OnRowIndexesChanged;
                _rowSelection.SourceReset += OnRowSelectionSourceReset;
                _rowSelection.PropertyChanged += OnRowSelectionPropertyChanged;
                _selectionSubscriptions.Add(Disposable.Create(() =>
                {
                    _rowSelection.SelectionChanged -= OnRowSelectionChanged;
                    _rowSelection.IndexesChanged -= OnRowIndexesChanged;
                    _rowSelection.SourceReset -= OnRowSelectionSourceReset;
                    _rowSelection.PropertyChanged -= OnRowSelectionPropertyChanged;
                }));
            }

            if (cellSelection is not null)
            {
                cellSelection.SelectionChanged += OnCellSelectionChanged;
                _selectionSubscriptions.Add(Disposable.Create(() => cellSelection.SelectionChanged -= OnCellSelectionChanged));
            }

            UpdateRowSelectionStates();
            UpdateCellSelectionStates();
            ApplyDragDropState();
        }

        private void RefreshColumnPropertySubscriptions()
        {
            _columnPropertySubscriptions.Clear();

            if (_columns is null)
                return;

            foreach (var column in _columns)
            {
                column.PropertyChanged += OnColumnPropertyChanged;
                _columnPropertySubscriptions.Add(Disposable.Create(() => column.PropertyChanged -= OnColumnPropertyChanged));
            }
        }

        private void ApplyTemplateState()
        {
            if (_columnHeadersPresenter is not null)
            {
                _columnHeadersPresenter.Owner = this;
                _columnHeadersPresenter.ElementFactory = ElementFactory;
                _columnHeadersPresenter.Items = _columns;
            }

            if (_rowsPresenter is not null)
            {
                _rowsPresenter.Owner = this;
                _rowsPresenter.ElementFactory = ElementFactory;
                _rowsPresenter.Columns = _columns;
                _rowsPresenter.Items = _rows;
            }

            ApplyHeaderVisibility();
            ApplyDragDropState();
        }

        private void Rebuild()
        {
            if (_isRebuilding || _columnHeadersPresenter is null || _rowsPresenter is null)
                return;

            do
            {
                _isRebuilding = true;
                _needsRebuildAfterCurrentPass = false;

                try
                {
                    ApplyHeaderVisibility();
                    ClearPresenters();

                    if (_source is null || _columns is null || _rows is null || _columns.Count == 0)
                        return;

                    _columnHeadersPresenter.Items = _columns;
                    _rowsPresenter.Columns = _columns;
                    _rowsPresenter.Items = _rows;

                _columnHeadersPresenter.Realize();
                _rowsPresenter.Realize();
                _rowsPresenter.UpdateViewport(
                    _rowsScrollViewer?.VerticalOffset ?? 0,
                    GetRowsViewportHeight());
                _rowsPresenter.UpdateLayout();
                UpdateColumnWidths();
                UpdateRowSelectionStates();
                UpdateCellSelectionStates();
                ApplyDragDropState();
                }
                finally
                {
                    _isRebuilding = false;
                }
            }
            while (_needsRebuildAfterCurrentPass && _columnHeadersPresenter is not null && _rowsPresenter is not null);
        }

        private void UpdateColumnWidths()
        {
            if (_isUpdatingColumnWidths ||
                _columns is null ||
                _rowsPresenter is null ||
                _columnHeadersPresenter is null ||
                _columnHeadersPresenter.RealizedHeaders.Count == 0)
                return;

            var viewportWidth = GetRowsViewportWidth();
            var viewportHeight = GetRowsViewportHeight();

            if (viewportWidth <= 0)
                return;

            _isUpdatingColumnWidths = true;
            try
            {
                _columns.ViewportChanged(new global::Avalonia.Rect(0, 0, viewportWidth, viewportHeight));

                for (var columnIndex = 0; columnIndex < _columnHeadersPresenter.RealizedHeaders.Count; ++columnIndex)
                {
                    var desiredWidth = _columnHeadersPresenter.RealizedHeaders[columnIndex].MeasureDesiredWidth();
                    _columns.CellMeasured(columnIndex, -1, new global::Avalonia.Size(desiredWidth, 32));
                }

                foreach (var row in _rowsPresenter.RealizedRows)
                {
                    if (row.RowIndex < 0 || row.RowIndex >= (_rows?.Count ?? 0))
                        continue;

                    var rowModel = _rows![row.RowIndex];
                    var rowHeight = rowModel.Height.IsAbsolute ? rowModel.Height.Value : 32d;

                    foreach (var cell in row.CellsPresenter?.RealizedCells ?? Array.Empty<TreeDataGridCell>())
                    {
                        _columns.CellMeasured(cell.ColumnIndex, row.RowIndex, new global::Avalonia.Size(cell.MeasureDesiredWidth(), rowHeight));
                    }
                }

                _columns.CommitActualWidths();

                var widths = new double[_columns.Count];
                for (var columnIndex = 0; columnIndex < _columns.Count; ++columnIndex)
                {
                    var column = _columns[columnIndex];
                    var width = !double.IsNaN(column.ActualWidth) && column.ActualWidth > 0
                        ? column.ActualWidth
                        : column.Width.GridUnitType switch
                        {
                            global::Avalonia.GridUnitType.Pixel => column.Width.Value,
                            global::Avalonia.GridUnitType.Star => Math.Max(_columnHeadersPresenter.RealizedHeaders[columnIndex].MeasureDesiredWidth(), viewportWidth / Math.Max(1, _columns.Count)),
                            _ => _columnHeadersPresenter.RealizedHeaders[columnIndex].MeasureDesiredWidth(),
                        };
                    widths[columnIndex] = Math.Max(48, width);
                }

                _columnHeadersPresenter.ApplyColumnWidths(widths);
                _rowsPresenter.ApplyColumnWidths(widths);
            }
            finally
            {
                _isUpdatingColumnWidths = false;
            }
        }

        private void ClearPresenters()
        {
            _columnHeadersPresenter?.Unrealize();
            _rowsPresenter?.Unrealize();
        }

        private void ApplyHeaderVisibility()
        {
            if (_headerScrollViewer is not null)
                _headerScrollViewer.Visibility = ShowColumnHeaders ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateRowSelectionStates()
        {
            foreach (var row in _rowsPresenter?.RealizedRows ?? Array.Empty<TreeDataGridRow>())
                row.UpdateSelection();
        }

        private void UpdateCellSelectionStates()
        {
            foreach (var row in _rowsPresenter?.RealizedRows ?? Array.Empty<TreeDataGridRow>())
                row.CellsPresenter?.UpdateSelection();
        }

        private void ApplyDragDropState()
        {
            AllowDrop = AutoDragDropRows || _rowDragOver is not null || _rowDrop is not null;
            _rowsPresenter?.UpdateDragState(CanStartRowDrag);
        }

        private void UpdateRowSelection(TreeDataGridRow row)
        {
            if (_rowSelection is null)
                return;

            if (_rowSelection.SingleSelect)
            {
                _rowSelection.SelectedIndex = row.ModelIndex;
                return;
            }

            var ctrlDown = IsModifierKeyDown(VirtualKey.Control);
            var shiftDown = IsModifierKeyDown(VirtualKey.Shift);

            if (shiftDown)
            {
                SelectRowRange(row);
            }
            else if (ctrlDown)
            {
                if (_rowSelection.IsSelected(row.ModelIndex))
                    _rowSelection.Deselect(row.ModelIndex);
                else
                    _rowSelection.Select(row.ModelIndex);
            }
            else
            {
                _rowSelection.SelectedIndex = row.ModelIndex;
            }
        }

        private void SelectRowRange(TreeDataGridRow row)
        {
            if (_rowSelection is null)
                return;

            var anchorModelIndex = _rowSelection.RangeAnchorIndex != default
                ? _rowSelection.RangeAnchorIndex
                : _rowSelection.AnchorIndex != default
                    ? _rowSelection.AnchorIndex
                    : row.ModelIndex;
            var anchorRowIndex = FindDisplayRowIndex(anchorModelIndex);

            if (anchorRowIndex < 0 || _rowsPresenter is null)
            {
                _rowSelection.SelectedIndex = row.ModelIndex;
                return;
            }

            var start = Math.Min(anchorRowIndex, row.RowIndex);
            var end = Math.Max(anchorRowIndex, row.RowIndex);

            _rowSelection.BeginBatchUpdate();
            try
            {
                _rowSelection.Clear();
                for (var i = start; i <= end; ++i)
                    _rowSelection.Select(_rows!.RowIndexToModelIndex(i));

                _rowSelection.RangeAnchorIndex = anchorModelIndex;
                _rowSelection.AnchorIndex = row.ModelIndex;
            }
            finally
            {
                _rowSelection.EndBatchUpdate();
            }
        }

        private int FindDisplayRowIndex(global::Avalonia.Controls.IndexPath modelIndex)
        {
            return _rows?.ModelIndexToRowIndex(modelIndex) ?? -1;
        }

        private static bool IsModifierKeyDown(VirtualKey key)
        {
            return (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (!TryGetRowDragInfo(e.DataView, out _))
            {
                HideDropIndicator();
                e.AcceptedOperation = DataPackageOperation.None;
                return;
            }

            if (e.DragUIOverride is { } dragUi)
            {
                dragUi.Clear();
                dragUi.IsCaptionVisible = false;
                dragUi.IsContentVisible = false;
                dragUi.IsGlyphVisible = false;
            }

            var row = VisualTreeHelpers.FindAncestorOrSelf<TreeDataGridRow>(e.OriginalSource as DependencyObject);
            var autoDrop = CalculateAutoDrop(row, e, out _, out var position);
            var args = new TreeDataGridRowDragEventArgs(row, e)
            {
                Position = position,
                DragEffects = autoDrop ? Avalonia.Input.DragDropEffects.Move : Avalonia.Input.DragDropEffects.None,
            };
            _rowDragOver?.Invoke(this, args);

            if (row is not null)
                ShowDropIndicator(row, args.Position);
            else
                HideDropIndicator();

            e.AcceptedOperation = ToDataPackageOperation(args.DragEffects);
            e.Handled = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            HideDropIndicator();
            e.AcceptedOperation = DataPackageOperation.None;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            HideDropIndicator();
            var row = VisualTreeHelpers.FindAncestorOrSelf<TreeDataGridRow>(e.OriginalSource as DependencyObject);
            var autoDrop = CalculateAutoDrop(row, e, out var data, out var position);
            var args = new TreeDataGridRowDragEventArgs(row, e)
            {
                Position = position,
                DragEffects = autoDrop ? Avalonia.Input.DragDropEffects.Move : Avalonia.Input.DragDropEffects.None,
            };
            _rowDrop?.Invoke(this, args);

            e.AcceptedOperation = ToDataPackageOperation(args.DragEffects);
            e.Handled = true;

            if (!args.Handled &&
                autoDrop &&
                _source is not null &&
                row is not null &&
                data is not null &&
                args.Position != TreeDataGridRowDropPosition.None &&
                args.DragEffects != Avalonia.Input.DragDropEffects.None)
            {
                var targetIndex = _source.Rows.RowIndexToModelIndex(row.RowIndex);
                _source.DragDropRows(_source, data.Indexes, targetIndex, args.Position, args.DragEffects);
            }

        }

        private bool CalculateAutoDrop(
            TreeDataGridRow? targetRow,
            DragEventArgs e,
            [NotNullWhen(true)] out RowDragInfo? data,
            out TreeDataGridRowDropPosition position)
        {
            if (!AutoDragDropRows ||
                !TryGetRowDragInfo(e.DataView, out var dragInfo) ||
                _source is null ||
                _source.IsSorted ||
                targetRow is null ||
                dragInfo.Source != _source)
            {
                data = null;
                position = TreeDataGridRowDropPosition.None;
                return false;
            }

            var targetIndex = _source.Rows.RowIndexToModelIndex(targetRow.RowIndex);
            position = GetDropPosition(_source, e, targetRow);

            foreach (var sourceIndex in dragInfo.Indexes)
            {
                if (sourceIndex.IsAncestorOf(targetIndex) ||
                    (sourceIndex == targetIndex && position == TreeDataGridRowDropPosition.Inside))
                {
                    data = null;
                    position = TreeDataGridRowDropPosition.None;
                    return false;
                }
            }

            data = dragInfo;
            return true;
        }

        private static bool TryGetRowDragInfo(
            DataPackageView? dataView,
            [NotNullWhen(true)] out RowDragInfo? data)
        {
            if (dataView?.Properties is { } properties &&
                properties.TryGetValue(RowDragDataKey, out var value) &&
                value is RowDragInfo dragInfo)
            {
                data = dragInfo;
                return true;
            }

            data = null;
            return false;
        }

        private static TreeDataGridRowDropPosition GetDropPosition(
            ITreeDataGridSource source,
            DragEventArgs e,
            TreeDataGridRow row)
        {
            var rowHeight = Math.Max(1, row.ActualHeight);
            var rowY = e.GetPosition(row).Y / rowHeight;

            if (source.IsHierarchical)
            {
                if (rowY < 0.33)
                    return TreeDataGridRowDropPosition.Before;
                if (rowY > 0.66)
                    return TreeDataGridRowDropPosition.After;
                return TreeDataGridRowDropPosition.Inside;
            }

            return rowY < 0.5
                ? TreeDataGridRowDropPosition.Before
                : TreeDataGridRowDropPosition.After;
        }

        private static DataPackageOperation ToDataPackageOperation(Avalonia.Input.DragDropEffects effects)
        {
            var result = DataPackageOperation.None;

            if (effects.HasFlag(Avalonia.Input.DragDropEffects.Copy))
                result |= DataPackageOperation.Copy;
            if (effects.HasFlag(Avalonia.Input.DragDropEffects.Move))
                result |= DataPackageOperation.Move;
            if (effects.HasFlag(Avalonia.Input.DragDropEffects.Link))
                result |= DataPackageOperation.Link;

            return result;
        }

        private object? TryGetResourceValue(string key)
        {
            return TryGetResourceValue(Resources, key, ActualTheme, out var local)
                ? local
                : TryGetResourceValue(Application.Current?.Resources, key, ActualTheme, out var appValue)
                    ? appValue
                    : null;
        }

        private static bool TryGetResourceValue(ResourceDictionary? dictionary, string key, ElementTheme theme, out object? value)
        {
            if (dictionary is null)
            {
                value = null;
                return false;
            }

            if (TryGetDirectResourceValue(dictionary, key, out value))
                return true;

            if (TryGetThemeDictionaryResourceValue(dictionary, key, theme, out value))
                return true;

            for (var i = dictionary.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                if (TryGetResourceValue(dictionary.MergedDictionaries[i], key, theme, out value))
                    return true;
            }

            value = null;
            return false;
        }

        private static bool TryGetDirectResourceValue(ResourceDictionary dictionary, string key, out object? value)
        {
            foreach (var entry in dictionary)
            {
                if (entry.Key is string resourceKey && resourceKey == key)
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static bool TryGetThemeDictionaryResourceValue(ResourceDictionary dictionary, string key, ElementTheme theme, out object? value)
        {
            foreach (var themeKey in EnumerateThemeKeys(theme))
            {
                if (dictionary.ThemeDictionaries.TryGetValue(themeKey, out var themeValue) &&
                    themeValue is ResourceDictionary themeDictionary &&
                    TryGetDirectResourceValue(themeDictionary, key, out value))
                {
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static IEnumerable<string> EnumerateThemeKeys(ElementTheme theme)
        {
            switch (theme)
            {
                case ElementTheme.Dark:
                    yield return "Dark";
                    break;
                case ElementTheme.Light:
                    yield return "Light";
                    break;
            }

            yield return "Default";
        }

        private void ShowDropIndicator(TreeDataGridRow row, TreeDataGridRowDropPosition position)
        {
            if (_dragIndicatorHost is null ||
                row.ActualWidth <= 0 ||
                row.ActualHeight <= 0 ||
                position == TreeDataGridRowDropPosition.None)
            {
                HideDropIndicator();
                return;
            }

            var transform = row.TransformToVisual(_dragIndicatorHost);
            var rowBounds = transform.TransformBounds(new global::Windows.Foundation.Rect(0, 0, row.ActualWidth, row.ActualHeight));

            if (_dragIndicatorLine is not null)
                _dragIndicatorLine.Visibility = Visibility.Collapsed;
            if (_dragIndicatorBox is not null)
                _dragIndicatorBox.Visibility = Visibility.Collapsed;

            switch (position)
            {
                case TreeDataGridRowDropPosition.Before:
                    if (_dragIndicatorLine is not null)
                    {
                        Canvas.SetLeft(_dragIndicatorLine, rowBounds.Left);
                        Canvas.SetTop(_dragIndicatorLine, Math.Max(0, rowBounds.Top));
                        _dragIndicatorLine.Width = Math.Max(0, rowBounds.Width);
                        _dragIndicatorLine.Visibility = Visibility.Visible;
                    }
                    break;

                case TreeDataGridRowDropPosition.After:
                    if (_dragIndicatorLine is not null)
                    {
                        Canvas.SetLeft(_dragIndicatorLine, rowBounds.Left);
                        Canvas.SetTop(_dragIndicatorLine, Math.Max(0, rowBounds.Bottom - DragIndicatorThickness));
                        _dragIndicatorLine.Width = Math.Max(0, rowBounds.Width);
                        _dragIndicatorLine.Visibility = Visibility.Visible;
                    }
                    break;

                case TreeDataGridRowDropPosition.Inside:
                    if (_dragIndicatorBox is not null)
                    {
                        Canvas.SetLeft(_dragIndicatorBox, rowBounds.Left);
                        Canvas.SetTop(_dragIndicatorBox, rowBounds.Top);
                        _dragIndicatorBox.Width = Math.Max(0, rowBounds.Width);
                        _dragIndicatorBox.Height = Math.Max(0, rowBounds.Height);
                        _dragIndicatorBox.Visibility = Visibility.Visible;
                    }
                    break;
            }
        }

        private void HideDropIndicator()
        {
            if (_dragIndicatorLine is not null)
                _dragIndicatorLine.Visibility = Visibility.Collapsed;

            if (_dragIndicatorBox is not null)
                _dragIndicatorBox.Visibility = Visibility.Collapsed;
        }

        private void OnSourceModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ITreeDataGridSource.Selection))
                RebindSelection();

            Rebuild();
        }

        private void OnSourceSorted()
        {
            Rebuild();
        }

        private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshColumnPropertySubscriptions();
            Rebuild();
        }

        private void OnRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            QueueVisibleLayoutRefresh();

            // Files can expand from model-backed IsExpanded state while rows are being realized.
            // If that happens mid-rebuild, schedule one more pass instead of dropping the change.
            if (_isRebuilding)
            {
                _needsRebuildAfterCurrentPass = true;
                return;
            }

            Rebuild();
            InvalidateMeasure();
            InvalidateArrange();
            _rowsPresenter?.InvalidateMeasure();
            _rowsPresenter?.InvalidateArrange();
            _rowsScrollViewer?.InvalidateMeasure();
            _rowsScrollViewer?.InvalidateArrange();
            RequestColumnWidthsUpdate();
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (Visibility != Visibility.Visible)
                    return;

                UpdateLayout();
                _rowsPresenter?.UpdateLayout();
                _rowsScrollViewer?.UpdateLayout();
                RequestColumnWidthsUpdate();
            });
        }

        private void OnColumnsLayoutInvalidated(object? sender, EventArgs e)
        {
            if (_isUpdatingColumnWidths)
                return;

            RequestColumnWidthsUpdate();
        }

        private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName))
            {
                Rebuild();
            }
            else if (e.PropertyName == nameof(IColumn.Width))
            {
                RequestColumnWidthsUpdate();
            }
        }

        private void OnRowSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs e)
        {
            UpdateRowSelectionStates();
        }

        private void OnRowIndexesChanged(object? sender, TreeSelectionModelIndexesChangedEventArgs e)
        {
            UpdateRowSelectionStates();
        }

        private void OnRowSelectionSourceReset(object? sender, TreeSelectionModelSourceResetEventArgs e)
        {
            Rebuild();
        }

        private void OnRowSelectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ITreeSelectionModel.SelectedIndex) || e.PropertyName == nameof(ITreeSelectionModel.Count))
                UpdateRowSelectionStates();
        }

        private void OnCellSelectionChanged(object? sender, TreeDataGridCellSelectionChangedEventArgs e)
        {
            UpdateCellSelectionStates();
        }

        private void OnRowsScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_rowsScrollViewer is not null)
            {
                _rowsPresenter?.UpdateViewport(_rowsScrollViewer.VerticalOffset, GetRowsViewportHeight());
            }

            if (_isSyncingScroll || _headerScrollViewer is null || _rowsScrollViewer is null)
                return;

            if (Math.Abs(_headerScrollViewer.HorizontalOffset - _rowsScrollViewer.HorizontalOffset) <= ScrollSyncTolerance)
                return;

            _isSyncingScroll = true;
            try
            {
                _headerScrollViewer.ChangeView(_rowsScrollViewer.HorizontalOffset, null, null, true);
            }
            finally
            {
                _isSyncingScroll = false;
            }
        }

        private void OnHeaderScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_isSyncingScroll || _headerScrollViewer is null || _rowsScrollViewer is null)
                return;

            if (Math.Abs(_rowsScrollViewer.HorizontalOffset - _headerScrollViewer.HorizontalOffset) <= ScrollSyncTolerance)
                return;

            _isSyncingScroll = true;
            try
            {
                _rowsScrollViewer.ChangeView(_headerScrollViewer.HorizontalOffset, null, null, true);
            }
            finally
            {
                _isSyncingScroll = false;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DisconnectSourceSubscriptions();
            ConnectSourceSubscriptions();
            Rebuild();
            QueueVisibleLayoutRefresh();
            RequestColumnWidthsUpdate();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DisconnectSourceSubscriptions();
            ClearPresenters();
            ResetVisibleLayoutRefresh();
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            ApplyDragIndicatorBrushes();
            Rebuild();
        }

        private void OnTreeDataGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if ((e.NewSize.Width != e.PreviousSize.Width || e.NewSize.Height != e.PreviousSize.Height) &&
                e.NewSize.Width > 0)
            {
                RequestColumnWidthsUpdate();
            }
        }

        private void OnRowsScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_rowsScrollViewer is not null)
                _rowsPresenter?.UpdateViewport(_rowsScrollViewer.VerticalOffset, GetRowsViewportHeight());

            if ((e.NewSize.Width != e.PreviousSize.Width || e.NewSize.Height != e.PreviousSize.Height) &&
                e.NewSize.Width > 0)
            {
                RequestColumnWidthsUpdate();
            }
        }

        private void OnLayoutUpdated(object? sender, object e)
        {
            _ = sender;
            _ = e;

            if (!_needsVisibleLayoutRefresh ||
                Visibility != Visibility.Visible ||
                ActualWidth <= 0 ||
                ActualHeight <= 0 ||
                _rowsPresenter is null ||
                _columnHeadersPresenter is null)
            {
                return;
            }

            if (RowsHaveVisibleLayout())
            {
                ResetVisibleLayoutRefresh();
                return;
            }

            if (_visibleLayoutRefreshAttempts >= 2)
            {
                ResetVisibleLayoutRefresh();
                return;
            }

            ++_visibleLayoutRefreshAttempts;
            Rebuild();
            RequestColumnWidthsUpdate();
        }

        private static void OnVisibilityPropertyChanged(DependencyObject sender, DependencyProperty property)
        {
            if (sender is not TreeDataGrid control || control.Visibility != Visibility.Visible)
                return;

            control.QueueVisibleLayoutRefresh();
            control.InvalidateMeasure();
            control.InvalidateArrange();
            control._columnHeadersPresenter?.InvalidateMeasure();
            control._columnHeadersPresenter?.InvalidateArrange();
            control._rowsPresenter?.InvalidateMeasure();
            control._rowsPresenter?.InvalidateArrange();
            control.RequestColumnWidthsUpdate();
        }

        private bool RowsHaveVisibleLayout()
        {
            if (_rowsPresenter is null)
                return true;

            if (_rowsPresenter.RealizedRows.Count == 0)
                return _rows is null || _rows.Count == 0;

            return _rowsPresenter.RealizedRows.Any(row => row.ActualHeight > 0);
        }

        private double GetRowsViewportWidth()
        {
            if (_rowsScrollViewer?.ViewportWidth > 0)
                return _rowsScrollViewer.ViewportWidth;

            if (_rowsScrollViewer?.ActualWidth > 0)
                return _rowsScrollViewer.ActualWidth;

            return ActualWidth;
        }

        private double GetRowsViewportHeight()
        {
            if (_rowsScrollViewer?.ViewportHeight > 0)
                return _rowsScrollViewer.ViewportHeight;

            if (_rowsScrollViewer?.ActualHeight > 0)
                return _rowsScrollViewer.ActualHeight;

            return ActualHeight;
        }

        private void QueueVisibleLayoutRefresh()
        {
            _needsVisibleLayoutRefresh = true;
            _visibleLayoutRefreshAttempts = 0;
        }

        private void ResetVisibleLayoutRefresh()
        {
            _needsVisibleLayoutRefresh = false;
            _visibleLayoutRefreshAttempts = 0;
        }

        private void RequestColumnWidthsUpdate()
        {
            if (_columnWidthUpdateQueued || _columns is null || _rowsPresenter is null || _columnHeadersPresenter is null)
                return;

            if (DispatcherQueue is null)
            {
                UpdateColumnWidths();
                return;
            }

            _columnWidthUpdateQueued = true;
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                _columnWidthUpdateQueued = false;
                UpdateColumnWidths();
            });
        }

        private sealed class RowDragInfo
        {
            public RowDragInfo(ITreeDataGridSource source, IReadOnlyList<global::Avalonia.Controls.IndexPath> indexes)
            {
                Source = source;
                Indexes = indexes;
            }

            public ITreeDataGridSource Source { get; }
            public IReadOnlyList<global::Avalonia.Controls.IndexPath> Indexes { get; }
        }
    }
}
