// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.VisualTree;

namespace Avalonia.Controls.Automation.Peers;

public class TreeDataGridRowAutomationPeer : ControlAutomationPeer, IToggleProvider, IValueProvider,
    IExpandCollapseProvider, ISelectionItemProvider
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

    public ExpandCollapseState ExpandCollapseState => GetExpandCollapseState();

    public bool ShowsMenu => false;

    public bool IsSelected
    {
        get
        {
            if (TryGetModelIndex(out var modelIndex) &&
                GetRowSelectionModelOrNull() is { } selection)
            {
                return selection.IsSelected(modelIndex);
            }

            return false;
        }
    }

    public ISelectionProvider? SelectionContainer
    {
        get
        {
            if (GetOwningGridOrNull() is { } owner)
            {
                return GetOrCreate(owner).GetProvider<ISelectionProvider>();
            }

            return null;
        }
    }

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

    public void Expand()
    {
        EnsureEnabled();

        if (GetToggleExpanderOrNull() is { } expander)
        {
            expander.IsExpanded = true;
        }
    }

    public void Collapse()
    {
        EnsureEnabled();

        if (GetToggleExpanderOrNull() is { } expander)
        {
            expander.IsExpanded = false;
        }
    }

    public void SetValue(string? value)
    {
        throw new NotSupportedException("TreeDataGrid rows are read-only.");
    }

    public void AddToSelection()
    {
        EnsureEnabled();

        if (TryGetModelIndex(out var modelIndex) &&
            GetRowSelectionModelOrNull() is { } selection)
        {
            selection.Select(modelIndex);
        }
    }

    public void RemoveFromSelection()
    {
        EnsureEnabled();

        if (TryGetModelIndex(out var modelIndex) &&
            GetRowSelectionModelOrNull() is { } selection)
        {
            selection.Deselect(modelIndex);
        }
    }

    public void Select()
    {
        EnsureEnabled();

        if (TryGetModelIndex(out var modelIndex) &&
            GetRowSelectionModelOrNull() is { } selection)
        {
            selection.SelectedIndex = modelIndex;
        }
    }

    protected override object? GetProviderCore(Type providerType)
    {
        if ((providerType == typeof(IToggleProvider) || providerType == typeof(IExpandCollapseProvider)) &&
            GetToggleExpanderOrNull() is null)
        {
            return null;
        }

        if (providerType == typeof(ISelectionItemProvider) &&
            (!TryGetModelIndex(out _) || GetRowSelectionModelOrNull() is null))
        {
            return null;
        }

        return base.GetProviderCore(providerType);
    }

    private ExpandCollapseState GetExpandCollapseState()
    {
        if (GetToggleExpanderOrNull() is { } expander)
        {
            return expander.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        }

        return ExpandCollapseState.LeafNode;
    }

    private TreeDataGrid? GetOwningGridOrNull()
    {
        return Owner.FindAncestorOfType<TreeDataGrid>();
    }

    private ITreeSelectionModel? GetRowSelectionModelOrNull()
    {
        return GetOwningGridOrNull()?.RowSelection as ITreeSelectionModel;
    }

    private bool TryGetModelIndex(out IndexPath modelIndex)
    {
        modelIndex = default;

        var rows = Owner.Rows;
        var rowIndex = Owner.RowIndex;

        if (rows is null || rowIndex < 0 || rowIndex >= rows.Count)
        {
            return false;
        }

        if (rows[rowIndex] is IModelIndexableRow indexable)
        {
            modelIndex = indexable.ModelIndexPath;
            return true;
        }

        return false;
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
