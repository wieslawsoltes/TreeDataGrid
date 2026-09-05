using System;
using System.Collections;
using System.Collections.Generic;
using Core = global::TreeDataGridCore;

namespace Avalonia.Controls.Adapters
{
    internal static class CoreConversions
    {
        public static GridLength ToAvalonia(this Core.GridLength value) => new(value.Value, (GridUnitType)value.GridUnitType);
        public static Core.GridLength ToCore(this GridLength value) => new(value.Value, (Core.GridUnitType)value.GridUnitType);
        public static IndexPath ToAvalonia(this Core.IndexPath value) => IndexPath.FromCore(value);
        public static Core.IndexPath ToCore(this IndexPath value) => value.ToCoreIndexPath();
        public static IReadOnlyList<IndexPath> ToAvalonia(this IReadOnlyList<Core.IndexPath> value) =>
            new MappedList<Core.IndexPath, IndexPath>(value, ToAvalonia);
    }
    internal sealed class MappedList<TSource, TResult> : IReadOnlyList<TResult>
    {
        private readonly IReadOnlyList<TSource> _source;
        private readonly Func<TSource, TResult> _map;
        public MappedList(IReadOnlyList<TSource> source, Func<TSource, TResult> map) { _source = source; _map = map; }
        public int Count => _source.Count;
        public TResult this[int index] => _map(_source[index]);
        public IEnumerator<TResult> GetEnumerator() { for (var i = 0; i < Count; ++i) yield return this[i]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
