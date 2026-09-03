using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;

namespace TreeDataGridCore
{
    /// <summary>
    /// A data source which displays a hierarchical tree where each
    /// row may have multiple columns.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    public class HierarchicalTreeDataGridSource<TModel> : NotifyingBase,
        ITreeDataGridSource<TModel>,
        IDisposable,
        IExpanderRowController<TModel>
        where TModel : class
    {
        private IEnumerable<TModel> _items;
        private TreeDataGridItemsSourceView<TModel> _itemsView;
        private IExpanderColumn<TModel>? _expanderColumn;
        private HierarchicalRows<TModel>? _rows;
        private Comparison<TModel>? _comparison;
        private ITreeDataGridSelection? _selection;
        private bool _isSelectionSet;

        public HierarchicalTreeDataGridSource(TModel item)
            : this(new[] { item })
        {
        }

        public HierarchicalTreeDataGridSource(IEnumerable<TModel> items)
        {
            _items = items;
            _itemsView = TreeDataGridItemsSourceView<TModel>.GetOrCreate(items);
            Columns = new HierarchicalColumnList();
            Columns.CollectionChanged += OnColumnsCollectionChanged;
        }

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
                }
            }
        }

        public IRows Rows => GetOrCreateRows();
        public ColumnList<TModel> Columns { get; }

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
        public bool IsHierarchical => true;
        public bool IsSorted => _comparison is not null;

        IReadOnlyList<IColumn> ITreeDataGridSource.Columns => Columns;

        public event EventHandler<RowEventArgs<HierarchicalRow<TModel>>>? RowExpanding;
        public event EventHandler<RowEventArgs<HierarchicalRow<TModel>>>? RowExpanded;
        public event EventHandler<RowEventArgs<HierarchicalRow<TModel>>>? RowCollapsing;
        public event EventHandler<RowEventArgs<HierarchicalRow<TModel>>>? RowCollapsed;
        public TResult Accept<TResult>(ITreeDataGridSourceVisitor<TResult> visitor) => visitor.Visit(this);

        public event Action? Sorted;

        public void Dispose()
        {
            _rows?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Collapses the row at the specified index.
        /// </summary>
        /// <param name="index">The index path of the row to collapse.</param>
        public void Collapse(IndexPath index) => GetOrCreateRows().Collapse(index);

        /// <summary>
        /// Collapses all rows.
        /// </summary>
        public void CollapseAll() => GetOrCreateRows().ExpandCollapseRecursive(_ => false);

        /// <summary>
        /// Expands the row at the specified index.
        /// </summary>
        /// <param name="index">The index path of the row to expand.</param>
        public void Expand(IndexPath index) => GetOrCreateRows().Expand(index);

        /// <summary>
        /// Expands all rows.
        /// </summary>
        public void ExpandAll() => GetOrCreateRows().ExpandCollapseRecursive(_ => true);

        /// <summary>
        /// Expands or collapses rows according to a condition.
        /// </summary>
        /// <param name="predicate">
        /// A function which is passed a model instance and returns a boolean value representing
        /// the desired expanded state of the row.
        /// </param>
        public void ExpandCollapseRecursive(Func<TModel, bool> predicate)
        {
            GetOrCreateRows().ExpandCollapseRecursive(predicate);
        }

        /// <summary>
        /// Expands or collapses rows according to a condition, starting from the specified row.
        /// </summary>
        /// <param name="row">
        /// The row from which to start expanding or collapsing.
        /// </param>
        /// <param name="predicate">
        /// A function which is passed a model instance and returns a boolean value representing
        /// the desired expanded state of the row.
        /// </param>
        public void ExpandCollapseRecursive(HierarchicalRow<TModel> row, Func<TModel, bool> predicate)
        {
            GetOrCreateRows().ExpandCollapseRecursive(predicate, row);
        }

        public bool TryGetModelAt(IndexPath index, [NotNullWhen(true)] out TModel? result)
        {
            if (_expanderColumn is null)
                throw new InvalidOperationException("No expander column defined.");

            var items = (IEnumerable<TModel>?)Items;
            var count = index.Count;

            for (var depth = 0; depth < count; ++depth)
            {
                var i = index[depth];

                if (i < items?.Count())
                {
                    var e = items.ElementAt(i)!;

                    if (depth < count - 1)
                    {
                        items = _expanderColumn.GetChildModels(e);
                    }
                    else
                    {
                        result = e;
                        return true;
                    }
                }
                else
                {
                    break;
                }
            }

            result = default;
            return false;
        }

        public void Sort(Comparison<TModel>? comparison)
        {
            _comparison = comparison;
            _rows?.Sort(_comparison);
        }

        IEnumerable<object>? ITreeDataGridSource.GetModelChildren(object model)
        {
            return GetModelChildren((TModel)model);
        }

        public bool SortBy(IColumn? column, ListSortDirection direction)
        {
            if (column is IColumn<TModel> columnBase &&
                Columns.Contains(columnBase) &&
                columnBase.GetComparison(direction) is Comparison<TModel> comparison)
            {
                Sort(comparison);
                Sorted?.Invoke();
                foreach (var c in Columns)
                    c.SortDirection = c == column ? (ListSortDirection?)direction : null;
                return true;
            }

            return false;
        }

        /// <summary>Restores the source order and clears all column sort indicators.</summary>
        public void ClearSort()
        {
            if (_comparison is null)
                return;
            Sort(null);
            foreach (var column in Columns)
                column.SortDirection = null;
            Sorted?.Invoke();
        }

        public void MoveRows(
            ITreeDataGridSource source,
            IEnumerable<IndexPath> indexes,
            IndexPath targetIndex,
            RowDropPosition position,
            RowMoveEffects effects)
        {
            IList<TModel> GetItems(IndexPath path)
            {
                IEnumerable<TModel>? children;

                if (path.Count == 0)
                    children = _items;
                else if (TryGetModelAt(path, out var parent))
                    children = GetModelChildren(parent);
                else
                    throw new IndexOutOfRangeException();

                if (children is null)
                    throw new InvalidOperationException("The requested drop target has no children.");

                return children as IList<TModel> ??
                    throw new InvalidOperationException("Items does not implement IList<T>.");
            }

            if (effects != RowMoveEffects.Move)
                throw new NotSupportedException("Only move is currently supported for drag/drop.");
            if (IsSorted)
                throw new NotSupportedException("Drag/drop is not supported on sorted data.");
            if (position == RowDropPosition.None)
                return;

            IList<TModel> targetItems;
            int ti;

            if (position == RowDropPosition.Inside)
            {
                targetItems = GetItems(targetIndex);
                ti = targetItems.Count;
            }
            else
            {
                targetItems = GetItems(targetIndex[..^1]);
                ti = targetIndex[^1];
            }

            if (position == RowDropPosition.After)
                ++ti;

            var sourceItems = indexes
                .OrderBy(x => x)
                .Select(x =>
                {
                    var parent = x[..^1];
                    var items = GetItems(parent);
                    var index = x[^1];
                    return (Path: x, Parent: parent, Items: items, Index: index, Item: items[index]);
                })
                .ToArray();

            foreach (var item in sourceItems)
            {
                if (ReferenceEquals(item.Items, targetItems) && item.Index < ti)
                    --ti;
            }

            var insertIndex = ti;
            var selection = _selection as ITreeSelectionModel;
            var movedSelections = selection?.SelectedIndexes
                .Select(selected =>
                {
                    for (var i = 0; i < sourceItems.Length; ++i)
                    {
                        if (selected == sourceItems[i].Path)
                        {
                            return (Found: true, SourceOffset: i,
                                Relative: default(IndexPath), Original: selected);
                        }
                    }

                    for (var i = 0; i < sourceItems.Length; ++i)
                    {
                        var sourcePath = sourceItems[i].Path;
                        if (sourcePath.IsAncestorOf(selected))
                        {
                            return (Found: true, SourceOffset: i,
                                Relative: selected.Slice(sourcePath.Count, selected.Count - sourcePath.Count),
                                Original: selected);
                        }
                    }

                    return (Found: false, SourceOffset: -1, Relative: default(IndexPath), Original: selected);
                })
                .Where(x => x.Found)
                .ToArray();

            if (selection is not null && movedSelections is not null)
            {
                foreach (var selected in movedSelections)
                    selection.Deselect(selected.Original);
            }

            foreach (var group in sourceItems.GroupBy(x => x.Parent))
            {
                foreach (var item in group.OrderByDescending(x => x.Index))
                    item.Items.RemoveAt(item.Index);
            }

            foreach (var item in sourceItems)
                targetItems.Insert(ti++, item.Item);

            if (selection is not null && movedSelections is { Length: > 0 })
            {
                IndexPath? FindItemsPath(IEnumerable<TModel> models, IndexPath parentPath)
                {
                    if (ReferenceEquals(models, targetItems))
                        return parentPath;

                    var index = 0;
                    foreach (var model in models)
                    {
                        var children = GetModelChildren(model);
                        if (children is not null && FindItemsPath(children, parentPath.Append(index)) is { } result)
                            return result;
                        ++index;
                    }

                    return null;
                }

                var targetParentPath = FindItemsPath(_items, default) ??
                    throw new InvalidOperationException("Could not resolve the moved rows' destination.");

                foreach (var selected in movedSelections)
                {
                    var path = targetParentPath.Append(insertIndex + selected.SourceOffset);
                    foreach (var relativeIndex in selected.Relative)
                        path = path.Append(relativeIndex);
                    selection.Select(path);
                }
            }
        }

        void IExpanderRowController<TModel>.OnBeginExpandCollapse(IExpanderRow<TModel> row)
        {
            if (row is HierarchicalRow<TModel> r)
            {
                if (!row.IsExpanded)
                    RowExpanding?.Invoke(this, RowEventArgs.Create(r));
                else
                    RowCollapsing?.Invoke(this, RowEventArgs.Create(r));
            }
        }

        void IExpanderRowController<TModel>.OnEndExpandCollapse(IExpanderRow<TModel> row)
        {
            if (row is HierarchicalRow<TModel> r)
            {
                if (row.IsExpanded)
                    RowExpanded?.Invoke(this, RowEventArgs.Create(r));
                else
                    RowCollapsed?.Invoke(this, RowEventArgs.Create(r));
            }
        }

        void IExpanderRowController<TModel>.OnChildCollectionChanged(
            IExpanderRow<TModel> row,
            NotifyCollectionChangedEventArgs e)
        {
        }

        internal IEnumerable<TModel>? GetModelChildren(TModel model)
        {
            _ = _expanderColumn ?? throw new InvalidOperationException("No expander column defined.");
            return _expanderColumn.GetChildModels(model);
        }

        internal int GetRowIndex(in IndexPath index, int fromRowIndex = 0)
        {
            var result = -1;
            _rows?.TryGetRowIndex(index, out result, fromRowIndex);
            return result;
        }

        private HierarchicalRows<TModel> GetOrCreateRows()
        {
            if (_rows is null)
            {
                if (Columns.Count == 0)
                    throw new InvalidOperationException("No columns defined.");
                if (_expanderColumn is null)
                    throw new InvalidOperationException("No expander column defined.");
                _rows = new HierarchicalRows<TModel>(this, _itemsView, _expanderColumn, _comparison);
            }

            return _rows;
        }

        private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    HandleAdd(e.NewItems);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    HandleRemoveReplaceOrMove(e.OldItems, "removed");
                    break;

                case NotifyCollectionChangedAction.Replace:
                    HandleRemoveReplaceOrMove(e.OldItems, "replaced");
                    break;

                case NotifyCollectionChangedAction.Move:
                    // Ordering changes do not replace the expander used by the row projection.
                    break;

                case NotifyCollectionChangedAction.Reset:
                    _expanderColumn = Columns.OfType<IExpanderColumn<TModel>>().SingleOrDefault();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void HandleAdd(IList? newItems)
        {
            if (newItems is not null)
            {
                foreach (var i in newItems)
                {
                    if (i is IExpanderColumn<TModel> expander)
                    {
                        if (_expanderColumn is not null)
                        {
                            throw new InvalidOperationException("Only one expander column is allowed.");
                        }

                        _expanderColumn = expander;
                        break;
                    }
                }
            }
        }

        private void HandleRemoveReplaceOrMove(IList? items, string action)
        {
            if (items is not null)
            {
                foreach (var i in items)
                {
                    if (i is IExpanderColumn<TModel> && _expanderColumn is not null)
                    {
                        throw new InvalidOperationException($"The expander column cannot be {action}.");
                    }
                }
            }
        }

        private sealed class HierarchicalColumnList : ColumnList<TModel>
        {
            private bool _applyingReset;

            public override void InsertRange(int index, Action<Action<IColumn<TModel>>> action)
            {
                var proposed = new List<IColumn<TModel>>();
                action(proposed.Add);

                if (this.Count(x => x is IExpanderColumn<TModel>) +
                    proposed.Count(x => x is IExpanderColumn<TModel>) > 1)
                {
                    throw new InvalidOperationException("Only one expander column is allowed.");
                }

                base.InsertRange(index, add =>
                {
                    foreach (var column in proposed)
                        add(column);
                });
            }

            public override void RemoveRange(int index, int count)
            {
                for (var i = 0; i < count; ++i)
                {
                    if (this[index + i] is IExpanderColumn<TModel>)
                        throw new InvalidOperationException("The expander column cannot be removed.");
                }

                base.RemoveRange(index, count);
            }

            public override void Reset(Action<IList<IColumn<TModel>>> action)
            {
                var currentExpander = this.OfType<IExpanderColumn<TModel>>().SingleOrDefault();
                var proposed = new List<IColumn<TModel>>(this);
                action(proposed);
                var proposedExpanders = proposed.OfType<IExpanderColumn<TModel>>().ToArray();

                if (proposedExpanders.Length > 1 ||
                    (currentExpander is not null &&
                        (proposedExpanders.Length == 0 || !ReferenceEquals(currentExpander, proposedExpanders[0]))))
                {
                    throw new InvalidOperationException("A reset cannot remove or replace the expander column.");
                }

                _applyingReset = true;
                try
                {
                    base.Reset(columns =>
                    {
                        columns.Clear();
                        foreach (var column in proposed)
                            columns.Add(column);
                    });
                }
                finally
                {
                    _applyingReset = false;
                }
            }

            protected override void ClearItems()
            {
                if (!_applyingReset && this.Any(x => x is IExpanderColumn<TModel>))
                    throw new InvalidOperationException("The expander column cannot be removed.");

                base.ClearItems();
            }

            protected override void InsertItem(int index, IColumn<TModel> item)
            {
                if (!_applyingReset && item is IExpanderColumn<TModel> &&
                    this.Any(x => x is IExpanderColumn<TModel>))
                {
                    throw new InvalidOperationException("Only one expander column is allowed.");
                }

                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                if (!_applyingReset && this[index] is IExpanderColumn<TModel>)
                    throw new InvalidOperationException("The expander column cannot be removed.");

                base.RemoveItem(index);
            }

            protected override void SetItem(int index, IColumn<TModel> item)
            {
                if (!_applyingReset &&
                    (this[index] is IExpanderColumn<TModel> || item is IExpanderColumn<TModel>))
                {
                    throw new InvalidOperationException("The expander column cannot be replaced.");
                }

                base.SetItem(index, item);
            }
        }
    }
}
