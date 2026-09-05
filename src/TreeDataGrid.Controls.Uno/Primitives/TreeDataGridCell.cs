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
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(TreeDataGridCell), new PropertyMetadata(false, OnStateChanged));
    public static readonly DependencyProperty IsCurrentProperty = DependencyProperty.Register(
        nameof(IsCurrent), typeof(bool), typeof(TreeDataGridCell), new PropertyMetadata(false, OnStateChanged));
    private TextBlock? _text;
    private CheckBox? _check;
    private ContentPresenter? _content;
    private Button? _expander;
    private CellKind _kind;
    private DataTemplate? _template;
    private int _indent;
    private ExpanderCellValue? _expanderValue;
    private CellValue? _value;
    private bool _updating;
    private bool _rebinding;
    internal TreeDataGridRowsPresenter? Presenter { get; set; }

    public TreeDataGridCell()
    {
        DefaultStyleKey = typeof(TreeDataGridCell);
    }
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public bool IsCurrent { get => (bool)GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, value); }
    private static void OnStateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((TreeDataGridCell)sender).UpdateState();
    private void UpdateState()
    {
        VisualStateManager.GoToState(this, IsSelected ? "Selected" : "Unselected", false);
        VisualStateManager.GoToState(this, IsCurrent ? "Current" : "NotCurrent", false);
        VisualStateManager.GoToState(this, HasValidationError ? "Invalid" : "Valid", false);
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
        _check = GetTemplateChild("PART_CheckBox") as CheckBox;
        _content = GetTemplateChild("PART_Content") as ContentPresenter;
        _expander = GetTemplateChild("PART_Expander") as Button;
        _editorHost = GetTemplateChild("PART_EditorHost") as Grid;
        _editContent = GetTemplateChild("PART_EditingContent") as ContentPresenter;
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

    internal void UpdateIndexes(int row, int column) { RowIndex = row; ColumnIndex = column; }

    public virtual void Realize(CellColumn column, CellValue value, IRow row, int columnIndex, int rowIndex, DataTemplate? template, DataTemplate? editingTemplate = null)
    {
        Column = column;
        _value = value;
        _expanderValue = value as ExpanderCellValue;
        Row = row;
        RowModel = row.Model;
        ColumnIndex = columnIndex;
        RowIndex = rowIndex;
        _kind = column.ContentKind;
        // An expander wraps the inner content contract; the value remains the same
        // Core row and the native children stay attached while it is recycled.
        if (_kind == CellKind.Template && template is null)
            throw new InvalidOperationException($"No Uno cell template is registered for '{column.Model.PresentationKey}'.");
        _indent = (row as IIndentedRow)?.Indent ?? 0;
        _template = template;
        _editingTemplate = editingTemplate;
        UpdateContentKind();
        value.PropertyChanged += OnValueChanged;
        Visibility = Visibility.Visible;
        if (!_rebinding) UpdateValue();
    }

    public virtual void Unrealize()
    {
        try { CancelEdit(); }
        finally
        {
            if (_value is not null) _value.PropertyChanged -= OnValueChanged;
            _value = null;
            _expanderValue = null;
            Row = null;
            RowModel = null;
            RowIndex = ColumnIndex = -1;
            Column = null;
            IsSelected = IsCurrent = false;
            if (!_rebinding) ClearContent();
        }
    }
    private void ClearContent()
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
        if (_text is not null) _text.Visibility = !IsEditing && _kind == CellKind.Text ? Visibility.Visible : Visibility.Collapsed;
        if (_check is not null)
        {
            _check.Visibility = !IsEditing && _kind == CellKind.CheckBox ? Visibility.Visible : Visibility.Collapsed;
            _check.IsThreeState = Column?.IsThreeState == true;
        }
        if (_content is not null)
        {
            _content.Visibility = !IsEditing && _kind == CellKind.Template ? Visibility.Visible : Visibility.Collapsed;
            if (!ReferenceEquals(_content.ContentTemplate, _template)) _content.ContentTemplate = _template;
        }
        if (_editorHost is not null) _editorHost.Visibility = IsEditing && _editingTemplate is null ? Visibility.Visible : Visibility.Collapsed;
        if (_editContent is not null) _editContent.Visibility = IsEditing && _editingTemplate is not null ? Visibility.Visible : Visibility.Collapsed;
    }
    private void OnValueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_rebinding) return;
        Presenter?.InvalidateRowHeight(RowIndex);
        UpdateValue();
    }
    private void UpdateValue()
    {
        _updating = true;
        try
        {
            if (_text is not null && _kind == CellKind.Text) _text.Text = _value?.Value?.ToString() ?? string.Empty;
            if (_check is not null && _kind == CellKind.CheckBox)
            {
                _check.IsChecked = _value?.Value as bool?;
                _check.IsEnabled = _value?.CanEdit == true;
            }
            if (_content is not null && _kind == CellKind.Template) _content.Content = _value?.Value;
            if (_expander is not null && _expanderValue is { } expanded)
            {
                _expander.Content = expanded.IsExpanded ? "−" : "+";
                _expander.Opacity = expanded.ShowExpander ? 1 : 0;
                _expander.IsHitTestVisible = expanded.ShowExpander;
            }
        }
        finally { _updating = false; }
    }
    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_updating && _value?.CanEdit == true && _check is not null) _value.Write(_check.IsChecked);
    }
}
