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
public partial class TreeDataGrid : UserControl
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(ITreeDataGridSource), typeof(TreeDataGrid), new PropertyMetadata(null, ModelChanged));
    private readonly Grid _layout = new();
    private readonly TreeDataGridColumnHeadersPresenter _headers = new();
    private readonly ScrollViewer _headerScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TreeDataGridRowsPresenter _presenter = new();
    private readonly ColumnGeometry _geometry = new();
    private TreeDataGridPresentation? _presentation;
    private bool _loaded;
    public TreeDataGrid()
    {
        _layout.RowDefinitions.Add(new() { Height = Microsoft.UI.Xaml.GridLength.Auto });
        _layout.RowDefinitions.Add(new() { Height = new(1, Microsoft.UI.Xaml.GridUnitType.Star) });
        _headerScroll.Content = _headers;
        _headers.Owner = this;
        _scroll.Content = _presenter;
        _presenter.Owner = this;
        _layout.Children.Add(_headerScroll);
        Grid.SetRow(_scroll, 1);
        _layout.Children.Add(_scroll);
        Content = _layout;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _scroll.ViewChanged += OnViewChanged;
        _scroll.SizeChanged += (_, _) => UpdateViewport();
        SizeChanged += (_, _) => UpdateColumns();
    }
    public ITreeDataGridSource? Model { get => (ITreeDataGridSource?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public TreeDataGridPresentationOptions PresentationOptions { get; } = new();
    public Dictionary<string, DataTemplate> CellTemplates { get; } = new();
    public Func<CellColumn, TreeDataGridCell> CellFactory { get; set; } = static _ => new();
    public TreeDataGridPresentation? Presentation => _presentation;
    public TreeDataGridRowsPresenter RowsPresenter => _presenter;
    public TreeDataGridColumnHeadersPresenter ColumnHeadersPresenter => _headers;
    public ScrollViewer Scroll => _scroll;
    private static void ModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((TreeDataGrid)sender).ReplacePresentation();
    private void ReplacePresentation()
    {
        _presenter.Reset();
        if (_presentation is not null)
        {
            _presentation.ColumnsChanged -= OnColumnsChanged;
            _presentation.RowsChanged -= OnRowsChanged;
            _presentation.Dispose();
        }
        _presentation = Model is { } model ? TreeDataGridPresentation.Create(model, PresentationOptions) : null;
        if (_presentation is not null)
        {
            _presentation.ColumnsChanged += OnColumnsChanged;
            _presentation.RowsChanged += OnRowsChanged;
            if (!_loaded) _presentation.Suspend();
        }
        _presenter.SetPresentation(_loaded ? _presentation : null, _geometry);
        UpdateColumns();
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        _presentation?.Resume();
        _presenter.SetPresentation(_presentation, _geometry);
        UpdateColumns();
    }
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        _presenter.Reset();
        _headers.Update(null, _geometry, 0, 0);
        _presentation?.Suspend();
    }
    private void OnColumnsChanged(object? sender, EventArgs e)
    {
        _presenter.SetPresentation(_loaded ? _presentation : null, _geometry);
        UpdateColumns();
    }
    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => _presenter.RowsChanged(e);
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
        _headerScroll.ChangeView(_scroll.HorizontalOffset, null, null, true);
        UpdateViewport();
    }
    private void UpdateViewport()
    {
        var width = _scroll.ViewportWidth > 0 ? _scroll.ViewportWidth : Math.Max(0, ActualWidth);
        var height = _scroll.ViewportHeight > 0 ? _scroll.ViewportHeight : Math.Max(0, ActualHeight - 32);
        _presenter.UpdateViewport(_scroll.HorizontalOffset, _scroll.VerticalOffset, width, height);
        _headers.Update(_loaded ? _presentation : null, _geometry, _scroll.HorizontalOffset, width);
    }
}
