// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Primitives;

namespace Avalonia.Controls.Automation.Peers;

public class TreeDataGridCheckBoxCellAutomationPeer : TreeDataGridCellAutomationPeer, IToggleProvider
{
    public TreeDataGridCheckBoxCellAutomationPeer(TreeDataGridCheckBoxCell owner)
        : base(owner)
    {
    }

    public new TreeDataGridCheckBoxCell Owner => (TreeDataGridCheckBoxCell)base.Owner;

    public ToggleState ToggleState => Owner.Value switch
    {
        true => ToggleState.On,
        false => ToggleState.Off,
        null => ToggleState.Indeterminate,
    };

    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.CheckBox;
    }

    public void Toggle()
    {
        EnsureEnabled();

        if (Owner.IsReadOnly)
        {
            throw new NotSupportedException("TreeDataGrid checkbox cells are read-only.");
        }

        Owner.Value = NextValue(Owner.Value, Owner.IsThreeState);
    }

    private static bool? NextValue(bool? value, bool isThreeState)
    {
        if (value.HasValue)
        {
            if (value.Value)
            {
                return isThreeState ? null : false;
            }

            return true;
        }

        return false;
    }
}
