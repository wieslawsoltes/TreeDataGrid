using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Uno.Controls.Presentation;
using Windows.System;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridCell
{
    public static readonly DependencyProperty IsEditingProperty = DependencyProperty.Register(
        nameof(IsEditing), typeof(bool), typeof(TreeDataGridCell), new PropertyMetadata(false));
    public static readonly DependencyProperty HasValidationErrorProperty = DependencyProperty.Register(
        nameof(HasValidationError), typeof(bool), typeof(TreeDataGridCell), new PropertyMetadata(false, OnStateChanged));
    private CellEditSession? _edit;
    private Grid? _editorHost;
    private TextBox? _editor;
    private ContentPresenter? _editContent;
    private DataTemplate? _editingTemplate;
    public bool IsEditing { get => (bool)GetValue(IsEditingProperty); private set => SetValue(IsEditingProperty, value); }
    public bool HasValidationError { get => (bool)GetValue(HasValidationErrorProperty); private set => SetValue(HasValidationErrorProperty, value); }
    public Exception? EditError => _edit?.Error;
    public string EditingText
    {
        get => _editor?.Text ?? string.Empty;
        set
        {
            if (!IsEditing || _editor is null) throw new InvalidOperationException("No text edit is active.");
            _editor.Text = value;
        }
    }
    public bool BeginEdit()
    {
        if (IsEditing) return true;
        if (_value is null || (!_value.CanEdit && _editingTemplate is null)) return false;
        ApplyTemplate();
        if (_editingTemplate is null)
        {
            if (_kind != CellKind.Text || _editorHost is null) return false;
            if (_editor is null)
            {
                _editor = new TextBox { MinWidth = 0, MinHeight = 0, Padding = new(6, 2, 6, 2) };
                _editor.KeyDown += OnEditorKeyDown;
                _editorHost.Children.Add(_editor);
            }
            _editor.Text = _value.Value?.ToString() ?? string.Empty;
        }
        else if (_editContent is null) return false;
        var value = _value;
        var model = RowModel;
        var edit = new CellEditSession(value, model, writeValue: _editingTemplate is null);
        // User BeginEdit can synchronously replace the source or this row before
        // the session has been attached to its control.
        if (!ReferenceEquals(_value, value) || !ReferenceEquals(RowModel, model))
        {
            edit.Cancel();
            return false;
        }
        _edit = edit;
        IsEditing = true;
        HasValidationError = false;
        UpdateContentKind();
        if (_editingTemplate is null)
        {
            UpdateLayout();
            _editor!.Focus(FocusState.Programmatic);
            _editor.SelectAll();
        }
        else if (_editContent is not null)
        {
            _editContent.ContentTemplate = _editingTemplate;
            _editContent.Content = RowModel;
            UpdateLayout();
            if (FocusManager.FindFirstFocusableElement(_editContent) is Control control)
                control.Focus(FocusState.Programmatic);
        }
        return true;
    }
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!IsEditing || XamlRoot is null) return;
        for (var current = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, this)) return;
        CommitEdit();
    }
    public bool CommitEdit()
    {
        if (_edit is not { } edit) return true;
        var result = edit.Commit(_editor?.Text);
        if (!ReferenceEquals(_edit, edit)) return result;
        if (!result)
        {
            HasValidationError = true;
            ToolTipService.SetToolTip(this, edit.Error?.Message);
            return false;
        }
        _edit = null;
        EndEditingVisuals();
        UpdateValue();
        return true;
    }
    public void CancelEdit()
    {
        if (_edit is not { } edit) return;
        _edit = null;
        try { edit.Cancel(); }
        finally { EndEditingVisuals(); }
    }
    private void EndEditingVisuals()
    {
        IsEditing = false;
        HasValidationError = false;
        ToolTipService.SetToolTip(this, null);
        if (_editor is not null) _editor.Text = string.Empty;
        if (_editContent is not null) _editContent.Content = null;
        UpdateContentKind();
    }
    private void OnEditorKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape) { CancelEdit(); FocusGrid(); e.Handled = true; }
        else if (e.Key == VirtualKey.Enter) { if (CommitEdit()) FocusGrid(); e.Handled = true; }
    }
    private void FocusGrid()
    {
        for (DependencyObject? current = this; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is TreeDataGrid grid) { grid.Focus(FocusState.Keyboard); break; }
    }
}
