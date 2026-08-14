using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls.Models.TreeDataGrid;

namespace Avalonia.Controls.Primitives
{
    public class TreeDataGridElementFactory
    {
        private readonly Dictionary<object, RecyclePool> _recyclePools = new();

        public Control GetOrCreateElement(object? data, Control parent)
        {
            var recycleKey = GetDataRecycleKey(data);

            if (_recyclePools.TryGetValue(recycleKey, out var pool) &&
                pool.TryTake(parent, out var element))
            {
                if (parent.IsMeasureValid && !IsParentLayouting(parent))
                    parent.InvalidateMeasure();
                return element;
            }

            // Otherwise create a new element.
            return CreateElement(data);
        }

        public void RecycleElement(Control element)
        {
            var recycleKey = GetElementRecycleKey(element);

            if (!_recyclePools.TryGetValue(recycleKey, out var pool))
            {
                pool = new();
                _recyclePools.Add(recycleKey, pool);
            }

            pool.Add(element);
        }

        /// <summary>
        /// Determines whether an existing element can be reused for the supplied data without
        /// returning it to this factory's recycle pool.
        /// </summary>
        /// <remarks>
        /// This is useful for controls which retain a realized child in the visual tree while
        /// changing its model, avoiding a detach and subsequent style reapplication.
        /// </remarks>
        public bool CanReuseElement(Control element, object? data)
        {
            return GetElementRecycleKey(element) == GetDataRecycleKey(data);
        }

        protected virtual Control CreateElement(object? data)
        {
            return data switch
            {
                CheckBoxCell => new TreeDataGridCheckBoxCell(),
                TemplateCell => new TreeDataGridTemplateCell(),
                IExpanderCell => new TreeDataGridExpanderCell(),
                ICell => new TreeDataGridTextCell(),
                IColumn => new TreeDataGridColumnHeader(),
                IRow => new TreeDataGridRow(),
                _ => throw new NotSupportedException(),
            };
        }

        protected virtual string GetDataRecycleKey(object? data)
        {
            return data switch
            {
                CheckBoxCell => typeof(TreeDataGridCheckBoxCell).FullName!,
                TemplateCell => typeof(TreeDataGridTemplateCell).FullName!,
                IExpanderCell => typeof(TreeDataGridExpanderCell).FullName!,
                ICell => typeof(TreeDataGridTextCell).FullName!,
                IColumn => typeof(TreeDataGridColumnHeader).FullName!,
                IRow => typeof(TreeDataGridRow).FullName!,
                _ => throw new NotSupportedException(),
            };
        }

        protected virtual string GetElementRecycleKey(Control element)
        {
            return element.GetType().FullName!;
        }

        private static bool IsParentLayouting(Control parent)
        {
            return parent is TreeDataGridPresenterBase<IRow> rows && rows.IsLayoutInProgress ||
                   parent is TreeDataGridPresenterBase<IColumn> columns && columns.IsLayoutInProgress;
        }

        private sealed class RecyclePool
        {
            // Each pooled element is indexed both by its current parent and, when it can be
            // reparented, by the fallback list. Weak, intrusive entries make both removals O(1)
            // without allocating on each recycle or retaining controls after they are checked out.
            private readonly ConditionalWeakTable<Control, Entry> _entries = new();
            private readonly Dictionary<StyledElement, ParentBucket> _byParent = new();
            private Entry? _fallbackFirst;
            private Entry? _fallbackLast;

            public void Add(Control element)
            {
                var entry = _entries.GetValue(element, static x => new Entry(x));
                var parent = element.Parent;

                Debug.Assert(!entry.IsPooled);
                entry.IsPooled = true;

                if (parent is not null)
                {
                    if (!_byParent.TryGetValue(parent, out var parentBucket))
                    {
                        parentBucket = entry.ParentBucket?.Parent == parent ?
                            entry.ParentBucket : new ParentBucket(parent);
                        _byParent.Add(parent, parentBucket);
                    }

                    entry.ParentBucket = parentBucket;
                    parentBucket.Add(entry);
                }
                else
                    entry.ParentBucket = null;

                if (parent is null or Panel)
                    AddFallback(entry);
            }

            public bool TryTake(Control parent, out Control element)
            {
                Entry? entry = null;

                if (_byParent.TryGetValue(parent, out var parentBucket))
                    entry = parentBucket.First;

                entry ??= _fallbackFirst;

                if (entry is null)
                {
                    element = null!;
                    return false;
                }

                Remove(entry);

                if (entry.Element.Parent is Panel oldParent && oldParent != parent)
                {
                    oldParent.Children.Remove(entry.Element);
                    entry.ParentBucket = null;
                }

                Debug.Assert(entry.Element.Parent is null || entry.Element.Parent == parent);
                element = entry.Element;
                return true;
            }

            private void Remove(Entry entry)
            {
                if (entry.ParentBucket is { } parentBucket)
                {
                    parentBucket.Remove(entry);

                    if (parentBucket.First is null)
                        _byParent.Remove(parentBucket.Parent);
                }

                if (entry.IsFallback)
                    RemoveFallback(entry);

                entry.IsPooled = false;
            }

            private void AddFallback(Entry entry)
            {
                entry.IsFallback = true;
                entry.FallbackPrevious = _fallbackLast;

                if (_fallbackLast is not null)
                    _fallbackLast.FallbackNext = entry;
                else
                    _fallbackFirst = entry;

                _fallbackLast = entry;
            }

            private void RemoveFallback(Entry entry)
            {
                if (entry.FallbackPrevious is not null)
                    entry.FallbackPrevious.FallbackNext = entry.FallbackNext;
                else
                    _fallbackFirst = entry.FallbackNext;

                if (entry.FallbackNext is not null)
                    entry.FallbackNext.FallbackPrevious = entry.FallbackPrevious;
                else
                    _fallbackLast = entry.FallbackPrevious;

                entry.FallbackPrevious = null;
                entry.FallbackNext = null;
                entry.IsFallback = false;
            }

            private sealed class ParentBucket
            {
                public ParentBucket(StyledElement parent)
                {
                    Parent = parent;
                }

                public Entry? First { get; private set; }
                public Entry? Last { get; private set; }
                public StyledElement Parent { get; }

                public void Add(Entry entry)
                {
                    entry.ParentPrevious = Last;

                    if (Last is not null)
                        Last.ParentNext = entry;
                    else
                        First = entry;

                    Last = entry;
                }

                public void Remove(Entry entry)
                {
                    if (entry.ParentPrevious is not null)
                        entry.ParentPrevious.ParentNext = entry.ParentNext;
                    else
                        First = entry.ParentNext;

                    if (entry.ParentNext is not null)
                        entry.ParentNext.ParentPrevious = entry.ParentPrevious;
                    else
                        Last = entry.ParentPrevious;

                    entry.ParentPrevious = null;
                    entry.ParentNext = null;
                }
            }

            private sealed class Entry
            {
                public Entry(Control element)
                {
                    Element = element;
                }

                public Control Element { get; }
                public Entry? FallbackNext { get; set; }
                public Entry? FallbackPrevious { get; set; }
                public bool IsFallback { get; set; }
                public bool IsPooled { get; set; }
                public ParentBucket? ParentBucket { get; set; }
                public Entry? ParentNext { get; set; }
                public Entry? ParentPrevious { get; set; }
            }
        }
    }
}
