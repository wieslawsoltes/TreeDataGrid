using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.InteropServices;

namespace TreeDataGridCore.Models
{
    internal static class CollectionExtensions
    {
        public static readonly NotifyCollectionChangedEventArgs ResetEvent =
            new(NotifyCollectionChangedAction.Reset);

        public static bool HasKnownIndexes(this NotifyCollectionChangedEventArgs e) => e.Action switch
        {
            NotifyCollectionChangedAction.Add => e.NewStartingIndex >= 0,
            NotifyCollectionChangedAction.Remove => e.OldStartingIndex >= 0,
            NotifyCollectionChangedAction.Replace => e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0,
            NotifyCollectionChangedAction.Move => e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0,
            _ => true,
        };

        public static int BinarySearch<TRow, TModel>(
            this IReadOnlyList<TRow> items,
            TModel model,
            Comparison<TModel> comparison,
            int from = 0,
            int to = -1)
                where TRow : IRow<TModel>
        {
            to = to == -1 ? items.Count - 1 : to;

            var lo = from;
            var hi = to;

            while (lo <= hi)
            {
                // PERF: `lo` or `hi` will never be negative inside the loop,
                //       so computing median using uints is safe since we know
                //       `length <= int.MaxValue`, and indices are >= 0
                //       and thus cannot overflow an uint.
                //       Saves one subtraction per loop compared to
                //       `int i = lo + ((hi - lo) >> 1);`
                var i = (int)(((uint)hi + (uint)lo) >> 1);
                var c = comparison(model, items[i].Model);
                if (c == 0)
                    return i;
                else if (c > 0)
                    lo = i + 1;
                else
                    hi = i - 1;
            }

            // If none found, then a negative number that is the bitwise complement
            // of the index of the next element that is larger than or, if there is
            // no larger element, the bitwise complement of `length`, which
            // is `lo` at this point.
            return ~lo;
        }

        public static void InsertMany<T>(this List<T> list, int index, T item, int count)
        {
            ArgumentNullException.ThrowIfNull(list);
            var previousCount = list.Count;
            if ((uint)index > (uint)previousCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (count < 0 || count > int.MaxValue - previousCount)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0) return;

            // All validation and potentially failing growth precede the content
            // mutation. There are no application callbacks after this point.
            // This replaces the process-wide mutable FastRepeat<T> singleton,
            // which could cross-contaminate independent lists and retain Item
            // when InsertRange threw. No operation state escapes this call.
            var nextCount = previousCount + count;
            list.EnsureCapacity(nextCount);
            CollectionsMarshal.SetCount(list, nextCount);
            var storage = CollectionsMarshal.AsSpan(list);
            // CopyTo has memmove semantics for overlapping slices and preserves
            // GC references. Fill initializes every newly exposed element.
            storage.Slice(index, previousCount - index).CopyTo(storage.Slice(index + count));
            storage.Slice(index, count).Fill(item);
        }

        public static T[] Slice<T>(this List<T> list, int index, int count)
        {
            var result = new T[count];
            list.CopyTo(index, result, 0, count);
            return result;
        }
    }
}
