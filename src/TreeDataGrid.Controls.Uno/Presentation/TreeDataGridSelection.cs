using System;
using System.Collections.Generic;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using Microsoft.UI.Xaml.Input;

namespace Uno.Controls.Presentation;

/// <summary>Maps visible UI indexes to the shared Core selection. It owns no selected items.</summary>
public abstract class TreeDataGridSelection : Uno.Controls.Selection.ITreeDataGridSelectionInteraction
{
    public virtual void OnPreviewKeyDown(TreeDataGrid sender, KeyRoutedEventArgs e) { }
    public virtual void OnKeyDown(TreeDataGrid sender, KeyRoutedEventArgs e) => sender.ProcessSelectionKeyDown(e);
    public virtual void OnKeyUp(TreeDataGrid sender, KeyRoutedEventArgs e) { }
    public virtual void OnTextInput(TreeDataGrid sender, CharacterReceivedRoutedEventArgs e) => sender.ProcessSelectionTextInput(e);
    public virtual void OnPointerPressed(TreeDataGrid sender, PointerRoutedEventArgs e) => sender.ProcessSelectionPointerPressed(e);
    public virtual void OnPointerMoved(TreeDataGrid sender, PointerRoutedEventArgs e) { }
    public virtual void OnPointerReleased(TreeDataGrid sender, PointerRoutedEventArgs e) => sender.ProcessSelectionPointerReleased(e);
    event EventHandler? Uno.Controls.Selection.ITreeDataGridSelectionInteraction.SelectionChanged
    {
        add => Changed += value;
        remove => Changed -= value;
    }
    public virtual bool IsCellSelected(int columnIndex, int rowIndex) => IsCellSelection && IsSelected(rowIndex, columnIndex);
    public virtual bool IsRowSelected(IRow rowModel) => rowModel is IModelIndexableRow indexed &&
        Model is ITreeDataGridRowSelectionModel rows && rows.IsSelected(indexed.ModelIndexPath);
    public virtual bool IsRowSelected(int rowIndex) => !IsCellSelection && IsSelected(rowIndex, 0);
    public abstract ITreeDataGridSelection? Model { get; }
    public abstract bool IsCellSelection { get; }
    public abstract bool IsSelected(int row, int column);
    public abstract bool Select(int row, int column, bool extend = false, bool toggle = false, bool preserve = false);
    public abstract (int Row, int Column) GetAnchor(bool range);
    public abstract void SelectAll();
    public abstract void Clear();
    public abstract void Configure(TreeDataGridSelectionMode mode);
    public abstract event EventHandler? Changed;
    /// <summary>Selection deltas, separate from visual/anchor invalidation.</summary>
    public abstract event EventHandler<TreeDataGridSelectionChangedEventArgs>? SelectionChanged;
}

internal sealed class TreeDataGridSelection<TModel>(ITreeDataGridSource<TModel> source,
    IReadOnlyList<CellColumn> columns) : TreeDataGridSelection where TModel : class
{
    private ITreeDataGridSelection? _selection;
    private readonly Dictionary<IColumn, int> _sourceIndexes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IColumn, int> _visibleIndexes = new(ReferenceEqualityComparer.Instance);
    private bool _active;
    private CellIndex[] _selectedCells = Array.Empty<CellIndex>();
    private EventHandler<TreeDataGridSelectionChangedEventArgs>? _selectionChanged;
    public override ITreeDataGridSelection? Model => source.Selection;
    public override bool IsCellSelection => Model is ITreeDataGridCellSelectionModel<TModel>;
    public override bool IsRowSelected(int rowIndex) => (uint)rowIndex < (uint)source.Rows.Count &&
        Model is ITreeDataGridRowSelectionModel rows && rows.IsSelected(source.Rows.RowIndexToModelIndex(rowIndex));
    public override event EventHandler? Changed;
    public override event EventHandler<TreeDataGridSelectionChangedEventArgs>? SelectionChanged
    {
        add
        {
            if (_selectionChanged is null && value is not null) CaptureSelectedCells();
            _selectionChanged += value;
        }
        remove
        {
            _selectionChanged -= value;
            if (_selectionChanged is null) _selectedCells = Array.Empty<CellIndex>();
        }
    }

    internal void Resume()
    {
        _active = true;
        Refresh();
    }
    internal void Suspend()
    {
        _active = false;
        Detach();
    }
    internal void Refresh()
    {
        if (!_active) return;
        if (!ReferenceEquals(_selection, source.Selection))
        {
            Detach();
            _selection = source.Selection;
            if (_selection is ITreeDataGridRowSelectionModel rows)
            {
                rows.StateChanged += OnChanged;
                rows.SelectionChanged += OnRowSelectionChanged;
            }
            if (_selection is ITreeDataGridCellSelectionModel<TModel> cells)
            {
                if (_selectionChanged is not null) CaptureSelectedCells();
                cells.SelectionChanged += OnCellSelectionChanged;
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    internal void ColumnsChanged(IReadOnlyList<CellColumn>? next = null)
    {
        var visible = next ?? columns;
        _sourceIndexes.Clear();
        _visibleIndexes.Clear();
        for (var i = 0; i < source.Columns.Count; ++i) _sourceIndexes[source.Columns[i]] = i;
        for (var i = 0; i < visible.Count; ++i) _visibleIndexes[visible[i].Model] = i;
    }
    private void Detach()
    {
        if (_selection is ITreeDataGridRowSelectionModel rows)
        {
            rows.StateChanged -= OnChanged;
            rows.SelectionChanged -= OnRowSelectionChanged;
        }
        if (_selection is ITreeDataGridCellSelectionModel<TModel> cells) cells.SelectionChanged -= OnCellSelectionChanged;
        _selection = null;
        _selectedCells = Array.Empty<CellIndex>();
    }
    private bool IsCurrentSelection(object? sender) =>
        _active && ReferenceEquals(sender, _selection) && ReferenceEquals(_selection, source.Selection);
    private void OnChanged(object? sender, EventArgs e)
    {
        if (IsCurrentSelection(sender)) Changed?.Invoke(this, EventArgs.Empty);
    }
    private void CaptureSelectedCells() => _selectedCells = _active && _selection is ITreeDataGridCellSelectionModel<TModel> cells
        ? cells.SelectedIndexes.ToArray() : Array.Empty<CellIndex>();
    private void OnRowSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs e)
    {
        if (!IsCurrentSelection(sender)) return;
        // Core's item views can resolve lazily. Capture while the original
        // model/index mapping is valid, before application callbacks mutate it.
        _selectionChanged?.Invoke(this, new(
            e.DeselectedIndexes.ToArray(), e.SelectedIndexes.ToArray(),
            e.DeselectedItems.ToArray(), e.SelectedItems.ToArray()));
    }
    private void OnCellSelectionChanged(object? sender, TreeDataGridCellSelectionChangedEventArgs<TModel> e)
    {
        if (!IsCurrentSelection(sender)) return;
        try
        {
            if (_selectionChanged is not { } handler) return;
            var selected = ((ITreeDataGridCellSelectionModel<TModel>)sender!).SelectedIndexes.ToArray();
            var previous = _selectedCells;
            // Commit the baseline first: nested selection changes must diff
            // against this state, not the state preceding the outer event.
            _selectedCells = selected;
            var args = new TreeDataGridSelectionChangedEventArgs(
                deselectedCellIndexes: previous.Except(selected).ToArray(),
                selectedCellIndexes: selected.Except(previous).ToArray());
            handler(this, args);
        }
        finally
        {
            if (IsCurrentSelection(sender)) Changed?.Invoke(this, EventArgs.Empty);
        }
    }
    private bool TryIndex(int row, int column, out CellIndex index)
    {
        if ((uint)row < (uint)source.Rows.Count && (uint)column < (uint)columns.Count &&
            _sourceIndexes.TryGetValue(columns[column].Model, out var sourceColumn))
        {
            index = new(sourceColumn, source.Rows.RowIndexToModelIndex(row));
            return true;
        }
        index = default;
        return false;
    }
    public override bool IsSelected(int row, int column) => TryIndex(row, column, out var index) && Model switch
    {
        ITreeDataGridRowSelectionModel rows => rows.IsSelected(index.RowIndex),
        ITreeDataGridCellSelectionModel<TModel> cells => cells.IsSelected(index),
        _ => false,
    };
    public override bool Select(int row, int column, bool extend = false, bool toggle = false, bool preserve = false)
    {
        if (!_active || !TryIndex(row, column, out var index)) return false;
        if (preserve && IsSelected(row, column)) return true;
        if (Model is ITreeDataGridCellSelectionModel<TModel> cells)
        {
            var anchor = cells.AnchorIndex;
            var anchorRow = source.Rows.ModelIndexToRowIndex(anchor.RowIndex);
            if (extend && !cells.SingleSelect && anchor.ColumnIndex >= 0 && anchorRow >= 0)
                cells.SetSelectedRange(anchor, Inclusive(index.ColumnIndex - anchor.ColumnIndex), Inclusive(row - anchorRow));
            else cells.SelectedIndex = index;
            return true;
        }
        if (Model is not ITreeDataGridRowSelectionModel rows) return false;
        if (extend && !rows.SingleSelect)
        {
            var anchor = rows.RangeAnchorIndex;
            var anchorRow = source.Rows.ModelIndexToRowIndex(anchor);
            if (anchorRow < 0) { rows.SelectedIndex = index.RowIndex; return true; }
            rows.BeginBatchUpdate();
            try
            {
                if (!toggle) rows.Clear();
                for (var i = Math.Min(anchorRow, row); i <= Math.Max(anchorRow, row); ++i)
                    rows.Select(source.Rows.RowIndexToModelIndex(i));
                rows.AnchorIndex = index.RowIndex;
                rows.RangeAnchorIndex = anchor;
            }
            finally { rows.EndBatchUpdate(); }
        }
        else if (toggle && !rows.SingleSelect)
        {
            if (rows.IsSelected(index.RowIndex)) rows.Deselect(index.RowIndex);
            else rows.Select(index.RowIndex);
            rows.AnchorIndex = rows.RangeAnchorIndex = index.RowIndex;
        }
        else rows.SelectedIndex = index.RowIndex;
        return true;
    }
    public override (int Row, int Column) GetAnchor(bool range)
    {
        if (Model is ITreeDataGridRowSelectionModel rows)
            return (source.Rows.ModelIndexToRowIndex(rows.AnchorIndex), 0);
        if (Model is ITreeDataGridCellSelectionModel<TModel> cells)
        {
            var anchor = range ? cells.RangeAnchorIndex : cells.SelectedIndex;
            var column = (uint)anchor.ColumnIndex < (uint)source.Columns.Count &&
                _visibleIndexes.TryGetValue(source.Columns[anchor.ColumnIndex], out var visible) ? visible : -1;
            return (source.Rows.ModelIndexToRowIndex(anchor.RowIndex), column);
        }
        return (-1, -1);
    }
    public override void SelectAll()
    {
        if (!_active || source.Rows.Count == 0 || columns.Count == 0) return;
        if (Model is ITreeDataGridCellSelectionModel<TModel> cells)
            cells.SetSelectedRange(new(0, source.Rows.RowIndexToModelIndex(0)), source.Columns.Count, source.Rows.Count);
        else if (Model is ITreeDataGridRowSelectionModel rows)
        {
            if (rows.SingleSelect) { rows.SelectedIndex = source.Rows.RowIndexToModelIndex(0); return; }
            rows.BeginBatchUpdate();
            try
            {
                rows.Clear();
                for (var i = 0; i < source.Rows.Count; ++i) rows.Select(source.Rows.RowIndexToModelIndex(i));
            }
            finally { rows.EndBatchUpdate(); }
        }
    }
    public override void Clear()
    {
        if (Model is ITreeDataGridRowSelectionModel rows) rows.Clear();
        if (Model is ITreeDataGridCellSelectionModel<TModel> cells) cells.Clear();
    }
    public override void Configure(TreeDataGridSelectionMode mode)
    {
        if (mode == TreeDataGridSelectionMode.Source) return;
        if (mode == TreeDataGridSelectionMode.None) { source.Selection = null; return; }
        if ((mode & ~(TreeDataGridSelectionMode.Row | TreeDataGridSelectionMode.Cell | TreeDataGridSelectionMode.Multiple)) != 0)
            throw new ArgumentOutOfRangeException(nameof(mode));
        var singleSelect = (mode & TreeDataGridSelectionMode.Multiple) == 0;
        if ((mode & TreeDataGridSelectionMode.Cell) != 0)
        {
            if (source.Selection is not ITreeDataGridCellSelectionModel<TModel>) source.Selection = new TreeDataGridCellSelectionModel<TModel>(source);
            ((ITreeDataGridCellSelectionModel<TModel>)source.Selection!).SingleSelect = singleSelect;
        }
        else
        {
            if (source.Selection is not ITreeDataGridRowSelectionModel) source.Selection = new TreeDataGridRowSelectionModel<TModel>(source);
            ((ITreeDataGridRowSelectionModel)source.Selection!).SingleSelect = singleSelect;
        }
    }
    private static int Inclusive(int delta) => delta >= 0 ? delta + 1 : delta - 1;
}
