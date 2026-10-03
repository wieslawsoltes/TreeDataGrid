using System;
using System.Collections.Generic;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

public sealed partial class TreeDataGridPresentation<TModel> where TModel : class
{
    private int _cellOperationVersion;
    private EmptyStackPool<CellValue> _emptyCellStacks;

    public override CellValue RealizeCell(int columnIndex, int rowIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_active) throw new InvalidOperationException("The presentation is suspended.");
        var version = _cellOperationVersion;
        var column = _visible[columnIndex];
        var row = Rows[rowIndex];
        EnsureCellOperation(version, columnIndex, column);
        while (_pool.TryGetValue(column, out var values) && values.TryPop(out var value))
        {
            --_pooled;
            if (values.Count == 0)
            {
                // Empty dictionaries used to reserve one of the 32 column-pool
                // slots forever, starving later columns after horizontal travel.
                // Reuse their empty storage, not their obsolete column ownership.
                _pool.Remove(column);
                _emptyCellStacks.ReturnEmpty(values);
            }
            try
            {
                var reused = column.TryReuseCell(value, row);
                EnsureCellOperation(version, columnIndex, column);
                if (reused)
                {
                    column.ConfigureCell(value);
                    EnsureCellOperation(version, columnIndex, column);
                    return value;
                }
            }
            catch (Exception error)
            {
                DisposeFailedCell(value, error);
                throw;
            }
            value.Dispose();
            EnsureCellOperation(version, columnIndex, column);
            // Core's flat row wrapper is mutable. A rejected reuse or disposal
            // callback can query another row without changing the collection.
            // Reacquire the requested Core row instead of retaining that wrapper.
            row = Rows[rowIndex];
            EnsureCellOperation(version, columnIndex, column);
        }

        var created = column.CreateCell(row) ?? throw new InvalidOperationException("The column factory returned no cell.");
        try
        {
            EnsureCellOperation(version, columnIndex, column);
            column.ConfigureCell(created);
            EnsureCellOperation(version, columnIndex, column);
            return created;
        }
        catch (Exception error)
        {
            DisposeFailedCell(created, error);
            throw;
        }
    }

    internal override bool TryReuseCell(int columnIndex, int rowIndex, CellValue value)
    {
        var version = _cellOperationVersion;
        if (_disposed || !_active || (uint)columnIndex >= (uint)_visible.Count || (uint)rowIndex >= (uint)Rows.Count) return false;
        var column = _visible[columnIndex];
        var row = Rows[rowIndex];
        if (!IsCellOperationCurrent(version, columnIndex, column) || !column.TryReuseCell(value, row) ||
            !IsCellOperationCurrent(version, columnIndex, column)) return false;
        column.ConfigureCell(value);
        // This path borrows a caller-owned cell; false leaves its cleanup with
        // that caller, unlike RealizeCell's exclusively owned popped/new value.
        return IsCellOperationCurrent(version, columnIndex, column);
    }

    public override void RecycleCell(CellColumn column, CellValue cell)
    {
        var version = _cellOperationVersion;
        try
        {
            if (_active && !_disposed && _pooled < PoolCapacity && _visible.Contains(column) &&
                (_pool.ContainsKey(column) || _pool.Count < ColumnPoolCapacity) && cell.TrySuspend() &&
                version == _cellOperationVersion && _active && !_disposed && _visible.Contains(column) &&
                _pooled < PoolCapacity && (_pool.ContainsKey(column) || _pool.Count < ColumnPoolCapacity))
            {
                if (!_pool.TryGetValue(column, out var values)) _pool[column] = values = _emptyCellStacks.Rent();
                values.Push(cell);
                ++_pooled;
                return;
            }
        }
        catch (Exception error)
        {
            DisposeFailedCell(cell, error);
            throw;
        }
        cell.Dispose();
    }

    private bool IsCellOperationCurrent(int version, int columnIndex, CellColumn column) =>
        version == _cellOperationVersion && _active && !_disposed &&
        (uint)columnIndex < (uint)_visible.Count && ReferenceEquals(_visible[columnIndex], column);

    private void EnsureCellOperation(int version, int columnIndex, CellColumn column)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsCellOperationCurrent(version, columnIndex, column))
            throw new InvalidOperationException("The presentation changed while realizing a cell.");
    }

    private static void DisposeFailedCell(CellValue cell, Exception error)
    {
        try { cell.Dispose(); }
        catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
    }
}
