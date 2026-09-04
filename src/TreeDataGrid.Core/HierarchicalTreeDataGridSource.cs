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
        IExpanderRowController<TModel>,
        IChildCollectionReplacementController<TModel>
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
                    RaisePropertyChanged();
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
            if (position is not RowDropPosition.None and not RowDropPosition.Before and
                not RowDropPosition.After and not RowDropPosition.Inside)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            if (position == RowDropPosition.None)
                return;

            var orderedIndexes = indexes.OrderBy(x => x).ToArray();
            if (orderedIndexes.Length == 0)
                return;

            for (var i = 1; i < orderedIndexes.Length; ++i)
            {
                if (orderedIndexes[i] == orderedIndexes[i - 1])
                    throw new ArgumentException("Duplicate source index.", nameof(indexes));
            }

            foreach (var sourceIndex in orderedIndexes)
            {
                if (sourceIndex.IsAncestorOf(targetIndex) ||
                    (sourceIndex == targetIndex && position == RowDropPosition.Inside))
                {
                    throw new InvalidOperationException(
                        "A row cannot be moved into itself or one of its descendants.");
                }
            }

            IList<TModel> targetItems;
            IndexPath originalTargetParentPath;
            int ti;

            if (position == RowDropPosition.Inside)
            {
                targetItems = GetItems(targetIndex);
                originalTargetParentPath = targetIndex;
                ti = targetItems.Count;
            }
            else
            {
                if (targetIndex.Count == 0)
                    throw new ArgumentException("Invalid target index.", nameof(targetIndex));
                originalTargetParentPath = targetIndex[..^1];
                targetItems = GetItems(originalTargetParentPath);
                ti = targetIndex[^1];
                if ((uint)ti >= (uint)targetItems.Count)
                    throw new ArgumentOutOfRangeException(nameof(targetIndex));
            }

            if (position == RowDropPosition.After)
                ++ti;

            if (targetItems.IsReadOnly)
                throw new InvalidOperationException("The requested drop target is read-only.");

            var sourceItems = orderedIndexes
                .Select(x =>
                {
                    if (x.Count == 0)
                        throw new ArgumentException("Invalid source index.", nameof(indexes));
                    var parent = x[..^1];
                    var items = GetItems(parent);
                    var index = x[^1];
                    if ((uint)index >= (uint)items.Count)
                        throw new ArgumentOutOfRangeException(nameof(indexes));
                    return (Path: x, Parent: parent, Items: items, Index: index, Item: items[index]);
                })
                .ToArray();

            var sourceGroups = sourceItems
                .GroupBy(x => x.Items, ReferenceEqualityComparer.Instance)
                .Select(x => x.ToArray())
                .ToArray();

            if (sourceGroups.Any(group =>
                group.Select(x => x.Index).Distinct().Count() != group.Length))
            {
                throw new ArgumentException("Duplicate physical source index.", nameof(indexes));
            }

            bool ContainsTargetCollection(TModel root)
            {
                if (_expanderColumn is null)
                    return false;

                var pending = new Stack<TModel>();
                var visited = new HashSet<TModel>(ReferenceEqualityComparer.Instance);
                pending.Push(root);

                while (pending.Count > 0)
                {
                    var model = pending.Pop();
                    if (!visited.Add(model))
                        continue;

                    var children = GetModelChildren(model);
                    if (ReferenceEquals(children, targetItems))
                        return true;
                    if (children is not null)
                    {
                        foreach (var child in children)
                            pending.Push(child);
                    }
                }

                return false;
            }

            if (sourceItems.Any(x =>
                !ReferenceEquals(x.Items, targetItems) && ContainsTargetCollection(x.Item)))
            {
                throw new InvalidOperationException(
                    "A row cannot be moved into a collection in its own subtree.");
            }

            if (sourceItems.Any(x => x.Items.IsReadOnly))
                throw new InvalidOperationException("One or more source collections are read-only.");

            var originalTargetOffset = ti;
            ti -= sourceItems.Count(item =>
                ReferenceEquals(item.Items, targetItems) && item.Index < originalTargetOffset);

            IndexPath MapAfterRemovals(IndexPath original)
            {
                var result = original.ToArray();

                for (var depth = 0; depth < result.Length; ++depth)
                {
                    var parent = original.Slice(0, depth);
                    var parentItems = GetItems(parent);
                    result[depth] -= sourceItems.Count(x =>
                        ReferenceEquals(x.Items, parentItems) && x.Index < original[depth]);
                }

                return new IndexPath(result);
            }

            var insertIndex = ti;
            var targetParentPath = MapAfterRemovals(originalTargetParentPath);

            bool IsValidModelPath(IndexPath path)
            {
                if (path.Count == 0)
                    return false;

                try
                {
                    var items = GetItems(path[..^1]);
                    return (uint)path[^1] < (uint)items.Count;
                }
                catch (IndexOutOfRangeException)
                {
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            (IndexPath Path, bool Moved) MapFinalPath(IndexPath original)
            {
                var occurrences = Enumerable.Range(0, original.Count)
                    .Select(depth => (Items: GetItems(original.Slice(0, depth)),
                        Index: original[depth]))
                    .ToArray();
                var afterRemoval = MapAfterRemovals(original);
                var sourceOffset = -1;
                var sourceDepth = -1;

                for (var i = 0; i < sourceItems.Length; ++i)
                {
                    for (var depth = 0; depth < occurrences.Length; ++depth)
                    {
                        var occurrence = occurrences[depth];
                        if (ReferenceEquals(occurrence.Items, sourceItems[i].Items) &&
                            occurrence.Index == sourceItems[i].Index && depth + 1 > sourceDepth)
                        {
                            sourceOffset = i;
                            sourceDepth = depth + 1;
                        }
                    }
                }

                if (sourceOffset >= 0)
                {
                    var path = targetParentPath.Append(insertIndex + sourceOffset);
                    foreach (var relativeIndex in afterRemoval.Slice(
                        sourceDepth, afterRemoval.Count - sourceDepth))
                    {
                        path = path.Append(relativeIndex);
                    }

                    return (path, true);
                }

                var indexes = afterRemoval.ToArray();
                for (var depth = 0; depth < occurrences.Length; ++depth)
                {
                    if (ReferenceEquals(occurrences[depth].Items, targetItems) &&
                        indexes[depth] >= insertIndex)
                    {
                        indexes[depth] += sourceItems.Length;
                    }
                }

                return (new IndexPath(indexes), false);
            }

            var selection = _selection as ITreeSelectionModel ??
                (_selection as TreeDataGridCellSelectionModel<TModel>)?.RowSelection;
            var originalPrimarySelection = selection?.SelectedIndex ?? default;
            var originalSelections = selection?.SelectedIndexes.ToArray();
            var mappedAnchor = selection?.AnchorIndex.Count > 0 &&
                IsValidModelPath(selection.AnchorIndex) ?
                MapFinalPath(selection.AnchorIndex).Path : default;
            var mappedRangeAnchor = selection?.RangeAnchorIndex.Count > 0 &&
                IsValidModelPath(selection.RangeAnchorIndex) ?
                MapFinalPath(selection.RangeAnchorIndex).Path : default;
            var mappedSelections = originalSelections?
                .Select(original => (Original: original, Mapped: MapFinalPath(original)))
                .ToArray();
            var mappedExpansions = _rows?.GetExpandedModelIndexes()
                .Select(x => MapFinalPath(x).Path)
                .Distinct()
                .OrderBy(x => x.Count)
                .ToArray();
            var refreshRows = _rows is not null &&
                (targetItems is not INotifyCollectionChanged ||
                    sourceItems.Any(x => x.Items is not INotifyCollectionChanged));

            if (selection is not null && mappedSelections is not null)
            {
                foreach (var selected in mappedSelections.Where(x => x.Mapped.Moved))
                    selection.Deselect(selected.Original);
            }

            foreach (var group in sourceGroups)
            {
                foreach (var item in group.OrderByDescending(x => x.Index))
                    item.Items.RemoveAt(item.Index);
            }

            foreach (var item in sourceItems)
                targetItems.Insert(ti++, item.Item);

            if (refreshRows)
                _rows!.SetItems(_itemsView);

            if (_rows is not null && mappedExpansions is not null)
            {
                foreach (var expanded in mappedExpansions)
                    _rows.Expand(expanded);
            }

            if (selection is not null)
            {
                selection.BeginBatchUpdate();
                try
                {
                    if (originalSelections is { Length: > 0 })
                    {
                        var restoredSelections = mappedSelections!
                            .Select(x => (x.Original, Path: x.Mapped.Path))
                            .ToArray();
                        var primaryMovedIndex = Array.FindIndex(restoredSelections,
                            x => x.Original == originalPrimarySelection);

                        selection.Clear();

                        if (primaryMovedIndex >= 0)
                            selection.Select(restoredSelections[primaryMovedIndex].Path);

                        for (var i = 0; i < restoredSelections.Length; ++i)
                        {
                            if (i != primaryMovedIndex)
                                selection.Select(restoredSelections[i].Path);
                        }
                    }

                    selection.AnchorIndex = mappedAnchor;
                    selection.RangeAnchorIndex = mappedRangeAnchor;
                }
                finally
                {
                    selection.EndBatchUpdate();
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


        void IChildCollectionReplacementController<TModel>.OnChildCollectionReplaced(
            IExpanderRow<TModel> row,
            IEnumerable<TModel>? children)
        {
            if (_selection is TreeSelectionModelBase<TModel> selection &&
                row is IModelIndexableRow indexable)
            {
                selection.ResetChildrenSource(indexable.ModelIndexPath, children);
            }
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
