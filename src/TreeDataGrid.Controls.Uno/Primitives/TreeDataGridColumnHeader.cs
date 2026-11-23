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
[TemplatePart(Name = "PART_Resizer", Type = typeof(Control))]
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
    private Control? _resizer;
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
    public bool CanUserResize { get => (bool)GetValue(CanUserResizeProperty); private set => SetValue(CanUserResizeProperty, value ? s_true : s_false); }
    public ListSortDirection? SortDirection { get => (ListSortDirection?)GetValue(SortDirectionProperty); private set => SetValue(SortDirectionProperty, BoxSortDirection(value)); }
    public int ColumnIndex { get; private set; } = -1;
    public object? Header { get => GetValue(HeaderProperty); private set => SetValue(HeaderProperty, value); }
    public CellColumn? Column => _model as CellColumn;
    public void Realize(IColumns columns, int columnIndex) => RealizeHeader(columns, columnIndex);
    public void UpdateColumnIndex(int columnIndex) => UpdateHeaderIndex(columnIndex);

    internal void SetOwner(TreeDataGrid? owner)
    {
        if (_unrealizing || _pendingUnrealize) return;
        _owner = owner;
        RefreshProperties();
    }

    internal void Realize(TreeDataGrid owner, CellColumn column, int index)
    {
        if (_realizing || _unrealizing) throw new InvalidOperationException("Column header lifetime transition is in progress.");
        if (!ReferenceEquals(_model, column))
        {
            if (_model is not null) Unrealize();
            _owner = owner;
            Realize(owner.Presentation!.Columns, index);
        }
        else
        {
            UpdateColumnIndex(index);
            if (!ReferenceEquals(_model, column)) return;
            SetOwner(owner);
        }
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _model)) return;
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(IColumn.Header) or nameof(IColumn.SortDirection) or
            nameof(IColumn.CanUserResize) or nameof(CellColumn.HeaderTemplate) or nameof(CellColumn.HeaderTemplateSelector))
            RefreshProperties();
    }

    public void Unrealize() => UnrealizeHeader();

    protected override void OnApplyTemplate()
    {
        if (_resizer is Thumb oldThumb)
        {
            oldThumb.DragStarted -= OnResizeStarted;
            oldThumb.DragDelta -= OnResizeDelta;
            oldThumb.DragCompleted -= OnResizeCompleted;
            TreeDataGridColumnResizer.CancelThumbDrag(oldThumb);
        }
        else if (_resizer is TreeDataGridColumnResizer oldGrip)
        {
            oldGrip.DragStarted -= OnResizeStarted;
            oldGrip.DragDelta -= OnResizeDelta;
            oldGrip.DragCompleted -= OnResizeCompleted;
            oldGrip.CancelDrag();
        }
        if (_resizer is not null) _resizer.DoubleTapped -= OnResizeDoubleTapped;
        _resizing = false;
        base.OnApplyTemplate();
        // Keep accepting a native Thumb in application-supplied header templates.
        // The default theme uses a composed grip because WinUI Thumb is sealed.
        var part = GetTemplateChild("PART_Resizer");
        _resizer = part is Thumb or TreeDataGridColumnResizer ? (Control)part : null;
        if (_resizer is Thumb thumb)
        {
            thumb.DragStarted += OnResizeStarted;
            thumb.DragDelta += OnResizeDelta;
            thumb.DragCompleted += OnResizeCompleted;
        }
        else if (_resizer is TreeDataGridColumnResizer grip)
        {
            grip.DragStarted += OnResizeStarted;
            grip.DragDelta += OnResizeDelta;
            grip.DragCompleted += OnResizeCompleted;
        }
        if (_resizer is not null) _resizer.DoubleTapped += OnResizeDoubleTapped;
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
        if (_resizer is not null) _resizer.SetValue(VisibilityProperty, CanUserResize ? s_visible : s_collapsed);
    }
    private void OnHeaderClick(object sender, RoutedEventArgs e) => CycleSort();
}
