using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Uno.Controls.Primitives;

namespace Uno.Controls.Automation.Peers;

/// <summary>
/// Native checkbox peer with the same specialized public owner contract as the
/// Avalonia peer. Toggle execution shares the cell peer's realization, disabled,
/// read-only and current-source checks; it never caches a recycled cell value.
/// </summary>
public class TreeDataGridCheckBoxCellAutomationPeer : TreeDataGridCellAutomationPeer, IToggleProvider
{
    public TreeDataGridCheckBoxCellAutomationPeer(TreeDataGridCheckBoxCell owner) : base(owner) { }
    public new TreeDataGridCheckBoxCell Owner => (TreeDataGridCheckBoxCell)base.Owner;
    public new ToggleState ToggleState => base.ToggleState;
    public new void Toggle() => base.Toggle();
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;
}
