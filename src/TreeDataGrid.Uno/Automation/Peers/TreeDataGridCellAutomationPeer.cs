using Uno.Controls.Primitives;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Uno.Controls.Automation.Peers
{
    internal class TreeDataGridCellAutomationPeer : FrameworkElementAutomationPeer
    {
        public TreeDataGridCellAutomationPeer(TreeDataGridCell owner)
            : base(owner)
        {
        }

        protected override string GetClassNameCore() => nameof(TreeDataGridCell);

        protected override string GetNameCore()
        {
            if (Owner is not TreeDataGridCell owner)
                return base.GetNameCore();

            return owner.Model?.Value?.ToString() ?? $"Cell {owner.ColumnIndex},{owner.RowIndex}";
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
            => AutomationControlType.DataItem;
    }
}
