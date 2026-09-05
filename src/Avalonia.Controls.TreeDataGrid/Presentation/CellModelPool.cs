using System;
using System.Collections.Generic;
using Avalonia.Controls.Models.TreeDataGrid;
using Core = global::TreeDataGridCore;

namespace Avalonia.Controls.Presentation
{
    // One pool per presentation, keyed by column identity: bindings and options must never
    // cross columns or views. Both retained models and empty bucket metadata are bounded.
    internal sealed class CellModelPool<TModel> where TModel : class
    {
        internal const int MaximumCells = 256;
        private const int MaximumColumns = 32;
        private readonly Dictionary<IColumn, LinkedListNode<Bucket>> _columns = new(ReferenceEqualityComparer.Instance);
        private readonly LinkedList<Bucket> _recent = new();
        private int _count;

        public ICell? Take(ICellColumn<TModel> column, Core.Models.IRow<TModel> row)
        {
            if (!_columns.TryGetValue(column, out var node))
                return null;

            Touch(node);
            while (node.Value.Cells.Count > 0)
            {
                var cell = node.Value.Cells.Pop();
                --_count;
                if (column.TryReuseCell(cell, row))
                    return cell;
                (cell as IDisposable)?.Dispose();
            }
            return null;
        }

        public bool TryAdd(IColumn column, ICell cell)
        {
            if (cell is not IRecyclableCell recyclable || !recyclable.TrySuspend())
                return false;

            while (_count >= MaximumCells)
                Remove(_recent.First!);

            if (!_columns.TryGetValue(column, out var node))
            {
                if (_columns.Count >= MaximumColumns)
                    Remove(_recent.First!);
                node = _recent.AddLast(new Bucket(column));
                _columns.Add(column, node);
            }
            else
                Touch(node);

            node.Value.Cells.Push(cell);
            ++_count;
            return true;
        }

        public void Clear()
        {
            while (_recent.First is { } node)
                Remove(node);
        }

        private void Touch(LinkedListNode<Bucket> node)
        {
            if (node != _recent.Last)
            {
                _recent.Remove(node);
                _recent.AddLast(node);
            }
        }

        private void Remove(LinkedListNode<Bucket> node)
        {
            foreach (var cell in node.Value.Cells)
                (cell as IDisposable)?.Dispose();
            _count -= node.Value.Cells.Count;
            _columns.Remove(node.Value.Column);
            _recent.Remove(node);
        }

        private sealed class Bucket
        {
            public Bucket(IColumn column) => Column = column;
            public IColumn Column { get; }
            public Stack<ICell> Cells { get; } = new();
        }
    }
}
