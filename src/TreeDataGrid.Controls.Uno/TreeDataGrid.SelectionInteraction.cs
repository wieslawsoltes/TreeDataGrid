using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Uno.Controls.Presentation;
using Uno.Controls.Selection;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    private ITreeDataGridSelectionInteraction? _selectionInteraction;
    internal ITreeDataGridSelectionInteraction? SelectionInteraction => _selectionInteraction;

    private void UpdateSelectionInteraction()
    {
        var revision = _presentationRevision;
        var next = _loaded ? _presentation?.SelectionInteraction : null;
        if (revision != _presentationRevision || ReferenceEquals(next, _selectionInteraction)) return;
        var previous = _selectionInteraction;
        _selectionInteraction = next;
        if (previous is not null) previous.SelectionChanged -= OnSelectionChanged;
        if (revision != _presentationRevision || !ReferenceEquals(next, _selectionInteraction)) return;
        if (next is not null) next.SelectionChanged += OnSelectionChanged;
        if (revision == _presentationRevision && ReferenceEquals(next, _selectionInteraction)) _presenter?.RefreshSelection();
    }

    private void OnPresentationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _presentation) &&
            (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(TreeDataGridPresentation.SelectionInteraction)))
            UpdateSelectionInteraction();
    }

    protected override void OnPreviewKeyDown(KeyRoutedEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (IsOwnInput(e.OriginalSource as DependencyObject)) _selectionInteraction?.OnPreviewKeyDown(this, e);
    }

    protected override void OnKeyUp(KeyRoutedEventArgs e)
    {
        base.OnKeyUp(e);
        if (IsOwnInput(e.OriginalSource as DependencyObject)) _selectionInteraction?.OnKeyUp(this, e);
    }
}
