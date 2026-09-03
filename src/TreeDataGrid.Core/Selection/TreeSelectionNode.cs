using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using TreeDataGridCore.Models;

#nullable enable

namespace TreeDataGridCore.Selection
{
#pragma warning disable CS0436 // Type conflicts with imported type
    internal class TreeSelectionNode<T> : SelectionNodeBase<T>
#pragma warning restore CS0436 // Type conflicts with imported type
    {
        private readonly TreeSelectionModelBase<T> _owner;
        private List<TreeSelectionNode<T>?>? _children;

        public TreeSelectionNode(TreeSelectionModelBase<T> owner)
        {
            _owner = owner;
            RangesEnabled = true;
        }

        public TreeSelectionNode(
            TreeSelectionModelBase<T> owner,
            TreeSelectionNode<T> parent,
            int index)
            : this(owner)
        {
            Path = parent.Path.Append(index);
            if (parent.ItemsView is not null)
                Source = _owner.GetChildren(parent.ItemsView[index]);
        }

        public IndexPath Path { get; private set; }

        public new IEnumerable? Source
        {
            get => base.Source;
            set => base.Source = value;
        }

        public void ResetSource(IEnumerable? source)
        {
            if (ReferenceEquals(Source, source))
                return;
            Source = source;
            OnSourceReset();
        }

        public bool HasChildren
        {
            get
            {
                if (_children is null)
                    return false;

                foreach (var child in _children)
                {
                    if (child is not null)
                        return true;
                }

                return false;
            }
        }

        public IReadOnlyList<TreeSelectionNode<T>?>? Children => _children;

        public void Clear(TreeSelectionModelBase<T>.Operation operation)
        {
            if (Ranges.Count > 0)
            {
                operation.DeselectedRanges ??= new();
                foreach (var range in Ranges)
                    operation.DeselectedRanges.Add(Path, range);
            }

            if (_children is not null)
            {
                foreach (var child in _children)
                    child?.Clear(operation);
            }
        }

        public int CommitSelect(IndexRange range) => CommitSelect(range.Begin, range.End);
        public int CommitDeselect(IndexRange range) => CommitDeselect(range.Begin, range.End);
        public TreeSelectionNode<T>? GetChild(int index) => index < _children?.Count ? _children[index] : null;

        public TreeSelectionNode<T>? GetOrCreateChild(int index)
        {
            if (GetChild(index) is TreeSelectionNode<T> result)
                return result;

            var childCount = ItemsView is not null ? ItemsView.Count : Math.Max(_children?.Count ?? 0, index);

            if (index < childCount)
            {
                _children ??= new List<TreeSelectionNode<T>?>();
                Resize(_children, childCount);
                return _children[index] ??= new TreeSelectionNode<T>(_owner, this, index);
            }

            return null;
        }

        public void PruneEmptyChildren()
        {
            if (_children is null)
                return;

            for (var i = 0; i < _children.Count; ++i)
            {
                if (_children[i] is TreeSelectionNode<T> node)
                {
                    node.PruneEmptyChildren();

                    if (node.Ranges.Count == 0 && !node.HasChildren)
                    {
                        node.Source = null;
                        _children[i] = null;
                    }
                }
            }
        }

        protected override void OnSourceCollectionChangeStarted()
        {
            _owner.OnNodeCollectionChangeStarted();
        }

        protected override void OnSourceCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            var shiftStartIndex = 0;
            var shiftEndIndex = -1;
            var shiftDelta = 0;
            var indexesChanged = false;
            List<T?>? removed = null;
            var moveOldIndex = -1;
            var moveInsertIndex = -1;
            var moveCount = 0;
            var replaceOldIndex = -1;
            var replaceNewIndex = -1;
            var replaceOldCount = 0;
            var replaceNewCount = 0;

            // Adjust the selection in this node according to the collection change.
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    shiftStartIndex = e.NewStartingIndex;
                    shiftDelta = e.NewItems!.Count;
                    indexesChanged = OnItemsAdded(shiftStartIndex, e.NewItems).ShiftDelta > 0;
                    break;
                case NotifyCollectionChangedAction.Remove:
                    shiftStartIndex = e.OldStartingIndex;
                    shiftDelta = -e.OldItems!.Count;
                    var change = OnItemsRemoved(shiftStartIndex, e.OldItems);
                    indexesChanged = change.ShiftDelta != 0;
                    removed = change.RemovedItems;
                    break;
                case NotifyCollectionChangedAction.Replace:
                    replaceOldIndex = e.OldStartingIndex;
                    replaceNewIndex = e.NewStartingIndex >= 0 ? e.NewStartingIndex : replaceOldIndex;
                    replaceOldCount = e.OldItems!.Count;
                    replaceNewCount = e.NewItems!.Count;
                    var removeChange = OnItemsRemoved(e.OldStartingIndex, e.OldItems!);
                    var addChange = OnItemsAdded(replaceNewIndex, e.NewItems!);
                    shiftStartIndex = Math.Min(replaceOldIndex, replaceNewIndex);
                    shiftDelta = replaceNewCount - replaceOldCount;
                    indexesChanged = shiftDelta != 0 || removeChange.ShiftDelta != 0 ||
                        addChange.ShiftDelta != 0;
                    removed = removeChange.RemovedItems;
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldStartingIndex < 0)
                    {
                        OnSourceReset();
                        return;
                    }

                    moveOldIndex = e.OldStartingIndex;
                    moveCount = e.OldItems!.Count;
                    if (moveOldIndex == e.NewStartingIndex || moveCount == 0)
                        return;
                    shiftStartIndex = Math.Min(e.OldStartingIndex, e.NewStartingIndex);
                    shiftEndIndex = Math.Max(
                        e.OldStartingIndex + moveCount - 1,
                        e.NewStartingIndex + moveCount - 1);
                    shiftDelta = e.OldStartingIndex < e.NewStartingIndex ? -moveCount : moveCount;
                    indexesChanged = true;

                    var removeMoveChange = OnItemsRemoved(e.OldStartingIndex, e.OldItems!);
                    var insertIndex = e.NewStartingIndex;

                    OnItemsAdded(insertIndex, e.NewItems!);
                    moveInsertIndex = insertIndex;

                    if (removeMoveChange.DeselectedRanges is { Count: > 0 } deselectedRanges)
                    {
                        var movedRanges = new List<IndexRange>(deselectedRanges.Count);

                        foreach (var range in deselectedRanges)
                        {
                            var relativeBegin = range.Begin - e.OldStartingIndex;
                            var begin = insertIndex + relativeBegin;
                            var end = begin + (range.End - range.Begin);
                            movedRanges.Add(new IndexRange(begin, end));

                            if (RangesEnabled)
                            {
                                CommitSelect(begin, end);
                            }
                        }

                        var movedItems = removeMoveChange.RemovedItems is { Count: > 0 } items
                            ? (IReadOnlyList<T?>)items
                            : Array.Empty<T?>();

                        OnSelectionMoved(
                            e.OldStartingIndex,
                            insertIndex,
                            deselectedRanges,
                            movedRanges,
                            movedItems);

                        removed = null;
                    }
                    else
                    {
                        removed = removeMoveChange.RemovedItems;
                    }

                    break;
                case NotifyCollectionChangedAction.Reset:
                    OnSourceReset();
                    return;
                default:
                    throw new NotSupportedException($"Collection {e.Action} not supported.");
            }

            // Adjust the paths of any child nodes.
            if (_children is not null)
            {
                if (e.Action == NotifyCollectionChangedAction.Replace)
                {
                    AdjustChildrenForReplace(
                        replaceOldIndex,
                        replaceOldCount,
                        replaceNewIndex,
                        replaceNewCount,
                        ref removed);
                }
                else if (e.Action == NotifyCollectionChangedAction.Move && moveCount > 0 && moveInsertIndex >= 0)
                {
                    AdjustChildrenForMove(moveOldIndex, moveInsertIndex, moveCount);
                }
                else if (shiftDelta != 0 && _children.Count > 0)
                {
                    for (var i = shiftStartIndex; i < _children.Count; ++i)
                    {
                        var child = _children[i];

                        if (shiftDelta < 1 && i >= shiftStartIndex && i < shiftStartIndex - shiftDelta)
                        {
                            child?.AncestorRemoved(ref removed);
                        }
                        else
                        {
                            child?.AncestorIndexChanged(Path, shiftStartIndex, shiftDelta);
                            indexesChanged = true;
                        }
                    }

                    if (shiftDelta > 0)
                        _children.InsertMany(shiftStartIndex, null, shiftDelta);
                    else
                        _children.RemoveRange(shiftStartIndex, -shiftDelta);
                }
            }

            if (shiftDelta != 0 || removed?.Count > 0 || replaceOldIndex >= 0)
            {
                if (shiftEndIndex == -1)
                    shiftEndIndex = ItemsView?.Count ?? 0;
                _owner.OnNodeCollectionChanged(
                    Path,
                    shiftStartIndex,
                    shiftEndIndex,
                    shiftDelta,
                    indexesChanged,
                    removed,
                    moveOldIndex,
                    moveInsertIndex,
                    moveCount,
                    replaceOldIndex,
                    replaceOldCount,
                    replaceNewCount);
            }
        }

        protected override void OnSourceCollectionChangeFinished()
        {
            _owner.OnNodeCollectionChangeFinished();
        }

        private protected override void OnSelectionMoved(
            int oldStartIndex,
            int newStartIndex,
            IReadOnlyList<IndexRange> oldSelectedRanges,
            IReadOnlyList<IndexRange> newSelectedRanges,
            IReadOnlyList<T?> movedItems)
        {
            _ = oldStartIndex;
            _ = newStartIndex;

            _owner.OnNodeSelectionMoved(Path, oldSelectedRanges, newSelectedRanges, movedItems);
        }

        protected override void OnSourceReset()
        {
            var removed = CommitDeselect(new IndexRange(0, int.MaxValue));

            if (_children is not null)
            {
                foreach (var child in _children)
                    child?.AncestorReset(ref removed);
                _children = null;
            }

            _owner.OnNodeCollectionReset(Path, removed);
        }

        private void AncestorIndexChanged(IndexPath parentIndex, int shiftIndex, int shiftDelta)
        {
            var path = Path;

            if (ShiftIndex(parentIndex, shiftIndex, shiftDelta, ref path))
                Path = path;

            if (_children is not null)
            {
                foreach (var child in _children)
                {
                    child?.AncestorIndexChanged(parentIndex, shiftIndex, shiftDelta);
                }
            }
        }

        private void AncestorRemoved(ref List<T?>? removed)
        {
            if (Ranges.Count > 0)
            {
                removed ??= new();

                foreach (var range in Ranges)
                {
                    for (var i = range.Begin; i <= range.End; i++)
                        removed.Add(ItemsView![i]);
                }
            }

            if (_children is not null)
            {
                foreach (var child in _children)
                    child?.AncestorRemoved(ref removed);
            }

            Source = null;
        }

        private void AncestorReset(ref int removedCount)
        {
            if (Ranges.Count > 0)
            {
                removedCount += CommitDeselect(0, int.MaxValue);
            }

            if (_children is not null)
            {
                foreach (var child in _children)
                    child?.AncestorReset(ref removedCount);
            }

            Source = null;
        }

        private void AdjustChildrenForMove(int oldIndex, int newIndex, int count)
        {
            if (_children is null || count <= 0)
                return;

            if (oldIndex < 0 || oldIndex >= _children.Count)
                return;

            var available = _children.Count - oldIndex;
            count = Math.Min(count, available);

            if (count <= 0)
                return;

            var moving = _children.GetRange(oldIndex, count);
            _children.RemoveRange(oldIndex, count);

            var insertIndex = Math.Min(newIndex, _children.Count);
            _children.InsertRange(insertIndex, moving);

            var start = Math.Min(oldIndex, insertIndex);
            var end = Math.Min(_children.Count, Math.Max(oldIndex, insertIndex) + count);

            for (var i = start; i < end; ++i)
            {
                if (_children[i] is TreeSelectionNode<T> child)
                {
                    child.RebasePath(Path.Append(i));
                }
            }
        }

        private void AdjustChildrenForReplace(
            int oldIndex,
            int oldCount,
            int newIndex,
            int newCount,
            ref List<T?>? removed)
        {
            if (_children is null || oldIndex < 0)
                return;

            var removeCount = Math.Min(oldCount, Math.Max(0, _children.Count - oldIndex));
            for (var i = 0; i < removeCount; ++i)
                _children[oldIndex + i]?.AncestorRemoved(ref removed);
            _children.RemoveRange(oldIndex, removeCount);

            var insertIndex = Math.Min(Math.Max(newIndex, 0), _children.Count);
            _children.InsertMany(insertIndex, null, newCount);
            for (var i = Math.Min(oldIndex, insertIndex); i < _children.Count; ++i)
                _children[i]?.RebasePath(Path.Append(i));
        }

        private void RebasePath(IndexPath path)
        {
            Path = path;
            if (_children is null)
                return;

            for (var i = 0; i < _children.Count; ++i)
                _children[i]?.RebasePath(path.Append(i));
        }

        private static void Resize(List<TreeSelectionNode<T>?> list, int count)
        {
            var current = list.Count;

            if (count < current)
            {
                list.RemoveRange(count, current - count);
            }
            else if (count > current)
            {
                if (count > list.Capacity)
                {
                    list.Capacity = count;
                }

                list.InsertMany(0, null, count - current);
            }
        }

        internal static bool ShiftIndex(IndexPath parentIndex, int shiftIndex, int shiftDelta, ref IndexPath path)
        {
            if (path[parentIndex.Count] >= shiftIndex)
            {
                var indexes = path.ToArray();
                indexes[parentIndex.Count] += shiftDelta;
                path = new IndexPath(indexes);
                return true;
            }

            return false;
        }
    }
}
