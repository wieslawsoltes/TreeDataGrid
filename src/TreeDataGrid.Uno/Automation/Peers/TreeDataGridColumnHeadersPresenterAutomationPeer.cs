using Uno.Controls.Primitives;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Uno.Controls.Automation.Peers
{
    internal sealed class TreeDataGridColumnHeadersPresenterAutomationPeer : FrameworkElementAutomationPeer
    {
        public TreeDataGridColumnHeadersPresenterAutomationPeer(TreeDataGridColumnHeadersPresenter owner)
            : base(owner)
        {
        }

        protected override string GetClassNameCore() => nameof(TreeDataGridColumnHeadersPresenter);

        protected override AutomationControlType GetAutomationControlTypeCore()
            => AutomationControlType.Header;
    }
}
