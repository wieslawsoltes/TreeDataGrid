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
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, BooleanBoxes.Box(value)); }
    public bool IsCurrent { get => (bool)GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, BooleanBoxes.Box(value)); }
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
        if (!UpdateContentKind()) return;
        var realization = RealizationVersion;
        var revision = _contentLayoutRevision;
        UpdateValue();
        if (IsContentLayoutCurrent(realization, revision)) UpdateState();
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
        if (_unrealizing) throw new InvalidOperationException("Cell unrealization is in progress.");
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
        if (_unrealizing) throw new InvalidOperationException("Cell unrealization is in progress.");
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
        var kind = value.ContentKind;
        if (_unrealizing || RealizationVersion != realization) return;
        _kind = kind;
        // An expander wraps the inner content contract; the value remains the same
        // Core row and the native children stay attached while it is recycled.
        var inner = UsesInnerCellControl;
        if (_unrealizing || RealizationVersion != realization) return;
        if (!inner && kind == CellKind.Template && template is null)
            throw new InvalidOperationException($"No Uno cell template is registered for '{column.Model.PresentationKey}'.");
        var indent = ((_expanderValue?.Row ?? row) as IIndentedRow)?.Indent ?? 0;
        if (_unrealizing || RealizationVersion != realization) return;
        _indent = indent;
        _template = template;
        _editingTemplate = editingTemplate;
        // A nested style refresh may supersede layout without retiring this
        // realization. Finish its subscription/visibility, but never continue
        // applying the older layout's style values.
        UpdateContentKind();
        if (!ReferenceEquals(_value, value) || RealizationVersion != realization) return;
        SubscribeToModelChanges();
        if (!ReferenceEquals(_value, value) || RealizationVersion != realization) return;
        if (Visibility != Visibility.Visible) Visibility = Visibility.Visible;
        if (!ReferenceEquals(_value, value) || RealizationVersion != realization) return;
        if (!_rebinding) UpdateValue();
    }

    public virtual void Unrealize() => UnrealizeCore();

    internal void NotifyPrepared()
    {
        if (_prepared || _unrealizing || RowIndex < 0 || ColumnIndex < 0) return;
        _prepared = true;
        NotifyAutomationValueChanged();
        Presenter?.Owner?.RaiseCellPrepared(this, ColumnIndex, RowIndex);
    }
    protected void RaiseCellValueChanged()
    {
        if (_prepared && !_unrealizing && !IsEditing && !_rebinding && RowIndex >= 0 && ColumnIndex >= 0)
            Presenter?.Owner?.RaiseCellValueChanged(this, ColumnIndex, RowIndex);
    }
    protected virtual void ClearContent() => ClearBaseContent();

    private bool UpdateContentKind() => UpdateCurrentContentKind();

    private void OnValueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_unrealizing || _rebinding || !ReferenceEquals(sender, _value)) return;
        OnModelPropertyChanged(Model, e);
    }
    /// <summary>Observe the current model once; repeated calls by derived cells are safe.</summary>
    protected void SubscribeToModelChanges()
    {
        if (_unrealizing || ReferenceEquals(_subscribedValue, _value)) return;
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
        if (_unrealizing || realization != RealizationVersion || !ReferenceEquals(sender, Model)) return;
        NotifyAutomationValueChanged();
        if (_unrealizing || realization != RealizationVersion) return;
        OwningCell?.NotifyAutomationValueChanged();
        if (_unrealizing || realization != RealizationVersion) return;
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
        if (_rebinding || _unrealizing) return;
        var realization = RealizationVersion;
        Presenter?.InvalidateRowHeight(RowIndex);
        if (realization != RealizationVersion || !UpdateContentKind()) return;
        var revision = _contentLayoutRevision;
        RenderValue();
        if (IsContentLayoutCurrent(realization, revision)) NotifyAutomationValueChanged();
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
    private void RenderValue() => RenderCurrentValue();
    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_updating || _unrealizing || _check is not { } check) return;
        var realization = RealizationVersion;
        var readOnly = DisplayCheckBoxIsReadOnly;
        if (!readOnly && !_updating && !_unrealizing && realization == RealizationVersion && ReferenceEquals(_check, check))
            OnCheckBoxValueChanged(check.IsChecked);
    }
    protected virtual void OnCheckBoxValueChanged(bool? value) => WriteCurrentCheckBoxValue(value);
}
