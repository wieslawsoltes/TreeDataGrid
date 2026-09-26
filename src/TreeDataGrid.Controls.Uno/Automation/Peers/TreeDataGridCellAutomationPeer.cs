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
    private int _operationRevision;
    private int _notificationRevision;

    public TreeDataGridCellAutomationPeer(TreeDataGridCell owner) : base(owner)
    {
        this.owner = owner;
        _lastValue = Value;
        _lastReadOnly = IsReadOnly;
        _lastToggle = ToggleState;
        _lastExpanded = ExpandCollapseState;
    }
    public new TreeDataGridCell Owner => (TreeDataGridCell)base.Owner;
    private bool Realized
    {
        get
        {
            if (owner.RowIndex < 0 || owner.ColumnIndex < 0 || owner.Visibility != Visibility.Visible ||
                owner.Presenter?.Owner is not { IsLoaded: true } grid || grid.TryGetRow(owner.RowIndex) is not { } row ||
                !TreeDataGridRowAutomationPeer.IsRealized(row)) return false;
            var outer = owner;
            while (outer.OwningCell is { } parent) outer = parent;
            return ReferenceEquals(row.TryGetCell(owner.ColumnIndex), outer);
        }
    }
    protected override string GetClassNameCore() => owner switch
    {
        TreeDataGridTextCell => nameof(TreeDataGridTextCell),
        TreeDataGridCheckBoxCell => nameof(TreeDataGridCheckBoxCell),
        TreeDataGridTemplateCell => nameof(TreeDataGridTemplateCell),
        TreeDataGridExpanderCell => nameof(TreeDataGridExpanderCell),
        _ => nameof(TreeDataGridCell),
    };
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        if (!TryCapture(out var state)) return AutomationControlType.DataItem;
        var kind = state.Value.ContentKind;
        return Current(state) && kind == CellKind.CheckBox ? AutomationControlType.CheckBox : AutomationControlType.DataItem;
    }
    protected override bool IsContentElementCore() => Realized;
    protected override bool IsControlElementCore() => Realized;
    protected override bool IsOffscreenCore() => !Realized || base.IsOffscreenCore();
    // Preserve arbitrary template/editor content as real accessible children.
    protected override IList<AutomationPeer> GetChildrenCore()
    {
        if (!TryCapture(out var state)) return null!;
        var kind = state.Value.ContentKind;
        return Current(state) && (owner.IsEditing || kind == CellKind.Template) ? base.GetChildrenCore() : null!;
    }
    protected override string GetNameCore()
    {
        if (!TryCapture(out var state)) return string.Empty;
        var explicitName = base.GetNameCore();
        if (!Current(state)) return string.Empty;
        return !string.IsNullOrEmpty(explicitName) ? explicitName : Value;
    }
    protected override object? GetPatternCore(PatternInterface patternInterface)
    {
        if (!TryCapture(out var state)) return null;
        var kind = state.Value.ContentKind;
        if (!Current(state)) return null;
        return patternInterface switch
        {
            PatternInterface.Value when kind == CellKind.Text => this,
            PatternInterface.Toggle when kind == CellKind.CheckBox => this,
            PatternInterface.ExpandCollapse when state.Value is ExpanderCellValue => this,
            _ => base.GetPatternCore(patternInterface),
        };
    }

    public bool IsReadOnly => !TryCapture(out var state) || ReadOnly(state);
    private bool ReadOnly(in Observation state)
    {
        var writable = state.Value.CanWrite;
        if (!Current(state)) return true;
        var readOnly = state.Target is TreeDataGridCheckBoxCell { IsReadOnly: true };
        return !Current(state) || !writable || readOnly;
    }
    public string Value
    {
        get
        {
            if (!TryCapture(out var state)) return string.Empty;
            var text = state.Value.DisplayText;
            if (!Current(state)) return string.Empty;
            if (text is not null) return text;
            var value = state.Value.Value;
            if (!Current(state)) return string.Empty;
            var formatted = state.Column?.FormatValue(value) ?? string.Empty;
            return Current(state) ? formatted : string.Empty;
        }
    }
    public void SetValue(string value)
    {
        EnsureUsable();
        unchecked { ++_operationRevision; }
        if (!TryCapture(out var state)) return;
        var readOnly = ReadOnly(state);
        if (!Current(state)) return;
        EnsureEnabled(state);
        if (readOnly) throw new InvalidOperationException("This cell is read-only.");
        var kind = state.Value.ContentKind;
        if (!Current(state)) return;
        if (kind != CellKind.Text) throw new InvalidOperationException("This cell is read-only.");
        // ContentKind is virtual application metadata. Even a silent permission
        // change in that getter must not authorize a write based on an old read.
        readOnly = ReadOnly(state);
        if (!Current(state)) return;
        EnsureEnabled(state);
        if (readOnly) throw new InvalidOperationException("This cell is read-only.");
        if (owner.IsEditing || state.Target.IsEditing)
            throw new InvalidOperationException("Commit or cancel the active cell edit before setting its value.");
        // Write only the captured value. Getters may recycle this same container,
        // replace the source or perform a newer nested automation action.
        state.Value.Write(value);
    }
    public ToggleState ToggleState
    {
        get
        {
            if (!TryCapture(out var state)) return ToggleState.Indeterminate;
            var value = state.Value.Value;
            return Current(state) ? ToToggleState(value) : ToggleState.Indeterminate;
        }
    }
    private static ToggleState ToToggleState(object? value) => value switch
    {
        true => ToggleState.On,
        false => ToggleState.Off,
        _ => ToggleState.Indeterminate,
    };
    public void Toggle()
    {
        EnsureUsable();
        unchecked { ++_operationRevision; }
        if (!TryCapture(out var state)) return;
        var readOnly = ReadOnly(state);
        if (!Current(state)) return;
        EnsureEnabled(state);
        if (readOnly) throw new InvalidOperationException("This cell cannot be toggled.");
        var kind = state.Value.ContentKind;
        if (!Current(state)) return;
        if (kind != CellKind.CheckBox) throw new InvalidOperationException("This cell cannot be toggled.");
        var current = state.Value.Value;
        if (!Current(state)) return;
        bool? next;
        if (current is true)
        {
            var threeState = state.Target is TreeDataGridCheckBoxCell check
                ? check.IsThreeState : state.Value.IsThreeState ?? state.Column?.IsThreeState == true;
            if (!Current(state)) return;
            next = threeState ? null : false;
        }
        else next = current is false;
        readOnly = ReadOnly(state);
        if (!Current(state)) return;
        EnsureEnabled(state);
        if (readOnly) throw new InvalidOperationException("This cell cannot be toggled.");
        state.Value.Write(next);
    }
    public ExpandCollapseState ExpandCollapseState
    {
        get
        {
            if (!TryCapture(out var state) || state.Value is not ExpanderCellValue expander)
                return ExpandCollapseState.LeafNode;
            var show = expander.ShowExpander;
            if (!Current(state) || !show) return ExpandCollapseState.LeafNode;
            var expanded = expander.IsExpanded;
            return !Current(state) ? ExpandCollapseState.LeafNode :
                expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        }
    }
    public void Expand() => SetExpanded(true);
    public void Collapse() => SetExpanded(false);
    private void SetExpanded(bool expanded)
    {
        EnsureUsable();
        unchecked { ++_operationRevision; }
        if (!TryCapture(out var state)) return;
        if (state.Value is not ExpanderCellValue expander)
            throw new InvalidOperationException("This cell has no expandable children.");
        var show = expander.ShowExpander;
        if (!Current(state)) return;
        EnsureEnabled(state);
        if (!show) throw new InvalidOperationException("This cell has no expandable children.");
        expander.IsExpanded = expanded;
    }
    private void EnsureUsable()
    {
        if (!Realized) throw new InvalidOperationException("The cell is not currently realized.");
        if (!owner.IsEnabled || owner.Presenter?.Owner?.IsEnabled == false) throw new ElementNotEnabledException();
    }
    private void EnsureEnabled(in Observation state)
    {
        if (!owner.IsEnabled || !state.Target.IsEnabled || !state.Grid.IsEnabled) throw new ElementNotEnabledException();
    }

    // Stack-local identities only, not retained data. Capture the actual inner
    // editor because expander content may change without replacing its outer.
    private readonly record struct Observation(TreeDataGrid Grid, TreeDataGridPresentation Presentation,
        TreeDataGridRowsPresenter Presenter, int PresenterRevision, int Realization, int Row, int ColumnIndex,
        object? RowModel, CellColumn? Column, CellValue Value, TreeDataGridCell Target, int TargetRealization,
        CellValue? TargetValue, int OperationRevision, int NotificationRevision);

    private bool TryCapture(out Observation state)
    {
        state = default;
        if (!Realized || owner.Presenter is not { } presenter || presenter.Owner is not { } grid ||
            grid.Presentation is not { } presentation || owner.Value is not { } value) return false;
        var revision = presenter.Revision;
        var realization = owner.RealizationVersion;
        var row = owner.RowIndex;
        var column = owner.ColumnIndex;
        var model = owner.RowModel;
        var definition = owner.Column;
        var operation = _operationRevision;
        var notification = _notificationRevision;
        var target = owner.GetEditingTarget();
        state = new(grid, presentation, presenter, revision, realization, row, column, model, definition,
            value, target, target.RealizationVersion, target.Value, operation, notification);
        return Current(state);
    }
    private bool Current(in Observation state) =>
        state.OperationRevision == _operationRevision && state.NotificationRevision == _notificationRevision &&
        owner.RealizationVersion == state.Realization && owner.RowIndex == state.Row && owner.ColumnIndex == state.ColumnIndex &&
        ReferenceEquals(owner.RowModel, state.RowModel) && ReferenceEquals(owner.Column, state.Column) &&
        ReferenceEquals(owner.Value, state.Value) && ReferenceEquals(owner.Presenter, state.Presenter) &&
        state.Presenter.Revision == state.PresenterRevision && ReferenceEquals(state.Presenter.Owner, state.Grid) &&
        ReferenceEquals(state.Grid.Presentation, state.Presentation) &&
        state.Target.RealizationVersion == state.TargetRealization && ReferenceEquals(state.Target.Value, state.TargetValue) && Realized;

    // Existing peer notification path: no additional model subscriptions. Newer
    // nested notifications own their snapshot and suppress older continuations.
    internal void NotifyValueChanged()
    {
        unchecked { ++_notificationRevision; }
        if (!TryCapture(out var state)) return;
        var value = Value;
        if (!Current(state)) return;
        var readOnly = IsReadOnly;
        if (!Current(state)) return;
        var toggle = ToggleState;
        if (!Current(state)) return;
        var expanded = ExpandCollapseState;
        if (!Current(state)) return;
        var oldValue = _lastValue;
        var oldReadOnly = _lastReadOnly;
        var oldToggle = _lastToggle;
        var oldExpanded = _lastExpanded;
        _lastValue = value;
        _lastReadOnly = readOnly;
        _lastToggle = toggle;
        _lastExpanded = expanded;
        if (oldValue != value) RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldValue, value);
        if (!Current(state)) return;
        if (oldReadOnly != readOnly) RaisePropertyChangedEvent(ValuePatternIdentifiers.IsReadOnlyProperty, oldReadOnly, readOnly);
        if (!Current(state)) return;
        if (oldToggle != toggle) RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, oldToggle, toggle);
        if (!Current(state)) return;
        if (oldExpanded != expanded) RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty, oldExpanded, expanded);
    }
}
