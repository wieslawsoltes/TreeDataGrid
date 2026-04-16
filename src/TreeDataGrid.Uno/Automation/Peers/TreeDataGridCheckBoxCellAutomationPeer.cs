using Uno.Controls.Primitives;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Uno.Controls.Automation.Peers
{
    internal sealed class TreeDataGridCheckBoxCellAutomationPeer : TreeDataGridCellAutomationPeer
    {
        public TreeDataGridCheckBoxCellAutomationPeer(TreeDataGridCheckBoxCell owner)
            : base(owner)
        {
        }

        protected override string GetClassNameCore() => nameof(TreeDataGridCheckBoxCell);

        protected override AutomationControlType GetAutomationControlTypeCore()
            => AutomationControlType.CheckBox;
    }
}
