using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Primitives
{
    public class RealizedStackElementsCollectionChangeTests
    {
        [AvaloniaFact]
        public void Stable_Range_Requires_Contiguous_Measured_Elements()
        {
            var (target, _) = CreateRange(10, 3);

            Assert.True(target.TryGetStableRange(out var start, out var end));
            Assert.Equal(100, start);
            Assert.Equal(133, end);

            target.ItemsInserted(11, 1, UpdateIndex, _ => { });

            Assert.False(target.TryGetStableRange(out _, out _));
        }

        [AvaloniaFact]
        public void Stable_Range_Is_Invalidated_When_Indexes_Change_Before_It()
        {
            var (target, _) = CreateRange(10, 3);

            target.ItemsInserted(0, 1, UpdateIndex, _ => { });

            Assert.False(target.TryGetStableRange(out _, out _));
        }

        [AvaloniaFact]
        public void Insert_Inside_Range_Preserves_And_Reindexes_Suffix()
        {
            var (target, elements) = CreateRange(10, 5);
            var recycled = new List<IndexedControl>();

            target.ItemsInserted(12, 2, UpdateIndex, x => recycled.Add((IndexedControl)x));

            Assert.Equal(10, target.FirstIndex);
            Assert.Equal(16, target.LastIndex);
            Assert.Equal(
                new Control?[] { elements[0], elements[1], null, null, elements[2], elements[3], elements[4] },
                target.Elements);
            Assert.Equal(new[] { 10, 11, 14, 15, 16 }, elements.Select(x => x.Index));
            Assert.Empty(recycled);
        }

        [AvaloniaFact]
        public void Large_Insert_Recycles_Suffix_Instead_Of_Creating_A_Sparse_Range()
        {
            var (target, elements) = CreateRange(10, 5);
            var recycled = new List<IndexedControl>();

            target.ItemsInserted(12, 3, UpdateIndex, x => recycled.Add((IndexedControl)x));

            Assert.Equal(new Control?[] { elements[0], elements[1] }, target.Elements);
            Assert.Equal(elements.Skip(2), recycled);
            Assert.Equal(10, target.FirstIndex);
            Assert.Equal(11, target.LastIndex);
        }

        [AvaloniaFact]
        public void Remove_Inside_Range_Recycles_Only_Removed_Controls()
        {
            var (target, elements) = CreateRange(10, 5);
            var recycled = new List<IndexedControl>();

            target.ItemsRemoved(12, 1, UpdateIndex, x => recycled.Add((IndexedControl)x));

            Assert.Equal(new Control?[] { elements[0], elements[1], elements[3], elements[4] }, target.Elements);
            Assert.Equal(new[] { elements[2] }, recycled);
            Assert.Equal(new[] { 10, 11, 12, 13 }, target.Elements.Cast<IndexedControl>().Select(x => x.Index));
            Assert.Equal(new[] { 10d, 11d, 13d, 14d }, target.SizeU);
        }

        [AvaloniaFact]
        public void Remove_Overlapping_Start_Recycles_Overlap_And_Preserves_Remainder()
        {
            var (target, elements) = CreateRange(10, 5);
            var recycled = new List<IndexedControl>();

            target.ItemsRemoved(8, 4, UpdateIndex, x => recycled.Add((IndexedControl)x));

            Assert.Equal(new Control?[] { elements[2], elements[3], elements[4] }, target.Elements);
            Assert.Equal(elements.Take(2), recycled);
            Assert.Equal(8, target.FirstIndex);
            Assert.Equal(new[] { 8, 9, 10 }, target.Elements.Cast<IndexedControl>().Select(x => x.Index));
        }

        [AvaloniaFact]
        public void Remove_End_Boundaries_Do_Not_Recycle_Unchanged_Controls()
        {
            var (target, elements) = CreateRange(10, 5);
            var recycled = new List<IndexedControl>();

            target.ItemsRemoved(8, 2, UpdateIndex, x => recycled.Add((IndexedControl)x));

            Assert.Equal(8, target.FirstIndex);
            Assert.Equal(elements, target.Elements);
            Assert.Equal(Enumerable.Range(8, 5), elements.Select(x => x.Index));
            Assert.Empty(recycled);

            target.ItemsRemoved(11, 20, UpdateIndex, x => recycled.Add((IndexedControl)x));

            Assert.Equal(elements.Take(3), target.Elements);
            Assert.Equal(elements.Skip(3), recycled);
        }

        [AvaloniaFact]
        public void Insert_Then_Remove_Before_Layout_Restores_Original_Range()
        {
            var (target, elements) = CreateRange(0, 10);
            var recycled = new List<IndexedControl>();

            target.ItemsInserted(2, 1, UpdateIndex, x => recycled.Add((IndexedControl)x));
            target.ItemsRemoved(2, 1, UpdateIndex, x => recycled.Add((IndexedControl)x));

            Assert.Equal(elements, target.Elements);
            Assert.Equal(Enumerable.Range(0, 10), elements.Select(x => x.Index));
            Assert.Empty(recycled);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Move_Inside_Range_Preserves_Controls_And_Their_Sizes()
        {
            var source = CreateSource(20);
            var (target, elements) = CreateRange(source, 5, 10);
            var oldIndexes = elements.ToDictionary(x => x, x => x.Index);
            var recycled = new List<IndexedControl>();

            MoveRange(source, 7, 11, 2);
            target.ItemsMoved(7, 11, 2, UpdateIndex, x => recycled.Add((IndexedControl)x));

            AssertMovePreservation(target, source, elements, oldIndexes, recycled, 5, 14, 7, 11, 2);
            Assert.Equal(100, target.GetElementU(5));
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Move_Crossing_Range_Boundaries_Preserves_Controls_That_Remain_Visible()
        {
            var source = CreateSource(40);
            var (target, elements) = CreateRange(source, 10, 10);
            var oldIndexes = elements.ToDictionary(x => x, x => x.Index);
            var recycled = new List<IndexedControl>();

            MoveRange(source, 2, 15, 3);
            target.ItemsMoved(2, 15, 3, UpdateIndex, x => recycled.Add((IndexedControl)x));

            AssertMovePreservation(target, source, elements, oldIndexes, recycled, 10, 19, 2, 15, 3);
            Assert.True(double.IsNaN(target.GetElementU(10)));
        }

        [AvaloniaFact]
        public void Collection_Change_Matrix_Preserves_Item_Mapping()
        {
            var firstIndexes = new[] { 0, 5, 20 };
            var realizedCounts = new[] { 1, 5, 10 };
            var changeCounts = new[] { 1, 2, 5, 12 };

            foreach (var firstIndex in firstIndexes)
            {
                foreach (var realizedCount in realizedCounts)
                {
                    for (var index = 0; index <= 40; ++index)
                    {
                        foreach (var count in changeCounts)
                        {
                            var insertSource = CreateSource(40);
                            var (insertTarget, _) = CreateRange(insertSource, firstIndex, realizedCount);
                            var insertedItems = Enumerable.Range(0, count).Select(_ => new object()).ToList();
                            var insertRecycled = new List<IndexedControl>();

                            insertSource.InsertRange(index, insertedItems);
                            insertTarget.ItemsInserted(
                                index,
                                count,
                                UpdateIndex,
                                x => insertRecycled.Add((IndexedControl)x));
                            AssertValidMapping(insertTarget, insertSource, insertRecycled);

                            if (index < 40)
                            {
                                var removeCount = Math.Min(count, 40 - index);
                                var removeSource = CreateSource(40);
                                var (removeTarget, _) = CreateRange(removeSource, firstIndex, realizedCount);
                                var removeRecycled = new List<IndexedControl>();

                                removeSource.RemoveRange(index, removeCount);
                                removeTarget.ItemsRemoved(
                                    index,
                                    removeCount,
                                    UpdateIndex,
                                    x => removeRecycled.Add((IndexedControl)x));
                                AssertValidMapping(removeTarget, removeSource, removeRecycled);
                            }
                        }
                    }
                }
            }
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Move_Matrix_Preserves_Every_Control_That_Remains_In_The_Realized_Window()
        {
            const int sourceCount = 40;
            var firstIndexes = new[] { 0, 5, 20 };
            var realizedCounts = new[] { 1, 5, 10 };
            var moveCounts = new[] { 1, 2, 5 };

            foreach (var firstIndex in firstIndexes)
            {
                foreach (var realizedCount in realizedCounts)
                {
                    foreach (var moveCount in moveCounts)
                    {
                        for (var oldIndex = 0; oldIndex <= sourceCount - moveCount; ++oldIndex)
                        {
                            for (var newIndex = 0; newIndex <= sourceCount - moveCount; ++newIndex)
                            {
                                var source = CreateSource(sourceCount);
                                var (target, elements) = CreateRange(source, firstIndex, realizedCount);
                                var oldIndexes = elements.ToDictionary(x => x, x => x.Index);
                                var recycled = new List<IndexedControl>();

                                MoveRange(source, oldIndex, newIndex, moveCount);
                                target.ItemsMoved(
                                    oldIndex,
                                    newIndex,
                                    moveCount,
                                    UpdateIndex,
                                    x => recycled.Add((IndexedControl)x));

                                AssertMovePreservation(
                                    target,
                                    source,
                                    elements,
                                    oldIndexes,
                                    recycled,
                                    firstIndex,
                                    firstIndex + realizedCount - 1,
                                    oldIndex,
                                    newIndex,
                                    moveCount);
                            }
                        }
                    }
                }
            }
        }

        private static (RealizedStackElements Target, List<IndexedControl> Elements) CreateRange(
            int firstIndex,
            int count)
        {
            var source = CreateSource(firstIndex + count);
            return CreateRange(source, firstIndex, count);
        }

        private static (RealizedStackElements Target, List<IndexedControl> Elements) CreateRange(
            IReadOnlyList<object> source,
            int firstIndex,
            int count)
        {
            var target = new RealizedStackElements(traceEnabled: false);
            var elements = new List<IndexedControl>();
            var u = 100d;

            for (var i = 0; i < count; ++i)
            {
                var index = firstIndex + i;
                var element = new IndexedControl(index, source[index]);
                elements.Add(element);
                target.Add(index, element, u, index);
                u += index;
            }

            return (target, elements);
        }

        private static List<object> CreateSource(int count) =>
            Enumerable.Range(0, count).Select(_ => new object()).ToList();

        private static void AssertValidMapping(
            RealizedStackElements target,
            IReadOnlyList<object> source,
            IReadOnlyCollection<IndexedControl> recycled)
        {
            Assert.Equal(target.Elements.Count, target.SizeU.Count);
            var active = new HashSet<IndexedControl>();

            for (var i = 0; i < target.Elements.Count; ++i)
            {
                if (target.Elements[i] is not IndexedControl element)
                    continue;

                Assert.True(active.Add(element));
                Assert.Equal(target.FirstIndex + i, element.Index);
                Assert.InRange(element.Index, 0, source.Count - 1);
                Assert.Same(source[element.Index], element.Item);
                Assert.DoesNotContain(element, recycled);
            }
        }

        private static void AssertMovePreservation(
            RealizedStackElements target,
            IReadOnlyList<object> source,
            IReadOnlyList<IndexedControl> elements,
            IReadOnlyDictionary<IndexedControl, int> oldIndexes,
            IReadOnlyCollection<IndexedControl> recycled,
            int firstIndex,
            int lastIndex,
            int oldIndex,
            int newIndex,
            int count)
        {
            Assert.Equal(firstIndex, target.FirstIndex);
            Assert.Equal(lastIndex, target.LastIndex);
            Assert.Equal(lastIndex - firstIndex + 1, target.Elements.Count);
            Assert.Equal(target.Elements.Count, target.SizeU.Count);

            foreach (var element in elements)
            {
                var previousIndex = oldIndexes[element];
                var updatedIndex = MapMovedIndex(previousIndex, oldIndex, newIndex, count);

                if (updatedIndex >= firstIndex && updatedIndex <= lastIndex)
                {
                    var realizedIndex = updatedIndex - firstIndex;
                    Assert.Same(element, target.Elements[realizedIndex]);
                    Assert.Equal(previousIndex, target.SizeU[realizedIndex]);
                    Assert.Equal(updatedIndex, element.Index);
                    Assert.DoesNotContain(element, recycled);
                }
                else
                {
                    Assert.Contains(element, recycled);
                }
            }

            Assert.Equal(recycled.Count, recycled.Distinct().Count());
            AssertValidMapping(target, source, recycled);
        }

        private static int MapMovedIndex(int index, int oldIndex, int newIndex, int count)
        {
            if (index >= oldIndex && index < oldIndex + count)
                return newIndex + index - oldIndex;
            if (oldIndex < newIndex && index >= oldIndex + count && index < newIndex + count)
                return index - count;
            if (oldIndex > newIndex && index >= newIndex && index < oldIndex)
                return index + count;
            return index;
        }

        private static void MoveRange(List<object> source, int oldIndex, int newIndex, int count)
        {
            var moved = source.GetRange(oldIndex, count);
            source.RemoveRange(oldIndex, count);
            source.InsertRange(newIndex, moved);
        }

        private static void UpdateIndex(Control control, int oldIndex, int newIndex)
        {
            var element = (IndexedControl)control;
            Assert.Equal(oldIndex, element.Index);
            element.Index = newIndex;
        }

        private sealed class IndexedControl : Border
        {
            public IndexedControl(int index, object item)
            {
                Index = index;
                Item = item;
            }

            public int Index { get; set; }
            public object Item { get; }
        }
    }
}
