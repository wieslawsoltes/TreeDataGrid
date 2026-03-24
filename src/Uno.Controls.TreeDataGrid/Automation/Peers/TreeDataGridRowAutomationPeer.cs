using Avalonia.Controls.Primitives;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Avalonia.Controls.Automation.Peers
{
    internal sealed class TreeDataGridRowAutomationPeer : FrameworkElementAutomationPeer
    {
        public TreeDataGridRowAutomationPeer(TreeDataGridRow owner)
            : base(owner)
        {
        }

        protected override string GetClassNameCore() => nameof(TreeDataGridRow);

        protected override string GetNameCore()
        {
            if (Owner is not TreeDataGridRow owner)
                return base.GetNameCore();

            return owner.Model?.ToString() ?? $"Row {owner.RowIndex}";
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
            => AutomationControlType.DataItem;
    }
}
