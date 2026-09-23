using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Uno.Controls.Automation.Peers;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using Uno.Controls.Presentation;
using Uno.Controls.Selection;
using IColumns = Uno.Controls.Models.TreeDataGrid.IColumns;
using ITreeDataGridRows = Uno.Controls.Models.TreeDataGrid.ITreeDataGridRows;

namespace Uno.Controls.Primitives;

public enum TreeDataGridRowUnrealizeReason { Recycle, ItemRemoved }

/// <summary>A reusable row container whose cells retain their native parent.</summary>
[TemplatePart(Name = "PART_CellsPresenter", Type = typeof(TreeDataGridCellsPresenter))]
public class TreeDataGridRow : Control
{
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(IColumns), typeof(TreeDataGridRow), new PropertyMetadata(null));
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(ITreeDataGridRows), typeof(TreeDataGridRow), new PropertyMetadata(null));
    public static readonly DependencyProperty ElementFactoryProperty = DependencyProperty.Register(
        nameof(ElementFactory), typeof(TreeDataGridElementFactory), typeof(TreeDataGridRow), new PropertyMetadata(null, OnElementFactoryChanged));
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(TreeDataGridRow), new PropertyMetadata(false, OnSelectionChanged));
    internal TreeDataGridRowsPresenter? Presenter { get; private set; }
    internal TreeDataGridPresentation? Presentation { get; private set; }
    internal int RealizationVersion { get; private set; }
    internal int RecycledRowIndex { get; set; } = -1;
    internal bool IsResettingCells { get; set; }
    // Set only by the owning presenter during synchronous viewport layout.
    // Public/standalone unrealization and collection removal still hide now.
    internal bool IsRecyclingVisibilityDeferred { get; set; }
    internal ITreeDataGridSelectionInteraction? StandaloneSelection { get; private set; }
    private bool _realizingStandalone;
    private bool _unrealizing;
    public TreeDataGridRow() => DefaultStyleKey = typeof(TreeDataGridRow);
    private TreeDataGridRowAutomationPeer? _automationPeer;
    protected override AutomationPeer OnCreateAutomationPeer() => _automationPeer = new TreeDataGridRowAutomationPeer(this);
    internal void NotifyAutomationStateChanged() => _automationPeer?.NotifyStateChanged();
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); private set => SetValue(IsSelectedProperty, BooleanBoxes.Box(value)); }
    public object? Model => DataContext;
    public ITreeDataGridRows? Rows { get => (ITreeDataGridRows?)GetValue(RowsProperty); private set => SetValue(RowsProperty, value); }
    public IColumns? Columns { get => (IColumns?)GetValue(ColumnsProperty); private set => SetValue(ColumnsProperty, value); }
    public TreeDataGridElementFactory? ElementFactory
    {
        get => (TreeDataGridElementFactory?)GetValue(ElementFactoryProperty);
        set => SetValue(ElementFactoryProperty, value);
    }
    public TreeDataGridCellsPresenter? CellsPresenter { get; private set; }
    public int RowIndex { get; private set; } = -1;
    public TreeDataGridCell? TryGetCell(int columnIndex) => CellsPresenter?.TryGetElement(columnIndex);

    public void Realize(TreeDataGridElementFactory? elementFactory, ITreeDataGridSelectionInteraction? selection,
        IColumns? columns, ITreeDataGridRows? rows, int rowIndex)
    {
        // A previously grid-owned container must first be released by that grid.
        if (Presenter is not null) throw new InvalidOperationException("Row is still owned by a grid presenter.");
        RealizeCore(null, null, elementFactory, selection, columns, rows, rowIndex);
    }

    internal void Realize(TreeDataGridRowsPresenter presenter, TreeDataGridElementFactory? elementFactory,
        ITreeDataGridSelectionInteraction? selection, IColumns? columns, ITreeDataGridRows? rows, int rowIndex) =>
        RealizeCore(presenter, null, elementFactory, selection, columns, rows, rowIndex);

    internal void Realize(TreeDataGridRowsPresenter presenter, TreeDataGridPresentation presentation, int rowIndex) =>
        RealizeCore(presenter, presentation, presenter.ElementFactory ?? presenter.Owner?.ElementFactory,
            presentation.SelectionInteraction, presentation.Columns, presentation.Rows, rowIndex);

    private void RealizeCore(TreeDataGridRowsPresenter? presenter, TreeDataGridPresentation? presentation,
        TreeDataGridElementFactory? elementFactory, ITreeDataGridSelectionInteraction? selection,
        IColumns? columns, ITreeDataGridRows? rows, int rowIndex)
    {
        if (_unrealizing) throw new InvalidOperationException("Row unrealization is in progress.");
        if (RowIndex >= 0) throw new InvalidOperationException("Row is already realized.");
        if (_realizingStandalone) throw new InvalidOperationException("Row realization is already in progress.");
        if (rowIndex < 0 || (rows is not null && rowIndex >= rows.Count)) throw new ArgumentOutOfRangeException(nameof(rowIndex));
        IsRecyclingVisibilityDeferred = false;
        var realization = ++RealizationVersion;
        var revision = presenter?.Revision;
        Presenter = presenter;
        Presentation = presentation;
        _realizingStandalone = true;
        RowIndex = rowIndex;
        try
        {
            // Flat Core sources reuse their IRow wrapper. Capture the model
            // before setting properties or invoking application callbacks.
            var model = rows?[rowIndex].Model;
            if (!Current()) return;
            ElementFactory = elementFactory;
            if (!Current()) return;
            Columns = columns;
            if (!Current()) return;
            Rows = rows;
            if (!Current()) return;
            StandaloneSelection = selection;
            DataContext = model;
            if (!Current()) return;
            Visibility = Visibility.Visible;
            if (!Current()) return;
            CellsPresenter?.Attach(this);
            if (!Current()) return;
            UpdateSelection();
            if (!Current()) return;
            NotifyAutomationStateChanged();
            if (!Current()) return;
            OnRealized(rowIndex);
            if (Current()) presenter?.Owner?.RaiseRowPrepared(this, rowIndex);
        }
        catch (Exception error) when (presenter is null)
        {
            // A presenter owns failed native/hosted realization cleanup. The
            // directly hosted public method has no such lifetime owner.
            try { Unrealize(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
        finally { _realizingStandalone = false; }
        bool Current() => realization == RealizationVersion && revision == presenter?.Revision &&
            ReferenceEquals(Presenter, presenter) && ReferenceEquals(Presentation, presentation);
    }
    public void UpdateIndex(int rowIndex)
    {
        if (_unrealizing) throw new InvalidOperationException("Row unrealization is in progress.");
        if (RowIndex < 0) throw new InvalidOperationException("Row is not realized.");
        var realization = RealizationVersion;
        var rows = Rows;
        if (rowIndex < 0 || (rows is not null && rowIndex >= rows.Count))
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        // A custom Count accessor can retire or replace this realization.
        if (realization != RealizationVersion || !ReferenceEquals(Rows, rows)) return;
        var previous = RowIndex;
        RowIndex = rowIndex;
        CellsPresenter?.UpdateRowIndex(rowIndex);
        if (realization != RealizationVersion || RowIndex != rowIndex) return;
        OnRowIndexChanged(previous, rowIndex);
        if (realization == RealizationVersion && RowIndex == rowIndex) NotifyAutomationStateChanged();
    }
    internal void Unrealize(TreeDataGridRowUnrealizeReason reason)
    {
        // Preserve the old identity during clearing callbacks, but only the
        // outermost call owns teardown and its lifecycle notifications.
        if (_unrealizing || RowIndex < 0) return;
        _unrealizing = true;
        var rowIndex = RowIndex;
        ++RealizationVersion;
        List<Exception>? errors = null;
        try
        {
            try
            {
                Presenter?.Owner?.RaiseRowClearing(this, rowIndex);
                OnUnrealizing(rowIndex, reason);
            }
            catch (Exception error) { (errors ??= new()).Add(error); }
            try { CellsPresenter?.Unrealize(); }
            catch (Exception error) { (errors ??= new()).Add(error); }

            // Clear non-callback state first; keep RealizeCore guarded until
            // all DP/automation cleanup attempts have completed. A throwing
            // application callback must not skip the other retirement stages.
            RowIndex = -1;
            StandaloneSelection = null;
            try { DataContext = null; }
            catch (Exception error) { (errors ??= new()).Add(error); }
            try { IsSelected = false; }
            catch (Exception error) { (errors ??= new()).Add(error); }
            if (!IsRecyclingVisibilityDeferred)
            {
                try { Visibility = Visibility.Collapsed; }
                catch (Exception error) { (errors ??= new()).Add(error); }
            }
            try { NotifyAutomationStateChanged(); }
            catch (Exception error) { (errors ??= new()).Add(error); }
        }
        finally { _unrealizing = false; }
        // The normal recycling path allocates no error collection or closure.
        if (errors is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException("Row unrealization failed.", errors);
    }
    internal void Release()
    {
        IsRecyclingVisibilityDeferred = false;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? error = null;
        try { if (RowIndex < 0) Visibility = Visibility.Collapsed; }
        catch (Exception e) { error = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e); }
        try { CellsPresenter?.Reset(); }
        catch (Exception e) { error = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e); }
        Presenter = null;
        Presentation = null;
        StandaloneSelection = null;
        // Native property callbacks are application code. A failure clearing one
        // property must not leave the source or factory rooted by another.
        Clear(ColumnsProperty);
        Clear(RowsProperty);
        Clear(ElementFactoryProperty);
        error?.Throw();

        void Clear(DependencyProperty property)
        {
            try { SetValue(property, null); }
            catch (Exception e) { error ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e); }
        }
    }
    internal void UpdateSelection()
    {
        if (_unrealizing) return;
        var realization = RealizationVersion;
        var selection = Presentation is { } presentation ? presentation.SelectionInteraction : StandaloneSelection;
        var selected = RowIndex >= 0 && selection?.IsRowSelected(RowIndex) == true;
        if (realization != RealizationVersion) return;
        IsSelected = selected;
        if (realization != RealizationVersion) return;
        CellsPresenter?.UpdateSelection();
    }
    public void Unrealize() => Unrealize(TreeDataGridRowUnrealizeReason.Recycle);
    public void UnrealizeOnItemRemoved() => Unrealize(TreeDataGridRowUnrealizeReason.ItemRemoved);
    /// <summary>Called after this row and its primary cells presenter are realized.</summary>
    protected virtual void OnRealized(int rowIndex) => OnRealized();
    // Preserve the original Uno hook while providing Avalonia's indexed signature.
    protected virtual void OnRealized() { }
    protected virtual void OnRowIndexChanged(int oldIndex, int newIndex) { }
    /// <summary>Called while the old row index and model are still available.</summary>
    protected virtual void OnUnrealizing(int rowIndex, TreeDataGridRowUnrealizeReason reason) => OnUnrealizing(reason);
    protected virtual void OnUnrealizing(TreeDataGridRowUnrealizeReason reason) { }
    protected override void OnApplyTemplate()
    {
        CellsPresenter?.Reset();
        base.OnApplyTemplate();
        CellsPresenter = GetTemplateChild("PART_CellsPresenter") as TreeDataGridCellsPresenter;
        if (!_unrealizing) CellsPresenter?.Attach(this);
        UpdateState();
    }
    private static void OnSelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var row = (TreeDataGridRow)sender;
        row.UpdateState();
        row.NotifyAutomationStateChanged();
    }
    private static void OnElementFactoryChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var row = (TreeDataGridRow)sender;
        if (row.RowIndex >= 0 && !row._realizingStandalone && !row._unrealizing)
        {
            if (row.Presenter is { } presenter) presenter.ResetRowCells(row);
            else row.CellsPresenter?.Attach(row);
        }
    }
    private void UpdateState() => VisualStateManager.GoToState(this, IsSelected ? "Selected" : "Unselected", false);
}
