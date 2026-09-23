using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridCell
{
    private bool _unrealizing;
    internal bool IsUnrealizing => _unrealizing;
    internal virtual void ClearRetiredModelContext() { }

    private void UnrealizeCore()
    {
        if (_unrealizing) return;
        _unrealizing = true;
        // Retire in-flight getters even when a caller later realizes the same
        // model at the same indexes. Model identity is not a lifetime token.
        unchecked { ++RealizationVersion; }
        var adapter = _standaloneAdapter;
        _standaloneAdapter = null;
        var preserveContent = _rebinding;
        List<Exception>? errors = null;
        try
        {
            if (_prepared)
            {
                _prepared = false;
                try { Presenter?.Owner?.RaiseCellClearing(this, ColumnIndex, RowIndex); }
                catch (Exception error) { (errors ??= new()).Add(error); }
            }
            try { CancelEdit(); }
            catch (Exception error)
            {
                (errors ??= new()).Add(error);
                // A model/DP callback can interrupt ordinary edit cancellation.
                // Continue native editor cleanup without invoking Cancel twice.
                _edit = null;
                try { if (_editor is not null) _editor.Text = string.Empty; }
                catch (Exception cleanup) { errors.Add(cleanup); }
                try { if (_editContent is not null) _editContent.Content = null; }
                catch (Exception cleanup) { errors.Add(cleanup); }
                try { IsEditing = false; }
                catch (Exception cleanup) { errors.Add(cleanup); }
                try { HasValidationError = false; }
                catch (Exception cleanup) { errors.Add(cleanup); }
                try { ToolTipService.SetToolTip(this, null); }
                catch (Exception cleanup) { errors.Add(cleanup); }
            }
            try { UnsubscribeFromModelChanges(); }
            catch (Exception error) { (errors ??= new()).Add(error); }

            _value = null;
            _expanderValue = null;
            Row = null;
            RowModel = null;
            OwningRow = null;
            RowIndex = ColumnIndex = -1;
            Column = null;
            ContainerFactory = null;
            // Preserve the previous chained setter's right-to-left ordering,
            // but do not let one failed native callback skip the next property.
            try { IsCurrent = false; }
            catch (Exception error) { (errors ??= new()).Add(error); }
            try { IsSelected = false; }
            catch (Exception error) { (errors ??= new()).Add(error); }
            try { IsRowSelected = false; }
            catch (Exception error) { (errors ??= new()).Add(error); }
            if (!preserveContent)
            {
                try { ClearContent(); }
                catch (Exception error)
                {
                    (errors ??= new()).Add(error);
                    // A custom ClearContent can throw before delegating to base.
                    // Retire the base-owned visual payload and visibility anyway.
                    try { ClearBaseContent(); }
                    catch (Exception cleanup) { errors.Add(cleanup); }
                }
            }
            try { NotifyAutomationValueChanged(); }
            catch (Exception error) { (errors ??= new()).Add(error); }
            try { adapter?.Dispose(); }
            catch (Exception error) { (errors ??= new()).Add(error); }
            try { ClearRetiredModelContext(); }
            catch (Exception error) { (errors ??= new()).Add(error); }
        }
        finally { _unrealizing = false; }
        ThrowRetirementErrors(errors);
    }

    private void ClearBaseContent()
    {
        List<Exception>? errors = null;
        try { if (_content is not null) _content.Content = null; }
        catch (Exception error) { (errors ??= new()).Add(error); }
        try { if (_text is not null) _text.Text = string.Empty; }
        catch (Exception error) { (errors ??= new()).Add(error); }
        try { Visibility = Visibility.Collapsed; }
        catch (Exception error) { (errors ??= new()).Add(error); }
        ThrowRetirementErrors(errors);
    }

    private static void ThrowRetirementErrors(List<Exception>? errors)
    {
        if (errors is { Count: 1 })
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException("Cell retirement failed.", errors);
    }
}
