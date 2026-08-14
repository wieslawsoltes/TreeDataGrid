using System;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Primitives
{
    public class TreeDataGridElementFactoryTests
    {
        [AvaloniaFact]
        public void Reuses_Element_With_Same_Parent_In_Preference_To_Fallback()
        {
            var target = new TestElementFactory();
            var firstParent = new StackPanel();
            var requestedParent = new StackPanel();
            var fallback = target.GetOrCreateElement(new object(), firstParent);
            var expected = target.GetOrCreateElement(new object(), requestedParent);

            firstParent.Children.Add(fallback);
            requestedParent.Children.Add(expected);
            target.RecycleElement(fallback);
            target.RecycleElement(expected);

            var actual = target.GetOrCreateElement(new object(), requestedParent);

            Assert.Same(expected, actual);
            Assert.Same(requestedParent, actual.Parent);
            Assert.Equal(2, target.CreatedCount);
        }

        [AvaloniaFact]
        public void Reparents_Fallback_Element_From_A_Panel()
        {
            var target = new TestElementFactory();
            var oldParent = new StackPanel();
            var newParent = new StackPanel();
            var element = target.GetOrCreateElement(new object(), oldParent);

            oldParent.Children.Add(element);
            target.RecycleElement(element);

            var actual = target.GetOrCreateElement(new object(), newParent);

            Assert.Same(element, actual);
            Assert.Null(actual.Parent);
            Assert.Empty(oldParent.Children);
            Assert.Equal(1, target.CreatedCount);
        }

        [AvaloniaFact]
        public void Does_Not_Retain_An_Element_After_It_Leaves_The_Pool()
        {
            var (factory, weakElement) = CreateUnreferencedCheckedOutElement();

            for (var i = 0; i < 3 && weakElement.IsAlive; ++i)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Assert.False(weakElement.IsAlive);
            GC.KeepAlive(factory);
        }

        [Fact]
        public void CanReuseElement_Uses_The_Factorys_Recycle_Keys()
        {
            var target = new KeyedElementFactory();
            var element = new Border { Tag = "group" };

            Assert.True(target.CanReuseElement(element, "group"));
            Assert.False(target.CanReuseElement(element, "leaf"));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (TestElementFactory Factory, WeakReference Element) CreateUnreferencedCheckedOutElement()
        {
            var factory = new TestElementFactory();
            var parent = new StackPanel();
            var element = factory.GetOrCreateElement(new object(), parent);

            factory.RecycleElement(element);
            element = factory.GetOrCreateElement(new object(), parent);

            return (factory, new WeakReference(element));
        }

        private sealed class TestElementFactory : TreeDataGridElementFactory
        {
            public int CreatedCount { get; private set; }

            protected override Control CreateElement(object? data)
            {
                ++CreatedCount;
                return new Border();
            }

            protected override string GetDataRecycleKey(object? data) => "element";

            protected override string GetElementRecycleKey(Control element) => "element";
        }

        private sealed class KeyedElementFactory : TreeDataGridElementFactory
        {
            protected override string GetDataRecycleKey(object? data) => (string)data!;

            protected override string GetElementRecycleKey(Control element) => (string)element.Tag!;
        }
    }
}
