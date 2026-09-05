using System;
using System.Collections.Generic;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;

namespace Uno.Controls.Presentation;

public enum TreeDataGridSelectionMode { Source, None, SingleRow, MultipleRows, SingleCell, MultipleCells }

/// <summary>Maps visible UI indexes to the shared Core selection. It owns no selected items.</summary>
public abstract class TreeDataGridSelection
{
    public abstract ITreeDataGridSelection? Model { get; }
    public abstract bool IsCellSelection { get; }
    public abstract bool IsSelected(int row, int column);
    public abstract bool Select(int row, int column, bool extend = false, bool toggle = false, bool preserve = false);
    public abstract (int Row, int Column) GetAnchor(bool range);
    public abstract void SelectAll();
    public abstract void Clear();
    public abstract void Configure(TreeDataGridSelectionMode mode);
    public abstract event EventHandler? Changed;
}

internal sealed class TreeDataGridSelection<TModel>(ITreeDataGridSource<TModel> source,
    IReadOnlyList<CellColumn> columns) : TreeDataGridSelection where TModel : class
{
    private ITreeDataGridSelection? _selection;
    private readonly Dictionary<IColumn, int> _sourceIndexes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IColumn, int> _visibleIndexes = new(ReferenceEqualityComparer.Instance);
    private bool _active;
    public override ITreeDataGridSelection? Model => source.Selection;
    public override bool IsCellSelection => Model is ITreeDataGridCellSelectionModel<TModel>;
    public override event EventHandler? Changed;

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
            if (_selection is ITreeDataGridRowSelectionModel rows) rows.StateChanged += OnChanged;
            if (_selection is ITreeDataGridCellSelectionModel<TModel> cells) cells.SelectionChanged += OnChanged;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    internal void ColumnsChanged()
    {
        _sourceIndexes.Clear();
        _visibleIndexes.Clear();
        for (var i = 0; i < source.Columns.Count; ++i) _sourceIndexes[source.Columns[i]] = i;
        for (var i = 0; i < columns.Count; ++i) _visibleIndexes[columns[i].Model] = i;
    }
    private void Detach()
    {
        if (_selection is ITreeDataGridRowSelectionModel rows) rows.StateChanged -= OnChanged;
        if (_selection is ITreeDataGridCellSelectionModel<TModel> cells) cells.SelectionChanged -= OnChanged;
        _selection = null;
    }
    private void OnChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
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
        switch (mode)
        {
            case TreeDataGridSelectionMode.Source: return;
            case TreeDataGridSelectionMode.None: source.Selection = null; break;
            case TreeDataGridSelectionMode.SingleRow:
            case TreeDataGridSelectionMode.MultipleRows:
                if (source.Selection is not ITreeDataGridRowSelectionModel) source.Selection = new TreeDataGridRowSelectionModel<TModel>(source);
                ((ITreeDataGridRowSelectionModel)source.Selection!).SingleSelect = mode == TreeDataGridSelectionMode.SingleRow;
                break;
            case TreeDataGridSelectionMode.SingleCell:
            case TreeDataGridSelectionMode.MultipleCells:
                if (source.Selection is not ITreeDataGridCellSelectionModel<TModel>) source.Selection = new TreeDataGridCellSelectionModel<TModel>(source);
                ((ITreeDataGridCellSelectionModel<TModel>)source.Selection!).SingleSelect = mode == TreeDataGridSelectionMode.SingleCell;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }
    private static int Inclusive(int delta) => delta >= 0 ? delta + 1 : delta - 1;
}
