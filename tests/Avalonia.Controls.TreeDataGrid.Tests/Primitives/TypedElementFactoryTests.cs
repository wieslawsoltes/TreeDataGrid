using System;
using System.Runtime.CompilerServices;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Primitives
{
    public class TypedElementFactoryTests
    {
        [AvaloniaFact]
        public void Typed_Keys_Work_Through_The_Non_Generic_Factory_API()
        {
            TreeDataGridElementFactory factory = new TypedFactory();
            var grid = new TreeDataGrid { ElementFactory = factory };
            var parent = new Panel();
            var first = new Model("first", 1);
            var equal = new Model(new string("first".ToCharArray()), 1);
            var otherDepth = new Model("first", 2);
            var otherColumn = new Model("second", 1);
            var element = factory.GetOrCreateElement(first, parent);

            Assert.Same(factory, grid.ElementFactory);
            Assert.True(factory.CanReuseElement(element, equal));
            Assert.False(factory.CanReuseElement(element, otherDepth));
            Assert.False(factory.CanReuseElement(element, otherColumn));
            factory.RecycleElement(element);
            Assert.NotSame(element, factory.GetOrCreateElement(otherDepth, parent));
            Assert.NotSame(element, factory.GetOrCreateElement(otherColumn, parent));
            Assert.Same(element, factory.GetOrCreateElement(equal, parent));
        }

        [AvaloniaFact]
        public void Typed_Pool_Preserves_Parent_Preference_And_Fallback_Reparenting()
        {
            TreeDataGridElementFactory factory = new TypedFactory();
            var model = new Model("cell", 0);
            var firstParent = new Panel();
            var otherParent = new Panel();
            var first = factory.GetOrCreateElement(model, firstParent);
            var other = factory.GetOrCreateElement(model, otherParent);
            firstParent.Children.Add(first);
            otherParent.Children.Add(other);
            factory.RecycleElement(first);
            factory.RecycleElement(other);

            Assert.Same(other, factory.GetOrCreateElement(model, otherParent));
            Assert.Same(first, factory.GetOrCreateElement(model, otherParent));
            Assert.Null(first.Parent);
            Assert.Empty(firstParent.Children);
        }

        [AvaloniaFact]
        public void Warm_Typed_Keys_And_Pool_Operations_Do_Not_Box_Or_Allocate_After_GC()
        {
            TreeDataGridElementFactory factory = new TypedFactory();
            var model = new Model("cell", 0);
            var parent = new Panel();
            var element = factory.GetOrCreateElement(model, parent);
            _ = Measure(factory, model, parent, element);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.Equal(0, Measure(factory, model, parent, element));
            GC.KeepAlive(factory);
        }

        // Warm the exact measured method; keep tier transitions outside the allocation assertion.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static long Measure(TreeDataGridElementFactory factory, Model model, Control parent, Control element)
        {
            var valid = true;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10_000; ++i)
            {
                // Previously unseen depths must not need a formatted string or a cache entry.
                model.Depth = i;
                valid &= factory.CanReuseElement(element, model) == (i == 0);
                model.Depth = 0;
                factory.RecycleElement(element);
                valid &= ReferenceEquals(element, factory.GetOrCreateElement(model, parent));
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(valid);
            return allocated;
        }

        private sealed class Model
        {
            public Model(string column, int depth) { Column = column; Depth = depth; }
            public string Column { get; }
            public int Depth { get; set; }
        }

        private readonly struct Key : IEquatable<Key>
        {
            public Key(string column, int depth) { Column = column; Depth = depth; }
            public string Column { get; }
            public int Depth { get; }
            public bool Equals(Key other) => Column == other.Column && Depth == other.Depth;
            public override bool Equals(object? obj) => throw new InvalidOperationException("Boxed equality was used.");
            public override int GetHashCode() => HashCode.Combine(Column, Depth);
        }

        private sealed class Element : Control
        {
            public Element(Key key) { Key = key; }
            public Key Key { get; }
        }

        private sealed class TypedFactory : TreeDataGridElementFactory<Key>
        {
            protected override Control CreateElement(object? data) => new Element(GetDataRecycleKeyCore(data));
            protected override Key GetDataRecycleKeyCore(object? data)
            {
                var model = (Model)data!;
                return new Key(model.Column, model.Depth);
            }
            protected override Key GetElementRecycleKeyCore(Control element) => ((Element)element).Key;
            protected override string GetDataRecycleKey(object? data) => throw new InvalidOperationException("String key path was used.");
            protected override string GetElementRecycleKey(Control element) => throw new InvalidOperationException("String key path was used.");
        }
    }
}
