using System;
using System.Globalization;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Uno.Controls.Presentation;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridCell
{
    private bool _startingEdit;
    private int _startingEditRealization;

    private bool BeginEditForCurrentRealization()
    {
        var realization = RealizationVersion;
        // During visual cleanup IsEditing can still be true although its session
        // is already retired. Do not report that transition as an active edit.
        if (IsEditing) return realization == RealizationVersion && _edit?.IsActive == true;
        if (_startingEdit && _startingEditRealization == realization) return false;
        if (_value is not { } value) return false;
        var model = RowModel;
        var template = _editingTemplate;
        var wasStarting = _startingEdit;
        var previousRealization = _startingEditRealization;
        _startingEdit = true;
        _startingEditRealization = realization;
        CellEditSession? session = null;
        Exception? failure = null;
        var completed = false;
        try
        {
            // Every accessor here may execute application code, including
            // recycling to the same cell model. Identity alone is insufficient.
            var canEdit = value.CanEdit;
            if (!IsCurrent() || (!canEdit && template is null)) return false;
            ApplyTemplate();
            if (!IsCurrent()) return false;

            TextBox? editor = null;
            ContentPresenter? editContent = null;
            if (template is null)
            {
                if (_kind != CellKind.Text || (_editor is null && _editorHost is null)) return false;
                if (_editor is null)
                {
                    var host = _editorHost!;
                    editor = new TextBox { MinWidth = 0, MinHeight = 0, Padding = new(6, 2, 6, 2) };
                    editor.KeyDown += OnEditorKeyDown;
                    _editor = editor;
                    host.Children.Add(editor);
                    if (!IsCurrent() || !ReferenceEquals(_editor, editor)) return false;
                }
                else editor = _editor;

                var raw = value.Value;
                if (!IsCurrent()) return false;
                var options = value.TextOptions;
                if (!IsCurrent()) return false;
                if (options is null)
                {
                    options = Column?.TextOptions;
                    if (!IsCurrent()) return false;
                }
                var text = Convert.ToString(raw, options?.Culture ?? CultureInfo.CurrentCulture) ?? string.Empty;
                if (!IsCurrent() || !ReferenceEquals(_editor, editor)) return false;
                editor.Text = text;
                if (!IsCurrent() || !ReferenceEquals(_editor, editor)) return false;
            }
            else
            {
                editContent = _editContent;
                if (editContent is null) return false;
            }

            var target = value.EditTarget;
            if (!IsCurrent() || _edit is not null) return false;
            session = new CellEditSession(value, target ?? model, writeValue: template is null);
            // BeginEdit may retire this realization and start a newer edit. The
            // old session is cancelled in finally, without touching the new one.
            if (!IsCurrent() || _edit is not null) return false;
            _edit = session;
            IsEditing = true;
            if (!IsCurrentEdit()) return false;
            HasValidationError = false;
            if (!IsCurrentEdit()) return false;
            UpdateContentKind();
            if (!IsCurrentEdit()) return false;
            if (template is null)
            {
                UpdateLayout();
                if (!IsCurrentEdit() || !ReferenceEquals(editor, _editor)) return false;
                editor!.Focus(FocusState.Programmatic);
                if (!IsCurrentEdit() || !ReferenceEquals(editor, _editor)) return false;
                editor.SelectAll();
            }
            else
            {
                editContent!.ContentTemplate = template;
                if (!IsCurrentEdit() || !ReferenceEquals(editContent, _editContent)) return false;
                var content = _kind == CellKind.Template ? value.Value : model;
                if (!IsCurrentEdit()) return false;
                editContent.Content = content;
                if (!IsCurrentEdit()) return false;
                UpdateLayout();
                if (!IsCurrentEdit() || !ReferenceEquals(editContent, _editContent)) return false;
                if (FocusManager.FindFirstFocusableElement(editContent) is Control control)
                {
                    if (!IsCurrentEdit()) return false;
                    control.Focus(FocusState.Programmatic);
                }
            }
            completed = IsCurrentEdit();
            return completed;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try
            {
                if (!completed && session is not null)
                {
                    var owned = ReferenceEquals(_edit, session);
                    if (owned) _edit = null;
                    Exception? cleanup = null;
                    try { session.Cancel(); }
                    catch (Exception error) { cleanup = error; }
                    // A template change aborts adoption but does not create a
                    // new realization. Clean up our state in that case too.
                    // Cancellation itself may install a newer edit/realization.
                    if (owned && IsSameRealization() && _edit is null)
                    {
                        try
                        {
                            if (EndEditingVisuals() && IsSameRealization() && _edit is null) UpdateValue();
                        }
                        catch (Exception error)
                        { cleanup = cleanup is null ? error : new AggregateException(cleanup, error); }
                    }
                    if (cleanup is not null)
                    {
                        if (failure is not null) throw new AggregateException(failure, cleanup);
                        ExceptionDispatchInfo.Capture(cleanup).Throw();
                    }
                }
            }
            finally
            {
                _startingEdit = wasStarting;
                _startingEditRealization = previousRealization;
            }
        }

        bool IsSameRealization() => realization == RealizationVersion && ReferenceEquals(_value, value) &&
            ReferenceEquals(RowModel, model);
        bool IsCurrent() => IsSameRealization() && ReferenceEquals(_editingTemplate, template);
        bool IsCurrentEdit() => IsCurrent() && ReferenceEquals(_edit, session);
    }
}
