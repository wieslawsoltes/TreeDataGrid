// Adapted from Avalonia 12.0.0 (MIT). Copyright (c) AvaloniaUI OÜ. All Rights Reserved.
// See THIRD-PARTY-NOTICES.md, build/uno-parity-inputs/upstream and docs/uno-contract-materialization.json.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace TreeDataGridCore.Selection
{
    internal class FlatSelectedItems<T> : FlatReadOnlySelectionListBase<T>
    {
        private readonly SelectionModel<T>? _owner;
        private readonly TreeDataGridItemsSourceView<T>? _items;
        private readonly IReadOnlyList<IndexRange>? _ranges;

        public FlatSelectedItems(SelectionModel<T> owner) => _owner = owner;
        
        public FlatSelectedItems(IReadOnlyList<IndexRange> ranges, TreeDataGridItemsSourceView<T>? items)
        {
            _ranges = ranges ?? throw new ArgumentNullException(nameof(ranges));
            _items = items;
        }

        public override T? this[int index]
        {
            get
            {
                if (index >= Count)
                {
                    throw new IndexOutOfRangeException("The index was out of range.");
                }

                if (_owner?.SingleSelect == true)
                {
                    return _owner.SelectedItem;
                }
                else if (Items is not null && Ranges is not null)
                {
                    return Items[IndexRange.GetAt(Ranges, index)];
                }
                else
                {
                    return default;
                }
            }
        }

        public override int Count
        {
            get
            {
                if (_owner?.SingleSelect == true)
                {
                    return _owner.SelectedIndex == -1 ? 0 : 1;
                }
                else
                {
                    return Ranges is object ? IndexRange.GetCount(Ranges) : 0;
                }
            }
        }

        private TreeDataGridItemsSourceView<T>? Items => _items ?? _owner?.ItemsView;
        private IReadOnlyList<IndexRange>? Ranges => _ranges ?? _owner!.Ranges;

        public override IEnumerator<T?> GetEnumerator()
        {
            if (_owner?.SingleSelect == true)
            {
                if (_owner.SelectedIndex >= 0)
                {
                    yield return _owner.SelectedItem;
                }
            }
            else
            {
                var items = Items;

                foreach (var range in Ranges!)
                {
                    for (var i = range.Begin; i <= range.End; ++i)
                    {
                        yield return items is object ? items[i] : default;
                    }
                }
            }
        }

        public static FlatSelectedItems<T>? Create(
            IReadOnlyList<IndexRange>? ranges,
            TreeDataGridItemsSourceView<T>? items)
        {
            return ranges is object ? new FlatSelectedItems<T>(ranges, items) : null;
        }

        public class Untyped : FlatReadOnlySelectionListBase<object?>
        {
            private readonly IReadOnlyList<T?> _source;
            public Untyped(IReadOnlyList<T?> source) => _source = source;
            public override object? this[int index] => _source[index];
            public override int Count => _source.Count;
            public override IEnumerator<object?> GetEnumerator()
            {
                foreach (var i in _source)
                {
                    yield return i;
                }
            }
        }
    }
}
