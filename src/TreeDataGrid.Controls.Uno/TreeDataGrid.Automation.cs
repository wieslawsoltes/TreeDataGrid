using Microsoft.UI.Xaml.Automation.Peers;
using TreeDataGridCore.Selection;
using Uno.Controls.Automation.Peers;
using Uno.Controls.Primitives;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    private TreeDataGridAutomationPeer? _automationPeer;
    protected override AutomationPeer OnCreateAutomationPeer() => _automationPeer = new TreeDataGridAutomationPeer(this);
    private void NotifyAutomationSelectionChanged() => _automationPeer?.NotifySelectionChanged();

    internal void SelectAutomationRow(TreeDataGridRow row, bool exclusive, bool remove = false)
    {
        if (!TreeDataGridRowAutomationPeer.IsRealized(row) || _presentation is not { } presentation ||
            presentation.Selection.Model is not ITreeDataGridRowSelectionModel selection) return;
        var index = row.RowIndex;
        var modelIndex = presentation.Rows.RowIndexToModelIndex(index);
        var model = row.Model;
        var revision = row.Presenter!.Revision;
        if (QueryCancelSelection() || (EditingCell is not null && !CommitEdit())) return;
        // SelectionChanging and edit validation are user callbacks and may replace
        // the source or recycle this very container. Never act on its new model.
        if (!ReferenceEquals(_presentation, presentation) || !ReferenceEquals(presentation.Selection.Model, selection) ||
            !TreeDataGridRowAutomationPeer.IsRealized(row) || row.Presenter!.Revision != revision ||
            row.RowIndex != index || !ReferenceEquals(row.Model, model) ||
            !presentation.Rows.RowIndexToModelIndex(index).Equals(modelIndex)) return;
        if (remove) selection.Deselect(modelIndex);
        else if (exclusive) selection.SelectedIndex = modelIndex;
        else selection.Select(modelIndex);
    }
}
