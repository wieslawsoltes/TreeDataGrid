using Microsoft.UI.Xaml.Automation.Peers;

namespace Uno.Controls.Automation.Peers
{
    internal sealed class TreeDataGridAutomationPeer : FrameworkElementAutomationPeer
    {
        public TreeDataGridAutomationPeer(TreeDataGrid owner)
            : base(owner)
        {
        }

        protected override string GetClassNameCore() => nameof(TreeDataGrid);

        protected override AutomationControlType GetAutomationControlTypeCore()
            => AutomationControlType.DataGrid;
    }
}
