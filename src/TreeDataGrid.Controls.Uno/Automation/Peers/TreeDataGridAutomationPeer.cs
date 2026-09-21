using System.Collections.Generic;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using TreeDataGridCore.Selection;

namespace Uno.Controls.Automation.Peers;

/// <summary>Exposes the same realized-row selection contract as the Avalonia control.</summary>
public class TreeDataGridAutomationPeer : FrameworkElementAutomationPeer, ISelectionProvider
{
    private bool _canSelectMultiple;
    public TreeDataGridAutomationPeer(TreeDataGrid owner) : base(owner) => _canSelectMultiple = CanSelectMultiple;
    public new TreeDataGrid Owner => (TreeDataGrid)base.Owner;
    private ITreeDataGridRowSelectionModel? Selection => Owner.IsLoaded
        ? Owner.Presentation?.Selection.Model as ITreeDataGridRowSelectionModel : null;
    public bool CanSelectMultiple => Selection?.SingleSelect == false;
    public bool IsSelectionRequired => false;
    protected override string GetClassNameCore() => nameof(TreeDataGrid);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataGrid;
    protected override object GetPatternCore(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Selection ? Selection is not null ? this : null! : base.GetPatternCore(patternInterface);
    public IRawElementProviderSimple[] GetSelection()
    {
        var result = new List<IRawElementProviderSimple>();
        if (Selection is { } selection && Owner.Presentation is { } presentation)
        {
            foreach (var index in selection.SelectedIndexes)
            {
                var rowIndex = presentation.Rows.ModelIndexToRowIndex(index);
                if (Owner.TryGetRow(rowIndex) is { } row && TreeDataGridRowAutomationPeer.IsRealized(row) &&
                    CreatePeerForElement(row) is { } peer)
                    result.Add(ProviderFromPeer(peer));
            }
        }
        return result.ToArray();
    }
    internal void NotifySelectionChanged()
    {
        var previous = _canSelectMultiple;
        _canSelectMultiple = CanSelectMultiple;
        if (previous != _canSelectMultiple)
            RaisePropertyChangedEvent(SelectionPatternIdentifiers.CanSelectMultipleProperty, previous, _canSelectMultiple);
        RaisePropertyChangedEvent(SelectionPatternIdentifiers.SelectionProperty, null!, null!);
    }
}
