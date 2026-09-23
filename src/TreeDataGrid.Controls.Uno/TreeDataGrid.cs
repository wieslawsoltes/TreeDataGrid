using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using TreeDataGridCore;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;

namespace Uno.Controls;

/// <summary>Uno control presenting the shared framework-neutral TreeDataGrid model.</summary>
[TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_HeaderScrollViewer", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_RowsPresenter", Type = typeof(TreeDataGridRowsPresenter))]
[TemplatePart(Name = "PART_ColumnHeadersPresenter", Type = typeof(TreeDataGridColumnHeadersPresenter))]
[ContentProperty(Name = nameof(ColumnDefinitions))]
public partial class TreeDataGrid : Control
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(ITreeDataGridSource), typeof(TreeDataGrid), new PropertyMetadata(null, ModelChanged));
    public static readonly DependencyProperty PresentationOptionsProperty = DependencyProperty.Register(
        nameof(PresentationOptions), typeof(ITreeDataGridPresentationOptions), typeof(TreeDataGrid), new PropertyMetadata(null, ModelChanged));
    public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(
        nameof(SelectionMode), typeof(TreeDataGridSelectionMode), typeof(TreeDataGrid),
        new PropertyMetadata(TreeDataGridSelectionMode.Row, SelectionModeChanged));
    public static readonly DependencyProperty ShowColumnHeadersProperty = DependencyProperty.Register(
        nameof(ShowColumnHeaders), typeof(bool), typeof(TreeDataGrid), new PropertyMetadata(true, HeaderOptionsChanged));
    public static readonly DependencyProperty CanUserResizeColumnsProperty = DependencyProperty.Register(
        nameof(CanUserResizeColumns), typeof(bool), typeof(TreeDataGrid), new PropertyMetadata(false, HeaderOptionsChanged));
    public static readonly DependencyProperty CanUserSortColumnsProperty = DependencyProperty.Register(
        nameof(CanUserSortColumns), typeof(bool), typeof(TreeDataGrid), new PropertyMetadata(true, HeaderOptionsChanged));
    public static readonly DependencyProperty ColumnHeaderStyleProperty = DependencyProperty.Register(
        nameof(ColumnHeaderStyle), typeof(Style), typeof(TreeDataGrid), new PropertyMetadata(null, HeaderOptionsChanged));
    private TreeDataGridColumnHeadersPresenter? _headers;
    private ScrollViewer? _headerScroll;
    private ScrollViewer? _scroll;
    private TreeDataGridRowsPresenter? _presenter;
    private readonly ColumnGeometry _geometry = new();
    private TreeDataGridPresentation? _presentation;
    private bool _loaded;
    private bool _selectionModeConfigured;
    private int _presentationRevision;
    public TreeDataGrid()
    {
        DefaultStyleKey = typeof(TreeDataGrid);
        InitializeRowDragDrop();
        InitializeDeclarative();
        InitializeTextInput();
        InitializeAppearance();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => UpdateColumns();
        LayoutUpdated += OnLayoutUpdated;
    }
    protected override void OnApplyTemplate()
    {
        CancelRowDrag();
        if (_headerScroll is not null) _headerScroll.ViewChanged -= OnHeaderViewChanged;
        if (_scroll is not null)
        {
            _scroll.ViewChanged -= OnViewChanged;
            _scroll.SizeChanged -= OnScrollSizeChanged;
        }
        if (_presenter is not null) { _presenter.Reset(); _presenter.Owner = null; }
        if (_headers is not null) { _headers.Update(null, _geometry, 0, 0); _headers.Owner = null; }
        base.OnApplyTemplate();
        _scroll = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
        _headerScroll = GetTemplateChild("PART_HeaderScrollViewer") as ScrollViewer;
        _presenter = GetTemplateChild("PART_RowsPresenter") as TreeDataGridRowsPresenter;
        _headers = GetTemplateChild("PART_ColumnHeadersPresenter") as TreeDataGridColumnHeadersPresenter;
        ApplyDragTemplate();
        if (_headerScroll is not null)
        {
            _headerScroll.ViewChanged += OnHeaderViewChanged;
            _headerScroll.Visibility = ShowColumnHeaders ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_scroll is not null)
        {
            _scroll.ViewChanged += OnViewChanged;
            _scroll.SizeChanged += OnScrollSizeChanged;
        }
        if (_presenter is not null) { _presenter.Owner = this; _presenter.ConfigureRows(RowHeight, MinRowHeight); }
        if (_headers is not null) _headers.Owner = this;
        _presenter?.SetPresentation(_loaded ? _presentation : null, _geometry);
        UpdateColumns();
        SetValue(ScrollProperty, _scroll);
    }
    private void OnScrollSizeChanged(object sender, SizeChangedEventArgs e) => UpdateColumns();
    public ITreeDataGridSource? Model { get => (ITreeDataGridSource?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public TreeDataGridSelectionMode SelectionMode
    {
        get => (TreeDataGridSelectionMode)GetValue(SelectionModeProperty);
        set
        {
            _selectionModeConfigured = true;
            var unchanged = SelectionMode == value;
            SetValue(SelectionModeProperty, value);
            if (unchanged) _presentation?.Selection.Configure(value);
        }
    }
    public bool ShowColumnHeaders { get => (bool)GetValue(ShowColumnHeadersProperty); set => SetValue(ShowColumnHeadersProperty, value); }
    public bool CanUserResizeColumns { get => (bool)GetValue(CanUserResizeColumnsProperty); set => SetValue(CanUserResizeColumnsProperty, value); }
    public bool CanUserSortColumns { get => (bool)GetValue(CanUserSortColumnsProperty); set => SetValue(CanUserSortColumnsProperty, value); }
    public Style? ColumnHeaderStyle { get => (Style?)GetValue(ColumnHeaderStyleProperty); set => SetValue(ColumnHeaderStyleProperty, value); }
    public ITreeDataGridPresentationOptions? PresentationOptions
    {
        get => (ITreeDataGridPresentationOptions?)GetValue(PresentationOptionsProperty);
        set => SetValue(PresentationOptionsProperty, value);
    }
    public Dictionary<string, DataTemplate> CellTemplates { get; } = new();
    public Dictionary<string, DataTemplate> CellEditingTemplates { get; } = new();
    public TreeDataGridPresentation? Presentation => _presentation;
    public TreeDataGridRowsPresenter? RowsPresenter => _presenter;
    public TreeDataGridColumnHeadersPresenter? ColumnHeadersPresenter => _headers;
    public ScrollViewer? Scroll => _scroll;
    private static void ModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        => ((TreeDataGrid)sender).OnPresentationConfigurationChanged(e);
    private static void SelectionModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var grid = (TreeDataGrid)sender;
        grid.ResetTextSearch();
        grid._selectionModeConfigured = true;
        grid._presentation?.Selection.Configure((TreeDataGridSelectionMode)e.NewValue);
    }
    private static void HeaderOptionsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var grid = (TreeDataGrid)sender;
        if (e.Property == ColumnHeaderStyleProperty) grid.InvalidateAppearanceMeasurements();
        if (grid._headerScroll is not null)
            grid._headerScroll.Visibility = grid.ShowColumnHeaders ? Visibility.Visible : Visibility.Collapsed;
        grid.UpdateViewport();
    }
    private void ReplacePresentation()
    {
        ResetTextSearch();
        CancelRowDrag();
        _tapAllowed = false;
        _pendingVerticalAnchor = null;
        var revision = ++_presentationRevision;
        var model = ActiveSource;
        var next = model is not null ? TreeDataGridPresentation.Create(model, PresentationOptions) : null;
        try
        {
            // Match Avalonia: an untouched SelectionMode must not overwrite a
            // Core source's explicitly supplied row/cell selection model.
            if (ReferenceEquals(model, _generatedSource?.Source) || _selectionModeConfigured || ReadLocalValue(SelectionModeProperty) != DependencyProperty.UnsetValue)
                next?.Selection.Configure(SelectionMode);
            if (!_loaded) next?.Suspend();
            if (revision != _presentationRevision) return;
            CancelEdit();
            if (revision != _presentationRevision) return;
            _presenter?.Reset();
            if (revision != _presentationRevision) return;
            var previous = _presentation;
            _presentation = null;
            try
            {
                UpdateSelectionInteraction();
                if (previous is not null)
                {
                    previous.ColumnsChanged -= OnColumnsChanged;
                    previous.Columns.LayoutInvalidated -= OnColumnLayoutInvalidated;
                    previous.RowsChanged -= OnRowsChanged;
                    previous.PropertyChanged -= OnPresentationPropertyChanged;
                    previous.Selection.SelectionChanged -= OnDetailedSelectionChanged;
                }
                if (revision != _presentationRevision) return;
                _presentation = next;
                next = null;
                if (_presentation is not null)
                {
                    _presentation.ColumnsChanged += OnColumnsChanged;
                    _presentation.Columns.LayoutInvalidated += OnColumnLayoutInvalidated;
                    _presentation.RowsChanged += OnRowsChanged;
                    _presentation.PropertyChanged += OnPresentationPropertyChanged;
                    if (_selectionChanged is not null)
                        _presentation.Selection.SelectionChanged += OnDetailedSelectionChanged;
                }
                if (!PublishPresentationProperties(revision)) return;
                UpdateSelectionInteraction();
                if (revision != _presentationRevision) return;
                _presenter?.SetPresentation(_loaded ? _presentation : null, _geometry);
                UpdateColumns();
                NotifyAutomationSelectionChanged();
            }
            // Publish the replacement before disposing the old view, as in the
            // reference. Disposal callbacks must not see public Rows pointing
            // at an already disposed facade. Finally still retires the old view
            // when a property callback installs a newer source and returns early.
            finally { previous?.Dispose(); }
        }
        finally { next?.Dispose(); }
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        _presentation?.Resume();
        UpdateSelectionInteraction();
        _presenter?.SetPresentation(_presentation, _geometry);
        UpdateColumns();
        NotifyAutomationSelectionChanged();
    }
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ResetTextSearch();
        CancelRowDrag();
        _pendingVerticalAnchor = null;
        _pressedPoint = null;
        _tapAllowed = false;
        CancelEdit();
        _loaded = false;
        UpdateSelectionInteraction();
        _presenter?.Reset();
        _headers?.Update(null, _geometry, 0, 0);
        _presentation?.Suspend();
        NotifyAutomationSelectionChanged();
    }
    private void OnColumnsChanged(object? sender, EventArgs e)
    {
        unchecked { ++_textSearchStructureRevision; }
        _presenter?.SetPresentation(_loaded ? _presentation : null, _geometry);
        UpdateColumns();
    }
    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        unchecked { ++_textSearchStructureRevision; }
        _rowDragCandidate = null;
        _tapAllowed = false;
        // The presenter subscribes directly to Items, as in Avalonia. Forwarding
        // this event as well would shift/recycle each row twice.
    }
    private void OnColumnLayoutInvalidated(object? sender, EventArgs args)
    {
        if (ReferenceEquals(sender, _presentation?.Columns)) UpdateColumns(measurementsOnly: true);
    }
    private bool UpdateColumns(bool measurementsOnly = false)
    {
        if (_presentation is not { } presentation) { var cleared = _geometry.Commit([]); UpdateViewport(); return cleared; }
        var revision = _presentationRevision;
        var structure = _textSearchStructureRevision;
        var columns = presentation.NativeColumns;
        var count = columns.Count;
        var available = _scroll?.ViewportWidth > 0 ? _scroll.ViewportWidth : Math.Max(0, ActualWidth - BorderThickness.Left - BorderThickness.Right);
        var buffer = ArrayPool<double>.Shared.Rent(count);
        try
        {
            // Every nested update owns its own rental. No cached mutable buffer
            // can be overwritten by user code reentering layout or another grid.
            var widths = buffer.AsSpan(0, count);
            ColumnWidths.Calculate(columns, available, widths);
            if (!IsCurrent()) return false;
            var changed = _geometry.CommitSpan(widths);
            for (var i = 0; i < count; ++i)
            {
                columns[i].SetActualWidth(widths[i]);
                if (!IsCurrent()) return changed;
            }
            if (presentation.Columns is Models.TreeDataGrid.ColumnListBase<CellColumn> layout) layout.AcceptNativeWidths(available);
            if (!IsCurrent()) return changed;
            if (changed) _presenter?.InvalidateRowMeasurements();
            if (measurementsOnly && !changed) return false;
            UpdateViewport();
            return changed;
        }
        finally { ArrayPool<double>.Shared.Return(buffer); }

        bool IsCurrent() => revision == _presentationRevision && structure == _textSearchStructureRevision &&
            ReferenceEquals(presentation, _presentation) && columns.Count == count;
    }
    internal bool CommitColumnMeasurements() => UpdateColumns(measurementsOnly: true);
    private void OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        _pendingVerticalAnchor = null;
        _presenter?.CancelPendingAnchor();
        if (_scroll is not null) _headerScroll?.ChangeView(_scroll.HorizontalOffset, null, null, true);
        UpdateViewport();
    }
    private void OnHeaderViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_scroll is not null && _headerScroll is not null &&
            Math.Abs(_scroll.HorizontalOffset - _headerScroll.HorizontalOffset) > 0.01)
            _scroll.ChangeView(_headerScroll.HorizontalOffset, null, null, true);
    }
    private void UpdateViewport()
    {
        if (_scroll is null) return;
        var width = _scroll.ViewportWidth > 0 ? _scroll.ViewportWidth : Math.Max(0, ActualWidth);
        var height = _scroll.ViewportHeight > 0 ? _scroll.ViewportHeight : Math.Max(0, ActualHeight - 32);
        // The body can reserve space for a vertical scrollbar. Give the header
        // the same horizontal viewport or its smaller maximum scroll offset
        // would feed back into the body at the right edge.
        if (_headerScroll is not null && Math.Abs(_headerScroll.MaxWidth - width) > 0.01)
            _headerScroll.MaxWidth = width;
        _presenter?.UpdateViewport(_scroll.HorizontalOffset, _pendingVerticalAnchor ?? _scroll.VerticalOffset, width, height);
        _headers?.Update(_loaded && ShowColumnHeaders ? _presentation : null, _geometry, _scroll.HorizontalOffset, width);
    }
}
