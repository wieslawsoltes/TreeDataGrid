using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;

namespace TreeDataGridCore
{
    /// <summary>
    /// A data source which displays a flat grid.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    public class FlatTreeDataGridSource<TModel> : NotifyingBase,
        ITreeDataGridSource<TModel>,
        IDisposable
            where TModel : class
    {
        private IEnumerable<TModel> _items;
        private TreeDataGridItemsSourceView<TModel> _itemsView;
        private AnonymousSortableRows<TModel>? _rows;
        private IComparer<TModel>? _comparer;
        private ITreeDataGridSelection? _selection;
        private bool _isSelectionSet;

        public FlatTreeDataGridSource(IEnumerable<TModel> items)
        {
            _items = items;
            _itemsView = TreeDataGridItemsSourceView<TModel>.GetOrCreate(items);
            Columns = new ColumnList<TModel>();
        }

        public ColumnList<TModel> Columns { get; }
        public IRows Rows => _rows ??= CreateRows();
        IReadOnlyList<IColumn> ITreeDataGridSource.Columns => Columns;

        public IEnumerable<TModel> Items
        {
            get => _items;
            set
            {
                if (_items != value)
                {
                    _items = value;
                    _itemsView = TreeDataGridItemsSourceView<TModel>.GetOrCreate(value);
                    _rows?.SetItems(_itemsView);
                    if (_selection is object)
                        _selection.Source = value;
                    RaisePropertyChanged();
                }
            }
        }

        public ITreeDataGridSelection? Selection
        {
            get
            {
                if (_selection == null && !_isSelectionSet)
                    _selection = new TreeDataGridRowSelectionModel<TModel>(this);
                return _selection;
            }
            set
            {
                if (_selection != value || value is null)
                {
                    if (value is not null && value.Source != _items)
                        throw new InvalidOperationException("Selection source must be set to Items.");
                    _selection = value;
                    _isSelectionSet = true;
                    RaisePropertyChanged();
                }
            }
        }

        IEnumerable<object> ITreeDataGridSource.Items => Items;

        public ITreeDataGridRowSelectionModel<TModel>? RowSelection => Selection as ITreeDataGridRowSelectionModel<TModel>;
        public bool IsHierarchical => false;
        public bool IsSorted => _comparer is not null;

        public TResult Accept<TResult>(ITreeDataGridSourceVisitor<TResult> visitor) => visitor.Visit(this);

        public event Action? Sorted;

        public void Dispose()
        {
            _rows?.Dispose();
            GC.SuppressFinalize(this);
        }

        public void MoveRows(
            ITreeDataGridSource source,
            IEnumerable<IndexPath> indexes,
            IndexPath targetIndex,
            RowDropPosition position,
            RowMoveEffects effects)
        {
            if (effects != RowMoveEffects.Move)
                throw new NotSupportedException("Only move is currently supported for drag/drop.");
            if (IsSorted)
                throw new NotSupportedException("Drag/drop is not supported on sorted data.");
            if (position is not RowDropPosition.None and not RowDropPosition.Before and
                not RowDropPosition.After and not RowDropPosition.Inside)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            if (position == RowDropPosition.None)
                return;
            if (position == RowDropPosition.Inside)
                throw new ArgumentException("Invalid drop position.", nameof(position));
            var orderedIndexes = indexes.OrderByDescending(x => x).ToArray();
            if (orderedIndexes.Any(x => x.Count != 1))
                throw new ArgumentException("Invalid source index.", nameof(indexes));
            for (var i = 1; i < orderedIndexes.Length; ++i)
            {
                if (orderedIndexes[i] == orderedIndexes[i - 1])
                    throw new ArgumentException("Duplicate source index.", nameof(indexes));
            }
            if (targetIndex.Count != 1)
                throw new ArgumentException("Invalid target index.", nameof(targetIndex));
            if (_items is not IList<TModel> items)
                throw new InvalidOperationException("Items does not implement IList<T>.");
            if (items.IsReadOnly)
                throw new InvalidOperationException("Items is read-only.");

            foreach (var index in orderedIndexes)
            {
                if ((uint)index[0] >= (uint)items.Count)
                    throw new ArgumentOutOfRangeException(nameof(indexes));
            }

            if ((uint)targetIndex[0] >= (uint)items.Count)
                throw new ArgumentOutOfRangeException(nameof(targetIndex));

            var ti = targetIndex[0];

            if (position == RowDropPosition.After)
                ++ti;

            var selection = _selection as ITreeSelectionModel;
            var selectedIndexes = selection?.SelectedIndexes
                .Where(x => x.Count == 1)
                .Select(x => x[0])
                .ToArray();
            var primaryIndex = selection?.SelectedIndex.Count == 1 ? selection.SelectedIndex[0] : -1;
            var indexMap = Enumerable.Range(0, items.Count).ToList();
            var sourceItems = new List<(TModel Item, int OriginalIndex)>();

            foreach (var src in orderedIndexes)
            {
                var i = src[0];
                sourceItems.Add((items[i], indexMap[i]));
                items.RemoveAt(i);
                indexMap.RemoveAt(i);

                if (i < ti)
                    --ti;
            }

            for (var si = sourceItems.Count - 1; si >= 0; --si)
            {
                items.Insert(ti, sourceItems[si].Item);
                indexMap.Insert(ti++, sourceItems[si].OriginalIndex);
            }

            if (selection is not null && selectedIndexes is { Length: > 0 })
            {
                selection.BeginBatchUpdate();
                try
                {
                    selection.Clear();

                    if (primaryIndex >= 0)
                        selection.Select(new IndexPath(indexMap.IndexOf(primaryIndex)));

                    foreach (var selectedIndex in selectedIndexes)
                    {
                        if (selectedIndex != primaryIndex)
                            selection.Select(new IndexPath(indexMap.IndexOf(selectedIndex)));
                    }
                }
                finally
                {
                    selection.EndBatchUpdate();
                }
            }
        }

        public bool SortBy(IColumn? column, ListSortDirection direction)
        {
            if (column is IColumn<TModel> typedColumn)
            {
                if (!Columns.Contains(typedColumn))
                    return true;

                var comparer = typedColumn.GetComparison(direction);

                if (comparer is not null)
                {
                    _comparer = comparer is not null ? new FuncComparer<TModel>(comparer) : null;
                    _rows?.Sort(_comparer);
                    Sorted?.Invoke();
                    foreach (var c in Columns)
                        c.SortDirection = c == column ? direction : null;
                }
                return true;
            }

            return false;
        }

        /// <summary>Restores the source order and clears all column sort indicators.</summary>
        public void ClearSort()
        {
            if (_comparer is null)
                return;
            _comparer = null;
            _rows?.Sort(null);
            foreach (var column in Columns)
                column.SortDirection = null;
            Sorted?.Invoke();
        }

        IEnumerable<object> ITreeDataGridSource.GetModelChildren(object model)
        {
            return Enumerable.Empty<object>();
        }

        private AnonymousSortableRows<TModel> CreateRows()
        {
            return new AnonymousSortableRows<TModel>(_itemsView, _comparer);
        }
    }
}
