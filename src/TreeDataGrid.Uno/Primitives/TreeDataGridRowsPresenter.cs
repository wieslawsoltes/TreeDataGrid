using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Controls.Presentation;
using Windows.Foundation;

namespace Uno.Controls.Primitives;

/// <summary>Viewport-only realization with parented cell-control recycling.</summary>
public partial class TreeDataGridRowsPresenter : Panel
{
    private readonly Dictionary<(int Row, int Column), TreeDataGridCell> _realized = new();
    private readonly Dictionary<CellColumn, Stack<TreeDataGridCell>> _pool = new();
    private TreeDataGridPresentation? _presentation;
    private ColumnGeometry? _geometry;
    private double _horizontalOffset;
    private double _verticalOffset;
    private double _viewportWidth;
    private double _viewportHeight;
    private int _pooled;
    internal TreeDataGrid? Owner { get; set; }
    public IReadOnlyCollection<TreeDataGridCell> RealizedCells => _realized.Values;
    public double RowHeight { get; set; } = 28;
    internal void RefreshSelection()
    {
        foreach (var cell in _realized.Values) UpdateSelection(cell);
    }
    private void UpdateSelection(TreeDataGridCell cell)
    {
        cell.IsSelected = _presentation?.Selection.IsSelected(cell.RowIndex, cell.ColumnIndex) == true;
        cell.IsCurrent = Owner?.IsCurrentCell(cell.RowIndex, cell.ColumnIndex) == true;
    }

    internal void SetPresentation(TreeDataGridPresentation? presentation, ColumnGeometry geometry)
    {
        if (ReferenceEquals(_presentation, presentation))
        {
            _geometry = geometry;
            SynchronizeColumns();
            InvalidateMeasure();
            return;
        }
        Reset();
        _presentation = presentation;
        _geometry = geometry;
        InvalidateMeasure();
    }
    private void SynchronizeColumns()
    {
        if (_presentation is null) return;
        var indexes = new Dictionary<CellColumn, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < _presentation.Columns.Count; ++i) indexes[_presentation.Columns[i]] = i;
        var retained = new List<TreeDataGridCell>();
        foreach (var pair in _realized.ToArray())
        {
            if (indexes.ContainsKey(pair.Value.Column!)) retained.Add(pair.Value);
            else Recycle(pair.Key);
        }
        _realized.Clear();
        foreach (var cell in retained)
        {
            var columnIndex = indexes[cell.Column!];
            cell.UpdateIndexes(cell.RowIndex, columnIndex);
            _realized.Add((cell.RowIndex, columnIndex), cell);
            UpdateSelection(cell);
        }
        foreach (var column in _pool.Keys.ToArray())
        {
            if (indexes.ContainsKey(column)) continue;
            foreach (var cell in _pool[column]) { Children.Remove(cell); --_pooled; }
            _pool.Remove(column);
        }
    }
    internal void UpdateViewport(double horizontal, double vertical, double width, double height)
    {
        _horizontalOffset = horizontal;
        _verticalOffset = vertical;
        _viewportWidth = width;
        _viewportHeight = height;
        InvalidateMeasure();
    }
    internal void RowsChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_presentation is null) return;
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex >= 0)
        {
            ShiftRows(e.NewStartingIndex, e.NewItems!.Count);
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldStartingIndex >= 0)
        {
            var end = e.OldStartingIndex + e.OldItems!.Count;
            foreach (var key in _realized.Keys.ToArray())
                if (key.Row >= e.OldStartingIndex && key.Row < end) Recycle(key);
            ShiftRows(end, -e.OldItems.Count);
        }
        else
        {
            foreach (var key in _realized.Keys.ToArray())
            {
                if (key.Row >= _presentation.Rows.Count) Recycle(key);
                else if (e.Action != NotifyCollectionChangedAction.Replace || e.NewStartingIndex < 0 ||
                    (key.Row >= e.NewStartingIndex && key.Row < e.NewStartingIndex + e.NewItems!.Count))
                    Rebind(key);
            }
        }
        RefreshSelection();
        InvalidateMeasure();
    }
    private void ShiftRows(int from, int delta)
    {
        var shifted = _realized.Where(x => x.Key.Row >= from).ToArray();
        foreach (var pair in shifted) _realized.Remove(pair.Key);
        foreach (var pair in shifted)
        {
            var key = (Row: pair.Key.Row + delta, pair.Key.Column);
            pair.Value.UpdateIndexes(key.Row, key.Column);
            _realized.Add(key, pair.Value);
        }
    }

    private void Rebind((int Row, int Column) key)
    {
        var control = _realized[key];
        var column = control.Column!;
        var previous = control.Value!;
        CellValue? next = null;
        var success = false;
        try
        {
            control.BeginRebind();
            control.Unrealize();
            _presentation!.RecycleCell(column, previous);
            previous = null!;
            next = _presentation.RealizeCell(key.Column, key.Row);
            Owner!.CellTemplates.TryGetValue(column.Model.PresentationKey ?? "", out var template);
            Owner.CellEditingTemplates.TryGetValue(column.Model.PresentationKey ?? "", out var editingTemplate);
            control.Realize(column, next, _presentation.Rows[key.Row], key.Column, key.Row, template, editingTemplate);
            UpdateSelection(control);
            success = true;
        }
        finally
        {
            if (!success)
            {
                control.Unrealize();
                previous?.Dispose();
                next?.Dispose();
                _realized.Remove(key);
                PoolControl(column, control);
            }
            control.EndRebind(success);
        }
    }
    internal void Reset()
    {
        foreach (var key in _realized.Keys.ToArray()) Recycle(key);
        _pool.Clear();
        _pooled = 0;
        Children.Clear();
        _presentation = null;
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        if (_presentation is null || _geometry is null || Owner is null) return default;
        var rows = _presentation.Rows;
        var (firstColumn, endColumn) = _geometry.VisibleRange(_horizontalOffset, _viewportWidth);
        var firstRow = Math.Clamp((int)(_verticalOffset / RowHeight) - 1, 0, rows.Count);
        var endRow = Math.Clamp((int)Math.Ceiling((_verticalOffset + _viewportHeight) / RowHeight) + 1, 0, rows.Count);
        foreach (var key in _realized.Keys.ToArray())
            if (key.Row < firstRow || key.Row >= endRow || key.Column < firstColumn || key.Column >= endColumn)
                Recycle(key);
        for (var row = firstRow; row < endRow; ++row)
        {
            for (var column = firstColumn; column < endColumn; ++column)
            {
                if (!_realized.TryGetValue((row, column), out var control))
                {
                    var view = _presentation.Columns[column];
                    if (_pool.TryGetValue(view, out var pool) && pool.TryPop(out control)) --_pooled;
                    else
                    {
                        control = Owner.CellFactory(view);
                        Children.Add(control);
                    }
                    var success = false;
                    CellValue? value = null;
                    try
                    {
                        value = _presentation.RealizeCell(column, row);
                        Owner.CellTemplates.TryGetValue(view.Model.PresentationKey ?? "", out var template);
                        Owner.CellEditingTemplates.TryGetValue(view.Model.PresentationKey ?? "", out var editingTemplate);
                        control.Realize(view, value, rows[row], column, row, template, editingTemplate);
                        UpdateSelection(control);
                        _realized.Add((row, column), control);
                        success = true;
                    }
                    finally
                    {
                        if (!success)
                        {
                            control.Unrealize();
                            value?.Dispose();
                            PoolControl(view, control);
                        }
                    }
                }
                control.Measure(new(_geometry.Width(column), RowHeight));
            }
        }
        return new(_geometry.TotalWidth, rows.Count * RowHeight);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (key, control) in _realized)
            control.Arrange(new(_geometry!.Start(key.Column), key.Row * RowHeight, _geometry.Width(key.Column), RowHeight));
        return finalSize;
    }
    private void Recycle((int Row, int Column) key)
    {
        var cell = _realized[key];
        var value = cell.Value;
        var column = cell.Column!;
        cell.Unrealize();
        if (value is not null) _presentation!.RecycleCell(column, value);
        _realized.Remove(key);
        PoolControl(column, cell);
    }
    private void PoolControl(CellColumn column, TreeDataGridCell cell)
    {
        if (_pooled < 256)
        {
            if (!_pool.TryGetValue(column, out var pool)) _pool[column] = pool = new();
            pool.Push(cell);
            ++_pooled;
        }
        else Children.Remove(cell);
    }
}
