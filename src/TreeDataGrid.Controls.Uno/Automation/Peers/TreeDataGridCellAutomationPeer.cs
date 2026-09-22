using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;

namespace Uno.Controls.Automation.Peers;

/// <summary>Queries the current realization; never caches a pooled cell's old row/value.</summary>
public class TreeDataGridCellAutomationPeer : FrameworkElementAutomationPeer,
    IValueProvider, IToggleProvider, IExpandCollapseProvider
{
    private readonly TreeDataGridCell owner;
    private string _lastValue;
    private bool _lastReadOnly;
    private ToggleState _lastToggle;
    private ExpandCollapseState _lastExpanded;
    public TreeDataGridCellAutomationPeer(TreeDataGridCell owner) : base(owner)
    {
        this.owner = owner;
        _lastValue = Value;
        _lastReadOnly = IsReadOnly;
        _lastToggle = ToggleState;
        _lastExpanded = ExpandCollapseState;
    }
    public new TreeDataGridCell Owner => (TreeDataGridCell)base.Owner;
    private bool Realized => owner.RowIndex >= 0 && owner.ColumnIndex >= 0 && owner.Visibility == Visibility.Visible &&
        owner.Presenter?.Owner is { IsLoaded: true } grid && grid.TryGetRow(owner.RowIndex) is { } row &&
        TreeDataGridRowAutomationPeer.IsRealized(row) && ReferenceEquals(row.TryGetCell(owner.ColumnIndex), owner.OwningCell ?? owner);
    protected override string GetClassNameCore() => owner switch
    {
        TreeDataGridTextCell => nameof(TreeDataGridTextCell),
        TreeDataGridCheckBoxCell => nameof(TreeDataGridCheckBoxCell),
        TreeDataGridTemplateCell => nameof(TreeDataGridTemplateCell),
        TreeDataGridExpanderCell => nameof(TreeDataGridExpanderCell),
        _ => nameof(TreeDataGridCell),
    };
    protected override AutomationControlType GetAutomationControlTypeCore() => owner.ViewModel?.ContentKind == CellKind.CheckBox
        ? AutomationControlType.CheckBox : AutomationControlType.DataItem;
    protected override bool IsContentElementCore() => Realized;
    protected override bool IsControlElementCore() => Realized;
    protected override bool IsOffscreenCore() => !Realized || base.IsOffscreenCore();
    // Text/checkbox/expander details are represented by this peer's patterns.
    // Preserve arbitrary template/editor content as real accessible children.
    protected override IList<AutomationPeer> GetChildrenCore() => Realized &&
        (owner.IsEditing || owner.ViewModel?.ContentKind == CellKind.Template) ? base.GetChildrenCore() : null!;
    protected override string GetNameCore()
    {
        if (!Realized) return string.Empty;
        var explicitName = base.GetNameCore();
        return !string.IsNullOrEmpty(explicitName) ? explicitName : Value;
    }
    protected override object? GetPatternCore(PatternInterface patternInterface)
    {
        if (!Realized) return null;
        return patternInterface switch
        {
            PatternInterface.Value when owner.ViewModel?.ContentKind == CellKind.Text => this,
            PatternInterface.Toggle when owner.ViewModel?.ContentKind == CellKind.CheckBox => this,
            PatternInterface.ExpandCollapse when owner.Value is ExpanderCellValue => this,
            _ => base.GetPatternCore(patternInterface),
        };
    }
    public bool IsReadOnly => !Realized || owner.Value?.CanWrite != true || owner.GetEditingTarget() is TreeDataGridCheckBoxCell { IsReadOnly: true };
    public string Value => Realized ? owner.Value?.DisplayText ?? owner.Column?.FormatValue(owner.Value?.Value) ?? string.Empty : string.Empty;
    public void SetValue(string value)
    {
        EnsureUsable();
        if (IsReadOnly || owner.ViewModel?.ContentKind != CellKind.Text)
            throw new InvalidOperationException("This cell is read-only.");
        if (owner.IsEditing)
            throw new InvalidOperationException("Commit or cancel the active cell edit before setting its value.");
        owner.Value!.Write(value);
    }
    public ToggleState ToggleState => owner.Value?.Value switch
    {
        true => ToggleState.On,
        false => ToggleState.Off,
        _ => ToggleState.Indeterminate,
    };
    public void Toggle()
    {
        EnsureUsable();
        if (IsReadOnly || owner.ViewModel?.ContentKind != CellKind.CheckBox)
            throw new InvalidOperationException("This cell cannot be toggled.");
        bool? next = owner.Value!.Value switch
        {
            false => true,
            true => (owner.GetEditingTarget() is TreeDataGridCheckBoxCell check ? check.IsThreeState : owner.ViewModel.IsThreeState ?? owner.Column?.IsThreeState == true) ? null : false,
            _ => false,
        };
        owner.Value.Write(next);
    }
    public ExpandCollapseState ExpandCollapseState => owner.Value is ExpanderCellValue { ShowExpander: true } expander
        ? expander.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed
        : ExpandCollapseState.LeafNode;
    public void Expand() => SetExpanded(true);
    public void Collapse() => SetExpanded(false);
    private void SetExpanded(bool expanded)
    {
        EnsureUsable();
        if (owner.Value is not ExpanderCellValue { ShowExpander: true } expander)
            throw new InvalidOperationException("This cell has no expandable children.");
        expander.IsExpanded = expanded;
    }
    private void EnsureUsable()
    {
        if (!Realized) throw new InvalidOperationException("The cell is not currently realized.");
        if (!owner.IsEnabled || owner.Presenter?.Owner?.IsEnabled == false) throw new ElementNotEnabledException();
    }

    // No model subscriptions or cached row/value references. Called only for a
    // peer already requested by accessibility, through the existing view events.
    internal void NotifyValueChanged()
    {
        var value = Value;
        var readOnly = IsReadOnly;
        var toggle = ToggleState;
        var expanded = ExpandCollapseState;
        var oldValue = _lastValue;
        var oldReadOnly = _lastReadOnly;
        var oldToggle = _lastToggle;
        var oldExpanded = _lastExpanded;
        _lastValue = value;
        _lastReadOnly = readOnly;
        _lastToggle = toggle;
        _lastExpanded = expanded;
        if (oldValue != value) RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldValue, value);
        if (oldReadOnly != readOnly) RaisePropertyChangedEvent(ValuePatternIdentifiers.IsReadOnlyProperty, oldReadOnly, readOnly);
        if (oldToggle != toggle) RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, oldToggle, toggle);
        if (oldExpanded != expanded) RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty, oldExpanded, expanded);
    }
}
