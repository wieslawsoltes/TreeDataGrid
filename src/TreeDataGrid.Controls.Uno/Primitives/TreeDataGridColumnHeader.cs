using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Uno.Controls.Presentation;
using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Primitives;

/// <summary>A themeable column header with independent content, sort glyph and resize grip.</summary>
[TemplatePart(Name = "PART_Resizer", Type = typeof(Thumb))]
public partial class TreeDataGridColumnHeader : Button
{
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new global::Uno.Controls.Automation.Peers.TreeDataGridColumnHeaderAutomationPeer(this);
    public static readonly DependencyProperty CanUserResizeProperty = DependencyProperty.Register(
        nameof(CanUserResize), typeof(bool), typeof(TreeDataGridColumnHeader), new PropertyMetadata(false, StateChanged));
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(object), typeof(TreeDataGridColumnHeader), new PropertyMetadata(null));
    public static readonly DependencyProperty SortDirectionProperty = DependencyProperty.Register(
        nameof(SortDirection), typeof(ListSortDirection?), typeof(TreeDataGridColumnHeader), new PropertyMetadata(null, StateChanged));
    private Thumb? _resizer;
    private TreeDataGrid? _owner;
    private IColumns? _columns;
    private IColumn? _model;
    private bool _resizing;
    private int _realizationVersion;
    public TreeDataGridColumnHeader()
    {
        DefaultStyleKey = typeof(TreeDataGridColumnHeader);
        Click += OnHeaderClick;
    }
    public bool CanUserResize { get => (bool)GetValue(CanUserResizeProperty); private set => SetValue(CanUserResizeProperty, value); }
    public ListSortDirection? SortDirection { get => (ListSortDirection?)GetValue(SortDirectionProperty); private set => SetValue(SortDirectionProperty, value); }
    public int ColumnIndex { get; private set; } = -1;
    public object? Header { get => GetValue(HeaderProperty); private set => SetValue(HeaderProperty, value); }
    public CellColumn? Column => _model as CellColumn;
    public void Realize(IColumns columns, int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (_model is not null) throw new InvalidOperationException("Column header is already realized.");
        if ((uint)columnIndex >= (uint)columns.Count) throw new ArgumentOutOfRangeException(nameof(columnIndex));
        ++_realizationVersion;
        _columns = columns;
        _model = columns[columnIndex];
        ColumnIndex = columnIndex;
        try
        {
            _model.PropertyChanged += OnModelPropertyChanged;
            RefreshProperties();
        }
        catch (Exception error)
        {
            try { Unrealize(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }

    public void UpdateColumnIndex(int columnIndex) => ColumnIndex = columnIndex;

    internal void SetOwner(TreeDataGrid? owner)
    {
        _owner = owner;
        RefreshProperties();
    }

    internal void Realize(TreeDataGrid owner, CellColumn column, int index)
    {
        SetOwner(owner);
        if (!ReferenceEquals(_model, column))
        {
            if (_model is not null) Unrealize();
            _owner = owner;
            Realize(owner.Presentation!.Columns, index);
        }
        else { UpdateColumnIndex(index); RefreshProperties(); }
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _model)) return;
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(IColumn.Header) or nameof(IColumn.SortDirection) or
            nameof(IColumn.CanUserResize) or nameof(CellColumn.HeaderTemplate) or nameof(CellColumn.HeaderTemplateSelector))
            RefreshProperties();
    }

    private void RefreshProperties()
    {
        var revision = _realizationVersion;
        var model = _model;
        // Never stringify user content to append the sort arrow. A header can
        // be a model with a DataTemplate or a native control.
        Header = model?.Header;
        if (revision != _realizationVersion) return;
        Content = Header;
        if (revision != _realizationVersion) return;
        if ((model as CellColumn)?.HeaderTemplate is { } template) ContentTemplate = template;
        else ClearValue(ContentTemplateProperty);
        if (revision != _realizationVersion) return;
        if ((model as CellColumn)?.HeaderTemplateSelector is { } selector) ContentTemplateSelector = selector;
        else ClearValue(ContentTemplateSelectorProperty);
        if (revision != _realizationVersion) return;
        CanUserResize = model?.CanUserResize ?? _owner?.CanUserResizeColumns ?? false;
        if (revision != _realizationVersion) return;
        SortDirection = model?.SortDirection;
        if (revision != _realizationVersion) return;
        Visibility = model is null ? Visibility.Collapsed : Visibility.Visible;
    }
    public void Unrealize()
    {
        ++_realizationVersion;
        var model = _model;
        _owner = null;
        _columns = null;
        _model = null;
        ColumnIndex = -1;
        _resizing = false;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? error = null;
        try { if (model is not null) model.PropertyChanged -= OnModelPropertyChanged; }
        catch (Exception e) { error = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e); }
        // Complete cleanup even when an application's property callback throws.
        Clear(HeaderProperty, null);
        Clear(ContentProperty, null);
        Clear(ContentTemplateProperty, null);
        Clear(ContentTemplateSelectorProperty, null);
        Clear(CanUserResizeProperty, false);
        Clear(SortDirectionProperty, null);
        Clear(VisibilityProperty, Visibility.Collapsed);
        error?.Throw();

        void Clear(DependencyProperty property, object? value)
        {
            try { SetValue(property, value); }
            catch (Exception e) { error ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e); }
        }
    }
    protected override void OnApplyTemplate()
    {
        if (_resizer is not null)
        {
            _resizer.DragStarted -= OnResizeStarted;
            _resizer.DragDelta -= OnResizeDelta;
            _resizer.DragCompleted -= OnResizeCompleted;
            _resizer.DoubleTapped -= OnResizeDoubleTapped;
        }
        _resizing = false;
        base.OnApplyTemplate();
        _resizer = GetTemplateChild("PART_Resizer") as Thumb;
        if (_resizer is not null)
        {
            _resizer.DragStarted += OnResizeStarted;
            _resizer.DragDelta += OnResizeDelta;
            _resizer.DragCompleted += OnResizeCompleted;
            _resizer.DoubleTapped += OnResizeDoubleTapped;
        }
        UpdateState();
    }
    private static void StateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((TreeDataGridColumnHeader)sender).UpdateState();
    private void UpdateState()
    {
        VisualStateManager.GoToState(this, SortDirection switch
        {
            ListSortDirection.Ascending => "SortAscending",
            ListSortDirection.Descending => "SortDescending",
            _ => "Unsorted",
        }, false);
        if (_resizer is not null) _resizer.Visibility = CanUserResize ? Visibility.Visible : Visibility.Collapsed;
    }
    private void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (!_resizing && _owner is { CanUserSortColumns: true } owner && Column is { CanUserSort: not false } column)
            owner.Presentation?.SortBy(column, SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending : ListSortDirection.Ascending);
    }
    private void OnResizeStarted(object sender, DragStartedEventArgs e) => _resizing = true;
    private void OnResizeCompleted(object sender, DragCompletedEventArgs e) => _resizing = false;
    private void OnResizeDelta(object sender, DragDeltaEventArgs e)
    {
        if (!CanUserResize || _columns is not { } columns || _model is not { } column || !double.IsFinite(e.HorizontalChange)) return;
        var current = column.Width.IsAbsolute ? column.Width.Value : ActualWidth;
        var next = current + e.HorizontalChange;
        if (!double.IsFinite(next)) return;
        var minimum = column is IUpdateColumnLayout layout ? Math.Max(0, layout.MinActualWidth) : 0;
        var maximum = column is IUpdateColumnLayout limits ? limits.MaxActualWidth : double.PositiveInfinity;
        // The same constraint precedence as layout: maximum wins if Min > Max.
        columns.SetColumnWidth(ColumnIndex, new GridLength(Math.Min(maximum, Math.Max(minimum, next))));
    }
    private void OnResizeDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (!CanUserResize || _columns is null || _model is null) return;
        _columns.SetColumnWidth(ColumnIndex, GridLength.Auto);
        e.Handled = true;
    }
}
