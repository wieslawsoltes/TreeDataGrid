// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;

namespace Avalonia.Controls.Automation.Peers;

public class TreeDataGridColumnHeaderAutomationPeer : ContentControlAutomationPeer
{
    public TreeDataGridColumnHeaderAutomationPeer(TreeDataGridColumnHeader owner)
        : base(owner)
    {
    }

    public new TreeDataGridColumnHeader Owner => (TreeDataGridColumnHeader)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.HeaderItem;
    }

    protected override bool IsContentElementCore() => false;

    protected override bool IsControlElementCore() => true;
}
