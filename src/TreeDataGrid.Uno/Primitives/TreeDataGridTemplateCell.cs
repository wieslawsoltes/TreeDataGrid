using System;
using Uno.Controls.Models.TreeDataGrid;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Uno.Controls.Primitives;

public sealed class TreeDataGridTemplateCell : TreeDataGridCell
{
    private ContentPresenter? _displayContentPresenter;
    private ContentPresenter? _editingContentPresenter;
    private object? _content;
    private DataTemplate? _contentTemplate;
    private DataTemplate? _editingTemplate;

    public TreeDataGridTemplateCell()
    {
        DefaultStyleKey = typeof(TreeDataGridTemplateCell);
        LostFocus += OnLostFocus;
    }

    public override void Realize(TreeDataGrid owner, TreeDataGridElementFactory factory, ICell model, int columnIndex, int rowIndex)
    {
        if (model is not TemplateCell templateCell)
            throw new InvalidOperationException("TreeDataGridTemplateCell requires a TemplateCell model.");

        _content = templateCell.Value;
        _contentTemplate = templateCell.GetCellTemplate(this);
        _editingTemplate = templateCell.GetCellEditingTemplate?.Invoke(this);

        base.Realize(owner, factory, model, columnIndex, rowIndex);
        SyncVisuals();
    }

    public override void Unrealize()
    {
        _content = null;
        base.Unrealize();
        SyncVisuals();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _displayContentPresenter = GetTemplateChild("PART_DisplayContentPresenter") as ContentPresenter;
        _editingContentPresenter = GetTemplateChild("PART_EditingContentPresenter") as ContentPresenter;
        SyncVisuals();
        UpdateEditingState();
    }

    protected override void UpdateEditingState()
    {
        if (_displayContentPresenter is not null)
            _displayContentPresenter.Visibility = IsEditing ? Visibility.Collapsed : Visibility.Visible;

        if (_editingContentPresenter is not null)
        {
            _editingContentPresenter.Visibility = IsEditing ? Visibility.Visible : Visibility.Collapsed;

            if (IsEditing)
                _ = DispatcherQueue.TryEnqueue(FocusEditingContent);
        }
    }

    protected override void UpdateValueFromModel()
    {
        if (Model is TemplateCell templateCell)
        {
            _content = templateCell.Value;
            _contentTemplate = templateCell.GetCellTemplate(this);
            _editingTemplate = templateCell.GetCellEditingTemplate?.Invoke(this);
        }
        else
        {
            _content = null;
            _contentTemplate = null;
            _editingTemplate = null;
        }

        SyncVisuals();
    }

    private void SyncVisuals()
    {
        if (_displayContentPresenter is not null)
        {
            _displayContentPresenter.Content = _content;
            _displayContentPresenter.ContentTemplate = _contentTemplate;
        }

        if (_editingContentPresenter is not null)
        {
            _editingContentPresenter.Content = _content;
            _editingContentPresenter.ContentTemplate = _editingTemplate ?? _contentTemplate;
        }
    }

    private void FocusEditingContent()
    {
        if (!IsEditing)
            return;

        if (FindFirstFocusableDescendant(_editingContentPresenter) is Control control)
            control.Focus(FocusState.Programmatic);
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (!IsEditing)
            return;

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (XamlRoot is null)
            {
                EndEdit();
                return;
            }

            var focusedElement = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            if (!IsDescendantOf(focusedElement))
                EndEdit();
        });
    }

    private bool IsDescendantOf(DependencyObject? current)
    {
        while (current is not null)
        {
            if (ReferenceEquals(current, this))
                return true;

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static DependencyObject? FindFirstFocusableDescendant(DependencyObject? current)
    {
        if (current is Control control && control.IsTabStop)
            return control;

        if (current is null)
            return null;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(current); ++i)
        {
            var child = VisualTreeHelper.GetChild(current, i);
            var match = FindFirstFocusableDescendant(child);
            if (match is not null)
                return match;
        }

        return null;
    }
}
