using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Uno.Controls.Primitives;

namespace Uno.Controls.Automation.Peers;

public class TreeDataGridColumnHeadersPresenterAutomationPeer(TreeDataGridColumnHeadersPresenter owner) : FrameworkElementAutomationPeer(owner)
{
    public new TreeDataGridColumnHeadersPresenter Owner => (TreeDataGridColumnHeadersPresenter)base.Owner;
    protected override string GetClassNameCore() => nameof(TreeDataGridColumnHeadersPresenter);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Header;
    protected override bool IsContentElementCore() => false;
    protected override bool IsControlElementCore() => owner.IsLoaded && owner.Visibility == Visibility.Visible &&
        (owner.Owner is null || owner.Owner is { IsLoaded: true, ShowColumnHeaders: true });
    protected override IList<AutomationPeer> GetChildrenCore() => IsControlElementCore()
        ? owner.RealizedHeaders.Where(x => x.ColumnIndex >= 0 && x.Visibility == Visibility.Visible)
            .OrderBy(x => x.ColumnIndex).Select(CreatePeerForElement).Where(x => x is not null).ToList() : null!;
}
