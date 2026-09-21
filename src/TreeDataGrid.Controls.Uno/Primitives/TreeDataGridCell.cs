using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;

namespace Uno.Controls.Primitives;

/// <summary>A parented, reusable Uno cell control over a Core row.</summary>
public partial class TreeDataGridCell : Control
{
    private global::Uno.Controls.Automation.Peers.TreeDataGridCellAutomationPeer? _automationPeer;
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        _automationPeer = new global::Uno.Controls.Automation.Peers.TreeDataGridCellAutomationPeer(this);
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(TreeDataGridCell), new PropertyMetadata(false, OnStateChanged));
    public static readonly DependencyProperty IsCurrentProperty = DependencyProperty.Register(
        nameof(IsCurrent), typeof(bool), typeof(TreeDataGridCell), new PropertyMetadata(false, OnStateChanged));
    private TextBlock? _text;
    private TextAlignment _templateTextAlignment;
    private TextWrapping _templateTextWrapping;
    private TextTrimming _templateTextTrimming;
    private CheckBox? _check;
    private ContentPresenter? _content;
    private Button? _expander;
    private CellKind _kind;
    private DataTemplate? _template;
    private int _indent;
    private ExpanderCellValue? _expanderValue;
    private CellValue? _value;
    private CellValue? _subscribedValue;
    private bool _updating;
    private bool _rebinding;
    private bool _prepared;
    internal int RealizationVersion { get; private set; }
    private bool _isRowSelected;
    internal bool IsRowSelected
    {
        get => _isRowSelected;
        set { if (_isRowSelected != value) { _isRowSelected = value; UpdateState(); } }
    }
    internal TreeDataGridRowsPresenter? Presenter { get; set; }
    internal TreeDataGridElementFactory? ContainerFactory { get; set; }
    internal TreeDataGridCell? OwningCell { get; set; }
    internal virtual TreeDataGridCell GetEditingTarget() => this;
    protected virtual bool UsesInnerCellControl => false;

    public TreeDataGridCell() : this(CellKind.Text) { }
    protected TreeDataGridCell(CellKind kind)
    {
        _kind = kind;
        DefaultStyleKey = typeof(TreeDataGridCell);
    }
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public bool IsCurrent { get => (bool)GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, value); }
    private static void OnStateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((TreeDataGridCell)sender).UpdateState();
    protected virtual void UpdateState()
    {
        VisualStateManager.GoToState(this, IsSelected ? IsRowSelected ? "RowSelected" : "Selected" : "Unselected", false);
        VisualStateManager.GoToState(this, IsCurrent ? "Current" : "NotCurrent", false);
        VisualStateManager.GoToState(this, HasValidationError ? "Invalid" : "Valid", false);
        VisualStateManager.GoToState(this, IsEditing ? "Editing" : "NotEditing", false);
    }
    protected override void OnApplyTemplate()
    {
        CancelEdit();
        if (_editor is not null) _editor.KeyDown -= OnEditorKeyDown;
        _editor = null;
        if (_check is not null)
        {
            _check.Checked -= OnCheckChanged;
            _check.Unchecked -= OnCheckChanged;
            _check.Indeterminate -= OnCheckChanged;
        }
        if (_expander is not null) _expander.Click -= OnExpand;
        if (_content is not null) _content.Content = null;
        base.OnApplyTemplate();
        _text = GetTemplateChild("PART_Text") as TextBlock;
        _templateTextAlignment = _text?.TextAlignment ?? TextAlignment.Left;
        _templateTextWrapping = _text?.TextWrapping ?? TextWrapping.NoWrap;
        _templateTextTrimming = _text?.TextTrimming ?? TextTrimming.None;
        _check = GetTemplateChild("PART_CheckBox") as CheckBox;
        _content = GetTemplateChild("PART_ContentPresenter") as ContentPresenter ?? GetTemplateChild("PART_Content") as ContentPresenter;
        _expander = GetTemplateChild("PART_Expander") as Button;
        _editorHost = GetTemplateChild("PART_EditorHost") as Grid;
        _editContent = GetTemplateChild("PART_EditingContentPresenter") as ContentPresenter ?? GetTemplateChild("PART_EditingContent") as ContentPresenter;
        _editor = GetTemplateChild("PART_Edit") as TextBox;
        if (_editor is not null) _editor.KeyDown += OnEditorKeyDown;
        if (_check is not null)
        {
            _check.Checked += OnCheckChanged;
            _check.Unchecked += OnCheckChanged;
            _check.Indeterminate += OnCheckChanged;
        }
        if (_expander is not null) _expander.Click += OnExpand;
        UpdateContentKind();
        UpdateValue();
        UpdateState();
    }
    private void OnExpand(object sender, RoutedEventArgs e) { if (_expanderValue is { } value) value.IsExpanded = !value.IsExpanded; }

    public CellValue? Value => _value;
    /// <summary>The original cell model supplied by the column, as in Avalonia.</summary>
    public Models.TreeDataGrid.ICell? Model => _value?.PresentationModel;
    /// <summary>The Uno presentation adapter for the current cell model.</summary>
    public CellValue? ViewModel => _value;
    public bool IsEffectivelySelected => IsSelected || OwningCell?.IsEffectivelySelected == true ||
        OwningRow?.IsSelected == true || Presenter?.Owner?.TryGetRow(RowIndex)?.IsSelected == true;
    public IRow? Row { get; private set; }
    /// <summary>The realized model, captured because flat Core rows can be ephemeral.</summary>
    public object? RowModel { get; private set; }
    public int RowIndex { get; private set; } = -1;
    public int ColumnIndex { get; private set; } = -1;
    public CellColumn? Column { get; private set; }
    public virtual void BeginRebind() => _rebinding = true;
    public virtual void EndRebind(bool realized)
    {
        _rebinding = false;
        if (realized) UpdateValue();
        else ClearContent();
    }

    internal virtual void UpdateIndexes(int row, int column) { RowIndex = row; ColumnIndex = column; }

    public virtual void Realize(CellColumn column, CellValue value, IRow row, int columnIndex, int rowIndex, DataTemplate? template, DataTemplate? editingTemplate = null)
    {
        if (_directRealization)
        {
            RealizeCore(column, value, row, _standaloneRow is { } standalone ? standalone.Model : row.Model, columnIndex, rowIndex, template, editingTemplate);
            return;
        }
        if (_realizationContext is not null) throw new InvalidOperationException("Cell realization is already in progress.");
        _realizationContext = new(column, value, row, _nativeRow is { } captured ? captured.Model : row.Model, template, editingTemplate);
        try
        {
            Realize(ContainerFactory ?? Presenter?.Owner?.ElementFactory ?? (_fallbackFactory ??= new TreeDataGridElementFactory()),
                Presenter?.Owner?.Presentation?.Selection, value.PresentationModel, columnIndex, rowIndex);
        }
        finally { _realizationContext = null; }
    }

    private void RealizeCore(CellColumn column, CellValue value, IRow row, object? rowModel, int columnIndex, int rowIndex,
        DataTemplate? template, DataTemplate? editingTemplate)
    {
        if (RowIndex >= 0 || ColumnIndex >= 0) throw new InvalidOperationException("Cell is already realized.");
        if (rowIndex < 0) throw new ArgumentOutOfRangeException(nameof(rowIndex));
        if (columnIndex < 0) throw new ArgumentOutOfRangeException(nameof(columnIndex));
        unchecked { ++RealizationVersion; }
        var realization = RealizationVersion;
        Column = column;
        _value = value;
        _expanderValue = value as ExpanderCellValue;
        Row = row;
        RowModel = rowModel;
        ColumnIndex = columnIndex;
        RowIndex = rowIndex;
        _kind = value.ContentKind;
        // An expander wraps the inner content contract; the value remains the same
        // Core row and the native children stay attached while it is recycled.
        if (!UsesInnerCellControl && _kind == CellKind.Template && template is null)
            throw new InvalidOperationException($"No Uno cell template is registered for '{column.Model.PresentationKey}'.");
        _indent = ((_expanderValue?.Row ?? row) as IIndentedRow)?.Indent ?? 0;
        _template = template;
        _editingTemplate = editingTemplate;
        UpdateContentKind();
        if (!ReferenceEquals(_value, value) || RealizationVersion != realization) return;
        SubscribeToModelChanges();
        Visibility = Visibility.Visible;
        if (!ReferenceEquals(_value, value) || RealizationVersion != realization) return;
        if (!_rebinding) UpdateValue();
    }

    public virtual void Unrealize()
    {
        var adapter = _standaloneAdapter;
        _standaloneAdapter = null;
        try
        {
            if (_prepared)
            {
                _prepared = false;
                Presenter?.Owner?.RaiseCellClearing(this, ColumnIndex, RowIndex);
            }
        }
        finally
        {
            try { CancelEdit(); }
            finally
            {
                try
                {
                    UnsubscribeFromModelChanges();
                    _value = null;
                    _expanderValue = null;
                    Row = null;
                    RowModel = null;
                    OwningRow = null;
                    RowIndex = ColumnIndex = -1;
                    Column = null;
                    ContainerFactory = null;
                    IsSelected = IsCurrent = false;
                    IsRowSelected = false;
                    if (!_rebinding) ClearContent();
                    NotifyAutomationValueChanged();
                }
                finally { adapter?.Dispose(); }
            }
        }
    }
    internal void NotifyPrepared()
    {
        if (_prepared || RowIndex < 0 || ColumnIndex < 0) return;
        _prepared = true;
        NotifyAutomationValueChanged();
        Presenter?.Owner?.RaiseCellPrepared(this, ColumnIndex, RowIndex);
    }
    protected void RaiseCellValueChanged()
    {
        if (_prepared && !IsEditing && !_rebinding && RowIndex >= 0 && ColumnIndex >= 0)
            Presenter?.Owner?.RaiseCellValueChanged(this, ColumnIndex, RowIndex);
    }
    protected virtual void ClearContent()
    {
        if (_content is not null) _content.Content = null;
        if (_text is not null) _text.Text = string.Empty;
        Visibility = Visibility.Collapsed;
    }
    private void UpdateContentKind()
    {
        if (_expander is not null)
        {
            _expander.Visibility = _expanderValue is null ? Visibility.Collapsed : Visibility.Visible;
            _expander.Margin = new(_indent * 20, 0, 0, 0);
        }
        if (_text is not null)
        {
            _text.Visibility = !UsesInnerCellControl && !IsEditing && _kind == CellKind.Text ? Visibility.Visible : Visibility.Collapsed;
            // Compare before setting: native DP setters box enums. Specialized
            // cell properties can also change independently of column options.
            var alignment = DisplayTextAlignment;
            var wrapping = DisplayTextWrapping;
            var trimming = DisplayTextTrimming;
            if (_text.TextAlignment != alignment) _text.TextAlignment = alignment;
            if (_text.TextWrapping != wrapping) _text.TextWrapping = wrapping;
            if (_text.TextTrimming != trimming) _text.TextTrimming = trimming;
        }
        if (_check is not null)
        {
            _check.Visibility = !UsesInnerCellControl && !IsEditing && _kind == CellKind.CheckBox ? Visibility.Visible : Visibility.Collapsed;
            if (_check.IsThreeState != DisplayCheckBoxIsThreeState) _check.IsThreeState = DisplayCheckBoxIsThreeState;
        }
        if (_content is not null)
        {
            _content.Visibility = !UsesInnerCellControl && !IsEditing && _kind == CellKind.Template ? Visibility.Visible : Visibility.Collapsed;
            if (!UsesInnerCellControl && !ReferenceEquals(_content.ContentTemplate, _template)) _content.ContentTemplate = _template;
        }
        if (_editorHost is not null) _editorHost.Visibility = !UsesInnerCellControl && IsEditing && _editingTemplate is null ? Visibility.Visible : Visibility.Collapsed;
        if (_editContent is not null) _editContent.Visibility = !UsesInnerCellControl && IsEditing && _editingTemplate is not null ? Visibility.Visible : Visibility.Collapsed;
    }
    private void OnValueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_rebinding || !ReferenceEquals(sender, _value)) return;
        OnModelPropertyChanged(Model, e);
    }
    /// <summary>Observe the current model once; repeated calls by derived cells are safe.</summary>
    protected void SubscribeToModelChanges()
    {
        if (ReferenceEquals(_subscribedValue, _value)) return;
        UnsubscribeFromModelChanges();
        if (_value is { } value)
        {
            _subscribedValue = value;
            value.PropertyChanged += OnValueChanged;
        }
    }
    /// <summary>Stop observing the model without changing its ownership.</summary>
    protected void UnsubscribeFromModelChanges()
    {
        var subscribed = _subscribedValue;
        _subscribedValue = null;
        if (subscribed is not null) subscribed.PropertyChanged -= OnValueChanged;
    }
    protected virtual void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var realization = RealizationVersion;
        Presenter?.InvalidateRowHeight(RowIndex);
        UpdateValue();
        NotifyAutomationValueChanged();
        OwningCell?.NotifyAutomationValueChanged();
        Presenter?.Owner?.TryGetRow(RowIndex)?.NotifyAutomationStateChanged();
        if (realization == RealizationVersion && ReferenceEquals(sender, Model) &&
            (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(CellValue.Value)))
            (OwningCell ?? this).RaiseCellValueChanged();
    }
    private void NotifyAutomationValueChanged() => _automationPeer?.NotifyValueChanged();
    protected virtual string? DisplayText => _value?.DisplayText ?? Column?.FormatValue(_value?.Value);
    protected virtual TextAlignment DisplayTextAlignment => (_value?.TextOptions ?? Column?.TextOptions)?.Alignment ?? _templateTextAlignment;
    protected virtual TextWrapping DisplayTextWrapping => (_value?.TextOptions ?? Column?.TextOptions)?.Wrapping ?? _templateTextWrapping;
    protected virtual TextTrimming DisplayTextTrimming => (_value?.TextOptions ?? Column?.TextOptions)?.Trimming ?? _templateTextTrimming;
    protected virtual bool? DisplayCheckBoxValue => _value?.Value as bool?;
    protected virtual bool DisplayCheckBoxIsReadOnly => _value?.CanWrite != true;
    protected virtual bool DisplayCheckBoxIsThreeState => _value?.IsThreeState ?? Column?.IsThreeState == true;
    protected bool IsRebinding => _rebinding;
    protected void RefreshCellPresentation()
    {
        if (_rebinding) return;
        Presenter?.InvalidateRowHeight(RowIndex);
        UpdateContentKind();
        RenderValue();
        NotifyAutomationValueChanged();
    }
    protected void SetCellTemplates(DataTemplate? content, DataTemplate? editing)
    {
        if (!ReferenceEquals(_editingTemplate, editing)) CancelEdit();
        _template = content;
        _editingTemplate = editing;
        if (!_rebinding)
        {
            Presenter?.InvalidateRowHeight(RowIndex);
            UpdateContentKind();
        }
    }
    protected virtual void UpdateValue() => RenderValue();
    private void RenderValue()
    {
        _updating = true;
        try
        {
            if (!UsesInnerCellControl && _text is not null && _kind == CellKind.Text) _text.Text = DisplayText ?? string.Empty;
            if (!UsesInnerCellControl && _check is not null && _kind == CellKind.CheckBox)
            {
                _check.IsChecked = DisplayCheckBoxValue;
                _check.IsEnabled = !DisplayCheckBoxIsReadOnly;
            }
            if (!UsesInnerCellControl && _content is not null && _kind == CellKind.Template) _content.Content = _value?.Value;
            if (_expander is not null && _expanderValue is { } expanded)
            {
                // The button's retained template owns the glyph. No replacement
                // text/content child is created when expansion changes.
                if (_expander is TreeDataGridExpanderButton button) button.IsExpanded = expanded.IsExpanded;
                else VisualStateManager.GoToState(_expander, expanded.IsExpanded ? "Expanded" : "Collapsed", false);
                _expander.Opacity = expanded.ShowExpander ? 1 : 0;
                _expander.IsHitTestVisible = expanded.ShowExpander;
            }
        }
        finally { _updating = false; }
    }
    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_updating && !DisplayCheckBoxIsReadOnly && _check is not null) OnCheckBoxValueChanged(_check.IsChecked);
    }
    protected virtual void OnCheckBoxValueChanged(bool? value)
    {
        if (_value?.CanWrite == true) _value.Write(value);
    }
}
