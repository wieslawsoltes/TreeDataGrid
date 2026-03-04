using System;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;

namespace Avalonia.Controls.Automation.Peers;

public class TreeDataGridRowAutomationPeer : ControlAutomationPeer, IToggleProvider, IValueProvider
{
    public TreeDataGridRowAutomationPeer(TreeDataGridRow owner)
        : base(owner)
    {
    }

    public new TreeDataGridRow Owner => (TreeDataGridRow)base.Owner;

    public ToggleState ToggleState
    {
        get
        {
            if (GetToggleExpanderOrNull() is { } expander)
            {
                return expander.IsExpanded ? ToggleState.On : ToggleState.Off;
            }

            return ToggleState.Indeterminate;
        }
    }

    public bool IsReadOnly => true;

    public string? Value
    {
        get
        {
            return Owner.Model?.ToString();
        }
    }

    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.TreeItem;
    }

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public void Toggle()
    {
        EnsureEnabled();

        if (GetToggleExpanderOrNull() is { } expander)
        {
            expander.IsExpanded = !expander.IsExpanded;
        }
    }

    public void SetValue(string? value)
    {
        throw new NotSupportedException("TreeDataGrid rows are read-only.");
    }

    protected override object? GetProviderCore(Type providerType)
    {
        if (providerType == typeof(IToggleProvider) && GetToggleExpanderOrNull() is null)
        {
            return null;
        }

        return base.GetProviderCore(providerType);
    }

    private IExpander? GetToggleExpanderOrNull()
    {
        var rows = Owner.Rows;
        var rowIndex = Owner.RowIndex;

        if (rows is null || rowIndex < 0 || rowIndex >= rows.Count)
        {
            return null;
        }

        if (rows[rowIndex] is IExpander { ShowExpander: true } expander)
        {
            return expander;
        }

        return null;
    }
}
