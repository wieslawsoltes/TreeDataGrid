using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;

namespace Uno.Controls;

/// <summary>Uno control presenting the shared framework-neutral TreeDataGrid model.</summary>
[TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_HeaderScrollViewer", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_RowsPresenter", Type = typeof(TreeDataGridRowsPresenter))]
[TemplatePart(Name = "PART_ColumnHeadersPresenter", Type = typeof(TreeDataGridColumnHeadersPresenter))]
public partial class TreeDataGrid : Control
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(ITreeDataGridSource), typeof(TreeDataGrid), new PropertyMetadata(null, ModelChanged));
    public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(
        nameof(SelectionMode), typeof(TreeDataGridSelectionMode), typeof(TreeDataGrid),
        new PropertyMetadata(TreeDataGridSelectionMode.Source, SelectionModeChanged));
    private TreeDataGridColumnHeadersPresenter? _headers;
    private ScrollViewer? _headerScroll;
    private ScrollViewer? _scroll;
    private TreeDataGridRowsPresenter? _presenter;
    private readonly ColumnGeometry _geometry = new();
    private TreeDataGridPresentation? _presentation;
    private bool _loaded;
    private bool _restoringModel;
    public TreeDataGrid()
    {
        DefaultStyleKey = typeof(TreeDataGrid);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => UpdateColumns();
    }
    protected override void OnApplyTemplate()
    {
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
        if (_scroll is not null)
        {
            _scroll.ViewChanged += OnViewChanged;
            _scroll.SizeChanged += OnScrollSizeChanged;
        }
        if (_presenter is not null) _presenter.Owner = this;
        if (_headers is not null) _headers.Owner = this;
        _presenter?.SetPresentation(_loaded ? _presentation : null, _geometry);
        UpdateColumns();
    }
    private void OnScrollSizeChanged(object sender, SizeChangedEventArgs e) => UpdateViewport();
    public ITreeDataGridSource? Model { get => (ITreeDataGridSource?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public TreeDataGridSelectionMode SelectionMode { get => (TreeDataGridSelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
    public TreeDataGridPresentationOptions PresentationOptions { get; } = new();
    public Dictionary<string, DataTemplate> CellTemplates { get; } = new();
    public Func<CellColumn, TreeDataGridCell> CellFactory { get; set; } = static _ => new();
    public TreeDataGridPresentation? Presentation => _presentation;
    public TreeDataGridRowsPresenter RowsPresenter => _presenter ?? throw new InvalidOperationException("The rows template part has not been applied.");
    public TreeDataGridColumnHeadersPresenter ColumnHeadersPresenter => _headers ?? throw new InvalidOperationException("The headers template part has not been applied.");
    public ScrollViewer Scroll => _scroll ?? throw new InvalidOperationException("The scroll template part has not been applied.");
    private static void ModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var grid = (TreeDataGrid)sender;
        if (grid._restoringModel) return;
        try { grid.ReplacePresentation(); }
        catch
        {
            grid._restoringModel = true;
            try { grid.SetValue(ModelProperty, e.OldValue); }
            finally { grid._restoringModel = false; }
            throw;
        }
    }
    private static void SelectionModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((TreeDataGrid)sender)._presentation?.Selection.Configure((TreeDataGridSelectionMode)e.NewValue);
    private void ReplacePresentation()
    {
        var next = Model is { } model ? TreeDataGridPresentation.Create(model, PresentationOptions) : null;
        try
        {
            next?.Selection.Configure(SelectionMode);
            if (!_loaded) next?.Suspend();
        }
        catch { next?.Dispose(); throw; }
        _presenter?.Reset();
        if (_presentation is not null)
        {
            _presentation.ColumnsChanged -= OnColumnsChanged;
            _presentation.RowsChanged -= OnRowsChanged;
            _presentation.Selection.Changed -= OnSelectionChanged;
            _presentation.Dispose();
        }
        _presentation = next;
        if (_presentation is not null)
        {
            _presentation.ColumnsChanged += OnColumnsChanged;
            _presentation.RowsChanged += OnRowsChanged;
            _presentation.Selection.Changed += OnSelectionChanged;
        }
        _presenter?.SetPresentation(_loaded ? _presentation : null, _geometry);
        UpdateColumns();
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        _presentation?.Resume();
        _presenter?.SetPresentation(_presentation, _geometry);
        UpdateColumns();
    }
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _pressedPoint = null;
        _loaded = false;
        _presenter?.Reset();
        _headers?.Update(null, _geometry, 0, 0);
        _presentation?.Suspend();
    }
    private void OnColumnsChanged(object? sender, EventArgs e)
    {
        _presenter?.SetPresentation(_loaded ? _presentation : null, _geometry);
        UpdateColumns();
    }
    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => _presenter?.RowsChanged(e);
    private void UpdateColumns()
    {
        if (_presentation is null) { _geometry.Commit([]); UpdateViewport(); return; }
        var widths = new double[_presentation.Columns.Count];
        var available = Math.Max(0, ActualWidth);
        var stars = 0d;
        for (var i = 0; i < widths.Length; ++i)
        {
            var column = _presentation.Columns[i];
            var width = column.Model.Width;
            if (width.IsStar) stars += width.Value;
            else
            {
                widths[i] = Math.Clamp(width.IsAuto ? 150 : width.Value, column.MinimumWidth, column.MaximumWidth);
                available -= widths[i];
            }
        }
        for (var i = 0; i < widths.Length; ++i)
        {
            var column = _presentation.Columns[i];
            if (column.Model.Width.IsStar)
                widths[i] = Math.Clamp(Math.Max(0, available) * column.Model.Width.Value / Math.Max(1, stars), column.MinimumWidth, column.MaximumWidth);
        }
        _geometry.Commit(widths);
        UpdateViewport();
    }
    private void OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_scroll is not null) _headerScroll?.ChangeView(_scroll.HorizontalOffset, null, null, true);
        UpdateViewport();
    }
    private void UpdateViewport()
    {
        if (_scroll is null) return;
        var width = _scroll.ViewportWidth > 0 ? _scroll.ViewportWidth : Math.Max(0, ActualWidth);
        var height = _scroll.ViewportHeight > 0 ? _scroll.ViewportHeight : Math.Max(0, ActualHeight - 32);
        _presenter?.UpdateViewport(_scroll.HorizontalOffset, _scroll.VerticalOffset, width, height);
        _headers?.Update(_loaded ? _presentation : null, _geometry, _scroll.HorizontalOffset, width);
    }
}
