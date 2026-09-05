using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia.Controls.Adapters;
using Avalonia.Controls.Models;
using Avalonia.Controls.Models.TreeDataGrid;
using Core = global::TreeDataGridCore;

namespace Avalonia.Controls.Presentation
{
    // The items and collection notifications are the Core objects themselves. This view object
    // only adds cell realization; it maintains no second row collection or per-row wrappers.
    internal sealed class TreeDataGridRows<TModel> : ReadOnlyListBase<Core.Models.IRow>, ITreeDataGridRows, IReusableCellRows, IRecyclingCellRows, IDisposable where TModel : class
    {
        private Core.Models.IRows _rows;
        private bool _subscribed;
        private CellModelPool<TModel>? _cellPool;
        public TreeDataGridRows(Core.Models.IRows rows) { _rows = rows; Resume(); }
        internal void Resume() { if (!_subscribed) { _rows.CollectionChanged += OnCollectionChanged; _subscribed = true; } }
        internal void Suspend() { if (_subscribed) { _rows.CollectionChanged -= OnCollectionChanged; _subscribed = false; } ClearCellPool(); }
        internal void ClearCellPool() => _cellPool?.Clear();
        internal void SetRows(Core.Models.IRows rows)
        {
            if (ReferenceEquals(_rows, rows)) return;
            ClearCellPool();
            if (_subscribed) _rows.CollectionChanged -= OnCollectionChanged;
            _rows = rows;
            if (_subscribed) _rows.CollectionChanged += OnCollectionChanged;
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
        }
        public override int Count => _rows.Count;
        public override Core.Models.IRow this[int index] => _rows[index];
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => CollectionChanged?.Invoke(this, e);
        public void Dispose() => Suspend();
        public (int index, double y) GetRowAt(double y) => Math.Abs(y) < double.Epsilon ? (0, 0) : (-1, -1);
        public int ModelIndexToRowIndex(IndexPath index) => _rows.ModelIndexToRowIndex(index.ToCore());
        public IndexPath RowIndexToModelIndex(int index) => _rows.RowIndexToModelIndex(index).ToAvalonia();
        public ICell RealizeCell(IColumn column, int columnIndex, int rowIndex)
        {
            var cellColumn = (ICellColumn<TModel>)column;
            var row = (Core.Models.IRow<TModel>)_rows[rowIndex];
            return _cellPool?.Take(cellColumn, row) ?? cellColumn.CreateCell(row);
        }
        bool IRecyclingCellRows.TryRecycleCell(IColumn column, ICell cell) =>
            _subscribed && (_cellPool ??= new()).TryAdd(column, cell);
        public void UnrealizeCell(ICell cell, int columnIndex, int rowIndex) => (cell as IDisposable)?.Dispose();
        bool IReusableCellRows.TryReuseCell(IColumn column, ICell cell, int rowIndex) => ((ICellColumn<TModel>)column).TryReuseCell(cell, (Core.Models.IRow<TModel>)_rows[rowIndex]);
        public override IEnumerator<Core.Models.IRow> GetEnumerator() => _rows.GetEnumerator();
    }
}
