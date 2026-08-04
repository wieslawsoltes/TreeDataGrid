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
