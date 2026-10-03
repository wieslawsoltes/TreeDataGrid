// Adapted from Avalonia 12.0.0 (MIT). Copyright (c) AvaloniaUI OÜ. All Rights Reserved.
// See THIRD-PARTY-NOTICES.md, build/uno-parity-inputs/upstream and docs/uno-contract-materialization.json.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TreeDataGridCore.Selection
{
    internal class FlatSelectedIndexes<T> : FlatReadOnlySelectionListBase<int>
    {
        private readonly SelectionModel<T>? _owner;
        private readonly IReadOnlyList<IndexRange>? _ranges;

        public FlatSelectedIndexes(SelectionModel<T> owner) => _owner = owner;
        public FlatSelectedIndexes(IReadOnlyList<IndexRange> ranges) => _ranges = ranges;

        public override int this[int index]
        {
            get
            {
                if (index >= Count)
                {
                    throw new IndexOutOfRangeException("The index was out of range.");
                }

                if (_owner?.SingleSelect == true)
                {
                    return _owner.SelectedIndex;
                }
                else
                {
                    return IndexRange.GetAt(Ranges!, index);
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
                    return IndexRange.GetCount(Ranges!);
                }
            }
        }

        private IReadOnlyList<IndexRange> Ranges => _ranges ?? _owner!.Ranges!;

        public override IEnumerator<int> GetEnumerator()
        {
            IEnumerator<int> SingleSelect()
            {
                if (_owner.SelectedIndex >= 0)
                {
                    yield return _owner.SelectedIndex;
                }
            }

            if (_owner?.SingleSelect == true)
            {
                return SingleSelect();
            }
            else
            {
                return IndexRange.EnumerateIndices(Ranges).GetEnumerator();
            }
        }

        public static FlatSelectedIndexes<T>? Create(IReadOnlyList<IndexRange>? ranges)
        {
            return ranges is object ? new FlatSelectedIndexes<T>(ranges) : null;
        }
    }
}
