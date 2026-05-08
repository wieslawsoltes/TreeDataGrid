using Uno.Controls.Primitives;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Uno.Controls.Automation.Peers
{
    internal sealed class TreeDataGridColumnHeaderAutomationPeer : ButtonAutomationPeer
    {
        public TreeDataGridColumnHeaderAutomationPeer(TreeDataGridColumnHeader owner)
            : base(owner)
        {
        }

        protected override string GetClassNameCore() => nameof(TreeDataGridColumnHeader);

        protected override AutomationControlType GetAutomationControlTypeCore()
            => AutomationControlType.HeaderItem;
    }
}
