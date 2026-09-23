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
        nameof(IsEditing), typeof(bool), typeof(TreeDataGridCell), new PropertyMetadata(false, OnStateChanged));
    public static readonly DependencyProperty HasValidationErrorProperty = DependencyProperty.Register(
        nameof(HasValidationError), typeof(bool), typeof(TreeDataGridCell), new PropertyMetadata(false, OnStateChanged));
    private CellEditSession? _edit;
    private Grid? _editorHost;
    private TextBox? _editor;
    private ContentPresenter? _editContent;
    private DataTemplate? _editingTemplate;
    protected bool UsesTextEditor => _editingTemplate is null;
    public virtual bool IsEditing { get => (bool)GetValue(IsEditingProperty); protected set => SetValue(IsEditingProperty, BooleanBoxes.Box(value)); }
    public virtual bool HasValidationError { get => (bool)GetValue(HasValidationErrorProperty); protected set => SetValue(HasValidationErrorProperty, BooleanBoxes.Box(value)); }
    public virtual Exception? EditError => _edit?.Error;
    public virtual string EditingText
    {
        get => _editor?.Text ?? string.Empty;
        set
        {
            if (!IsEditing || _editor is null) throw new InvalidOperationException("No text edit is active.");
            _editor.Text = value;
        }
    }
    public virtual bool BeginEdit() => BeginEditForCurrentRealization();
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!IsEditing || XamlRoot is null) return;
        for (var current = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, this)) return;
        CommitEdit();
    }
    public virtual bool CommitEdit()
    {
        if (_edit is not { } edit) return true;
        if (edit.IsCommitting) return false;
        var realization = RealizationVersion;
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
        if (realization == RealizationVersion) (OwningCell ?? this).RaiseCellValueChanged();
        return true;
    }
    /// <summary>Reference-compatible edit completion for derived cell controls.</summary>
    protected internal void EndEdit() => CommitEdit();
    public virtual void CancelEdit()
    {
        if (_edit is not { } edit) return;
        _edit = null;
        try { edit.Cancel(); }
        finally { EndEditingVisuals(); UpdateValue(); }
    }
    private void EndEditingVisuals()
    {
        // Clear a template-bound editor while still editing: the text cell's
        // Value facade must not write this cleanup value to the model.
        if (_editor is not null) _editor.Text = string.Empty;
        if (_editContent is not null) _editContent.Content = null;
        IsEditing = false;
        HasValidationError = false;
        ToolTipService.SetToolTip(this, null);
        UpdateContentKind();
    }
    private void OnEditorKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var realization = RealizationVersion;
        if (e.Key == VirtualKey.Escape) { CancelEdit(); ReturnFocus(realization); e.Handled = true; }
        else if (e.Key == VirtualKey.Enter) { if (CommitEdit()) ReturnFocus(realization); e.Handled = true; }
    }
    private void ReturnFocus(int realization)
    {
        if (Presenter?.Owner is { } grid) grid.ReturnFocusAfterEditing(this, realization);
        else if (RealizationVersion == realization && RowIndex >= 0 && !IsEditing) Focus(FocusState.Keyboard);
    }
}
