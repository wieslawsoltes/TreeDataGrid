using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls.Models;
using Avalonia.Controls.Models.TreeDataGrid;
using Core = global::TreeDataGridCore;

namespace Avalonia.Controls.Adapters
{
    internal sealed class CoreRowsAdapter<TModel> : ReadOnlyListBase<IRow>, IRows, IReusableCellRows, IDisposable where TModel : class
    {
        private readonly Core.Models.IRows _rows;
        private readonly ConditionalWeakTable<Core.Models.IRow, RowAdapter> _wrappers = new();
        public CoreRowsAdapter(Core.Models.IRows rows) { _rows = rows; _rows.CollectionChanged += OnCollectionChanged; }
        public override int Count => _rows.Count;
        public override IRow this[int index] => Wrap(_rows[index]);
        private RowAdapter? _anonymousRow;
        private RowAdapter Wrap(Core.Models.IRow row)
        {
            // Flat sources intentionally reuse a row until the next retrieval. Preserve that
            // contract without weak-table entries for each temporary collection-change row.
            if (row is Core.Models.AnonymousRow<TModel> anonymous)
            {
                _anonymousRow ??= new RowAdapter(anonymous);
                _anonymousRow.SetRow(anonymous);
                return _anonymousRow;
            }
            return _wrappers.GetValue(row, r => r is Core.Models.IExpanderRow<TModel> expander ? new ExpanderRowAdapter(expander) : new RowAdapter((Core.Models.IRow<TModel>)r));
        }
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        public (int index, double y) GetRowAt(double y) => Math.Abs(y) < double.Epsilon ? (0, 0) : (-1, -1);
        public int ModelIndexToRowIndex(IndexPath index) => _rows.ModelIndexToRowIndex(index.ToCore());
        public IndexPath RowIndexToModelIndex(int index) => _rows.RowIndexToModelIndex(index).ToAvalonia();
        public ICell RealizeCell(IColumn column, int columnIndex, int rowIndex) => ((IColumn<TModel>)column).CreateCell((IRow<TModel>)this[rowIndex]);
        public void UnrealizeCell(ICell cell, int columnIndex, int rowIndex) => (cell as IDisposable)?.Dispose();
        bool IReusableCellRows.TryReuseCell(IColumn column, ICell cell, int rowIndex) => column is IReusableCellColumn<TModel> reusable && reusable.TryReuseCell(cell, (IRow<TModel>)this[rowIndex]);
        public override IEnumerator<IRow> GetEnumerator() { for (var i = 0; i < Count; ++i) yield return this[i]; }
        public void Dispose() => _rows.CollectionChanged -= OnCollectionChanged;
        private IList Map(IList items) => new EventRows(this, items);
        private sealed class EventRows : IList
        {
            private readonly CoreRowsAdapter<TModel> _owner;
            private readonly IList _items;
            public EventRows(CoreRowsAdapter<TModel> owner, IList items) { _owner = owner; _items = items; }
            public object? this[int index] { get => _owner.Wrap((Core.Models.IRow)_items[index]!); set => throw new NotSupportedException(); }
            public int Count => _items.Count;
            public bool IsReadOnly => true;
            public bool IsFixedSize => true;
            public bool IsSynchronized => false;
            public object SyncRoot => this;
            public IEnumerator GetEnumerator() { for (var i = 0; i < Count; ++i) yield return this[i]; }
            public void CopyTo(Array array, int index) { for (var i = 0; i < Count; ++i) array.SetValue(this[i], index + i); }
            public int Add(object? value) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public bool Contains(object? value) => throw new NotSupportedException();
            public int IndexOf(object? value) => throw new NotSupportedException();
            public void Insert(int index, object? value) => throw new NotSupportedException();
            public void Remove(object? value) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
        }
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (CollectionChanged is null) return;
            var mapped = e.Action switch
            {
                NotifyCollectionChangedAction.Add => new(e.Action, Map(e.NewItems!), e.NewStartingIndex),
                NotifyCollectionChangedAction.Remove => new(e.Action, Map(e.OldItems!), e.OldStartingIndex),
                NotifyCollectionChangedAction.Replace => new(e.Action, Map(e.NewItems!), Map(e.OldItems!), e.NewStartingIndex),
                NotifyCollectionChangedAction.Move => new(e.Action, Map(e.NewItems!), e.NewStartingIndex, e.OldStartingIndex),
                _ => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)
            };
            CollectionChanged(this, mapped);
        }
        private class RowAdapter : IRow<TModel>, IModelIndexableRow, IIndentedRow
        {
            protected Core.Models.IRow<TModel> Row;
            public RowAdapter(Core.Models.IRow<TModel> row) => Row = row;
            public void SetRow(Core.Models.IRow<TModel> row) => Row = row;
            public TModel Model => Row.Model;
            public object? Header => Row.Header is Core.IndexPath path ? path.ToAvalonia() : Row.Header;
            public GridLength Height { get => Row.Height.ToAvalonia(); set => Row.Height = value.ToCore(); }
            public int ModelIndex => ((Core.Models.IModelIndexableRow)Row).ModelIndex;
            public IndexPath ModelIndexPath => ((Core.Models.IModelIndexableRow)Row).ModelIndexPath.ToAvalonia();
            public int Indent => Row is Core.Models.IIndentedRow indented ? indented.Indent : 0;
            public void UpdateModelIndex(int delta) => Row.UpdateModelIndex(delta);
        }
        private sealed class ExpanderRowAdapter : RowAdapter, IExpanderRow<TModel>
        {
            private readonly Core.Models.IExpanderRow<TModel> _expander;
            public ExpanderRowAdapter(Core.Models.IExpanderRow<TModel> row) : base(row) => _expander = row;
            public bool IsExpanded { get => _expander.IsExpanded; set => _expander.IsExpanded = value; }
            public bool ShowExpander => _expander.ShowExpander;
            public event PropertyChangedEventHandler? PropertyChanged { add => _expander.PropertyChanged += value; remove => _expander.PropertyChanged -= value; }
            public void UpdateShowExpander(IExpanderCell cell, bool value) => _expander.UpdateShowExpander(value);
        }
    }
}
