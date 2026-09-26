using Uno.Controls.Presentation;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    private void ObserveDetailedSelection(TreeDataGridPresentation presentation, bool subscribe)
    {
        if (subscribe)
        {
            presentation.Selection.SelectionChanged += OnDetailedSelectionChanged;
            presentation.NativeSelectionChanged += OnPresentationSelectionChanged;
        }
        else
        {
            presentation.Selection.SelectionChanged -= OnDetailedSelectionChanged;
            presentation.NativeSelectionChanged -= OnPresentationSelectionChanged;
        }
    }

    private void OnPresentationSelectionChanged(object? sender, TreeDataGridSelectionChangedEventArgs args)
    {
        if (!_loaded || !ReferenceEquals(sender, _presentation)) return;
        var revision = _presentationRevision;
        _presenter?.RefreshSelection();
        if (_loaded && revision == _presentationRevision && ReferenceEquals(sender, _presentation))
            _selectionChanged?.Invoke(this, args);
    }
}
