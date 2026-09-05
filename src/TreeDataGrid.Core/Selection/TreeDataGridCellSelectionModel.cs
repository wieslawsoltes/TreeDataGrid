using System;
using System.Collections;
using System.Collections.Generic;
using TreeDataGridCore.Models;

namespace TreeDataGridCore.Selection
{
    /// <summary>Framework-neutral rectangular cell selection, expressed in source model indexes.</summary>
    public class TreeDataGridCellSelectionModel<TModel> : ITreeDataGridCellSelectionModel<TModel>, IDisposable
        where TModel : class
    {
        private readonly ITreeDataGridSource<TModel> _source;
        private readonly ColumnSelection _columns;
        private readonly TreeDataGridRowSelectionModel<TModel> _rows;
        private readonly CellIndexes _indexes;
        private EventHandler<TreeDataGridCellSelectionChangedEventArgs>? _untypedSelectionChanged;
        private int _batchDepth;
        private bool _changed;
        private bool _disposed;

        public TreeDataGridCellSelectionModel(ITreeDataGridSource<TModel> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _columns = new(source.Columns);
            _rows = new(source);
            _indexes = new(this);
            _columns.StateChanged += OnStateChanged;
            _rows.StateChanged += OnStateChanged;
        }

        internal ITreeSelectionModel RowSelection => _rows;

        public int Count => _columns.Count * _rows.Count;
        public bool SingleSelect
        {
            get => _rows.SingleSelect;
            set
            {
                BeginUpdate();
                try { _columns.SingleSelect = _rows.SingleSelect = value; }
                finally { EndUpdate(); }
            }
        }
        public CellIndex SelectedIndex
        {
            get => new(ColumnIndex(_columns.SelectedIndex), _rows.SelectedIndex);
            set => SetSelectedRange(value, 1, 1);
        }
        public CellIndex AnchorIndex => new(ColumnIndex(_columns.AnchorIndex), _rows.AnchorIndex);
        public CellIndex RangeAnchorIndex => new(ColumnIndex(_columns.RangeAnchorIndex), _rows.RangeAnchorIndex);
        public IReadOnlyList<CellIndex> SelectedIndexes => _indexes;
        IEnumerable? ITreeDataGridSelection.Source
        {
            get => ((ITreeDataGridSelection)_rows).Source;
            set
            {
                BeginUpdate();
                try { ((ITreeDataGridSelection)_rows).Source = value; }
                finally { EndUpdate(); }
            }
        }
        public event EventHandler<TreeDataGridCellSelectionChangedEventArgs<TModel>>? SelectionChanged;
        event EventHandler<TreeDataGridCellSelectionChangedEventArgs>? ITreeDataGridCellSelectionModel.SelectionChanged
        {
            add => _untypedSelectionChanged += value;
            remove => _untypedSelectionChanged -= value;
        }
        public bool IsSelected(CellIndex index) => IsSelected(index.ColumnIndex, index.RowIndex);
        public bool IsSelected(int columnIndex, IndexPath rowIndex) =>
            _columns.IsSelected(columnIndex) && _rows.IsSelected(rowIndex);

        public void Clear()
        {
            BeginUpdate();
            try { _columns.Clear(); _rows.Clear(); }
            finally { EndUpdate(); }
        }

        public void SetSelectedRange(CellIndex start, int columnCount, int rowCount)
        {
            var row = _source.Rows.ModelIndexToRowIndex(start.RowIndex);
            if (columnCount == 0 || rowCount == 0 || start.ColumnIndex < 0 ||
                start.ColumnIndex >= _source.Columns.Count || row < 0)
            {
                Clear();
                return;
            }
            var endColumn = End(start.ColumnIndex, SingleSelect ? 1 : columnCount, _source.Columns.Count);
            var endRow = End(row, SingleSelect ? 1 : rowCount, _source.Rows.Count);
            BeginUpdate();
            try
            {
                _columns.SelectedIndex = start.ColumnIndex;
                _rows.SelectedIndex = start.RowIndex;
                for (var i = Math.Min(start.ColumnIndex, endColumn); i <= Math.Max(start.ColumnIndex, endColumn); ++i)
                    _columns.Select(i);
                for (var i = Math.Min(row, endRow); i <= Math.Max(row, endRow); ++i)
                    _rows.Select(_source.Rows.RowIndexToModelIndex(i));
                _columns.AnchorIndex = start.ColumnIndex;
                _rows.AnchorIndex = start.RowIndex;
                _columns.RangeAnchorIndex = endColumn;
                _rows.RangeAnchorIndex = _source.Rows.RowIndexToModelIndex(endRow);
            }
            finally { EndUpdate(); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _columns.StateChanged -= OnStateChanged;
            _rows.StateChanged -= OnStateChanged;
            _columns.Detach();
            _rows.Clear();
            ((ITreeDataGridSelection)_rows).Source = null;
        }

        private static int End(int start, int count, int size) =>
            (int)Math.Clamp((long)start + count - Math.Sign(count), 0, size - 1);
        private static int ColumnIndex(IndexPath index) => index.Count == 1 ? index[0] : -1;
        private void BeginUpdate()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            ++_batchDepth;
            _columns.BeginBatchUpdate();
            _rows.BeginBatchUpdate();
        }
        private void EndUpdate()
        {
            try { _columns.EndBatchUpdate(); _rows.EndBatchUpdate(); }
            finally { if (--_batchDepth == 0 && _changed) PublishChange(); }
        }
        private void OnStateChanged(object? sender, EventArgs e)
        {
            _changed = true;
            if (_batchDepth == 0) PublishChange();
        }
        private void PublishChange()
        {
            _changed = false;
            var e = new TreeDataGridCellSelectionChangedEventArgs<TModel>();
            SelectionChanged?.Invoke(this, e);
            _untypedSelectionChanged?.Invoke(this, e);
        }

        private sealed class ColumnSelection : TreeSelectionModelBase<IColumn>
        {
            private bool _changed;
            public ColumnSelection(IEnumerable source) : base(source)
            {
                SelectionChanged += (_, _) => Changed();
                IndexesChanged += (_, _) => Changed();
                SourceReset += (_, _) => Changed();
            }
            public event EventHandler? StateChanged;
            public void Detach() { Clear(); Source = null; }
            protected internal override IEnumerable<IColumn>? GetChildren(IColumn node) => null;
            private void Changed()
            {
                if (IsSourceCollectionChanging) _changed = true;
                else StateChanged?.Invoke(this, EventArgs.Empty);
            }
            protected override void OnSourceCollectionChangeFinished()
            {
                if (!_changed) return;
                _changed = false;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private sealed class CellIndexes : IReadOnlyList<CellIndex>
        {
            private readonly TreeDataGridCellSelectionModel<TModel> _owner;
            public CellIndexes(TreeDataGridCellSelectionModel<TModel> owner) => _owner = owner;
            public int Count => _owner.Count;
            public CellIndex this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                    return new(ColumnIndex(_owner._columns.SelectedIndexes[index % _owner._columns.Count]),
                        _owner._rows.SelectedIndexes[index / _owner._columns.Count]);
                }
            }
            public IEnumerator<CellIndex> GetEnumerator()
            {
                for (var i = 0; i < Count; ++i) yield return this[i];
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
