using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Uno.Controls.Primitives;

namespace Uno.Controls.Automation.Peers;

public class TreeDataGridColumnHeaderAutomationPeer(TreeDataGridColumnHeader owner) : ButtonAutomationPeer(owner)
{
    public new TreeDataGridColumnHeader Owner => (TreeDataGridColumnHeader)base.Owner;
    protected override string GetClassNameCore() => nameof(TreeDataGridColumnHeader);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.HeaderItem;
    protected override bool IsContentElementCore() => false;
    protected override bool IsControlElementCore() => owner.ColumnIndex >= 0 && owner.Visibility == Visibility.Visible;
    protected override bool IsOffscreenCore() => owner.ColumnIndex < 0 || owner.Visibility != Visibility.Visible || base.IsOffscreenCore();
    protected override object GetPatternCore(PatternInterface patternInterface) => owner.ColumnIndex >= 0 && owner.Visibility == Visibility.Visible && owner.IsLoaded
        ? base.GetPatternCore(patternInterface) : null!;
}
