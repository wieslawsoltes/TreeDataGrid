using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using Uno.Controls.Primitives;

namespace Uno.Controls.Automation.Peers;

public class TreeDataGridRowAutomationPeer : FrameworkElementAutomationPeer,
    IValueProvider, IToggleProvider, IExpandCollapseProvider, ISelectionItemProvider
{
    private bool _selected;
    private ExpandCollapseState _expanded;
    private string _value;
    public TreeDataGridRowAutomationPeer(TreeDataGridRow owner) : base(owner)
    { _selected = IsSelected; _expanded = ExpandCollapseState; _value = Value; }
    public new TreeDataGridRow Owner => (TreeDataGridRow)base.Owner;
    internal static bool IsRealized(TreeDataGridRow row) => row.RowIndex >= 0 &&
        row.Visibility == Visibility.Visible && row.Presenter?.Owner is { IsLoaded: true } grid &&
        grid.Presentation is { } presentation && row.RowIndex < presentation.Rows.Count &&
        ReferenceEquals(row.Presentation, presentation) && ReferenceEquals(grid.TryGetRow(row.RowIndex), row);
    private ITreeDataGridRowSelectionModel? Selection => IsRealized(Owner)
        ? Owner.Presentation?.Selection.Model as ITreeDataGridRowSelectionModel : null;
    private IExpander? Expander => IsRealized(Owner) && Owner.Rows is { } rows && Owner.RowIndex < rows.Count &&
        rows[Owner.RowIndex] is IExpander { ShowExpander: true } expander ? expander : null;
    public bool IsReadOnly => true;
    /// <summary>Like the reference provider, row expansion exposes hierarchy, not a menu.</summary>
    public bool ShowsMenu => false;
    public string Value => IsRealized(Owner) ? Owner.Model?.ToString() ?? string.Empty : string.Empty;
    public bool IsSelected => Selection is { } selection &&
        selection.IsSelected(Owner.Rows!.RowIndexToModelIndex(Owner.RowIndex));
    public IRawElementProviderSimple SelectionContainer => Selection is not null && Owner.Presenter?.Owner is { } grid
        ? ProviderFromPeer(CreatePeerForElement(grid)) : null!;
    public ExpandCollapseState ExpandCollapseState => Expander is { } expander
        ? expander.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed : ExpandCollapseState.LeafNode;
    public ToggleState ToggleState => ToToggleState(ExpandCollapseState);
    protected override string GetClassNameCore() => nameof(TreeDataGridRow);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;
    protected override bool IsContentElementCore() => IsRealized(Owner);
    protected override bool IsControlElementCore() => IsRealized(Owner);
    protected override bool IsOffscreenCore() => !IsRealized(Owner) || base.IsOffscreenCore();
    protected override string GetNameCore()
    {
        if (!IsRealized(Owner)) return string.Empty;
        var name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? Value : name;
    }
    protected override IList<AutomationPeer> GetChildrenCore()
    {
        if (!IsRealized(Owner) || Owner.CellsPresenter is not { } cells) return null!;
        return cells.RealizedCells.OrderBy(x => x.ColumnIndex)
            .Where(x => x.RowIndex == Owner.RowIndex && x.Visibility == Visibility.Visible)
            .Select(CreatePeerForElement).Where(x => x is not null).ToList();
    }
    protected override object? GetPatternCore(PatternInterface patternInterface)
    {
        if (!IsRealized(Owner)) return null;
        return patternInterface switch
        {
            PatternInterface.Value => this,
            PatternInterface.SelectionItem when Selection is not null => this,
            PatternInterface.Toggle or PatternInterface.ExpandCollapse when Expander is not null => this,
            _ => base.GetPatternCore(patternInterface),
        };
    }
    public void SetValue(string value) => throw new NotSupportedException("TreeDataGrid rows are read-only.");
    public void Select() { EnsureUsable(); Owner.Presenter!.Owner!.SelectAutomationRow(Owner, exclusive: true); }
    public void AddToSelection() { EnsureUsable(); Owner.Presenter!.Owner!.SelectAutomationRow(Owner, exclusive: false); }
    public void RemoveFromSelection() { EnsureUsable(); Owner.Presenter!.Owner!.SelectAutomationRow(Owner, exclusive: false, remove: true); }
    public void Expand() { EnsureUsable(); if (Expander is { } expander) expander.IsExpanded = true; }
    public void Collapse() { EnsureUsable(); if (Expander is { } expander) expander.IsExpanded = false; }
    public void Toggle() { EnsureUsable(); if (Expander is { } expander) expander.IsExpanded = !expander.IsExpanded; }
    private void EnsureUsable()
    {
        if (!IsRealized(Owner)) throw new InvalidOperationException("The row is not currently realized.");
        if (!Owner.IsEnabled || Owner.Presenter?.Owner?.IsEnabled == false) throw new ElementNotEnabledException();
    }
    private static ToggleState ToToggleState(ExpandCollapseState state) => state switch
    {
        ExpandCollapseState.Expanded => ToggleState.On,
        ExpandCollapseState.Collapsed => ToggleState.Off,
        _ => ToggleState.Indeterminate,
    };
    internal void NotifyStateChanged()
    {
        var selected = IsSelected;
        var expanded = ExpandCollapseState;
        var previousSelected = _selected;
        var previousExpanded = _expanded;
        var value = Value;
        var previousValue = _value;
        _selected = selected;
        _expanded = expanded;
        _value = value;
        if (previousValue != value)
            RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, previousValue, value);
        if (previousSelected != selected)
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, previousSelected, selected);
        if (previousExpanded != expanded)
        {
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty, previousExpanded, expanded);
            RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, ToToggleState(previousExpanded), ToToggleState(expanded));
        }
    }
}
