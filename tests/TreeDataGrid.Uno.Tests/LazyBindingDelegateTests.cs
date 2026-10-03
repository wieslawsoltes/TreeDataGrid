using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class LazyBindingDelegateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Collection_handler_is_created_only_for_collection_owners(int kind)
    {
        using var binding = Create();
        // Property observation retains its original eager, readonly handler;
        // this narrowed candidate changes only collection-handler allocation.
        var property = Handler(binding, "_propertyChanged");
        Assert.NotNull(property);
        Assert.Null(Handler(binding, "_collectionChanged"));
        var model = Model(kind);
        binding.Retarget(model);
        Assert.Same(property, Handler(binding, "_propertyChanged"));
        Assert.Equal(kind is 2 or 3, Handler(binding, "_collectionChanged") is not null);
        Assert.Equal(model.Name, binding.Value);
        binding.Dispose();
        Assert.Equal(0, model.PropertySubscribers + model.CollectionSubscribers);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Retarget_and_suspend_reuse_the_exact_initial_handlers(int kind)
    {
        using var binding = Create();
        var first = Model(kind);
        var second = Model(kind);
        binding.Retarget(first);
        var property = first.LastPropertyAdded;
        var collection = first.LastCollectionAdded;
        binding.Retarget(second);
        Assert.Same(property, first.LastPropertyRemoved);
        Assert.Same(collection, first.LastCollectionRemoved);
        Assert.Same(property, second.LastPropertyAdded);
        Assert.Same(collection, second.LastCollectionAdded);
        Assert.Equal(0, first.PropertySubscribers + first.CollectionSubscribers);
        binding.Suspend();
        binding.Retarget(first);
        Assert.Same(property, first.LastPropertyAdded);
        Assert.Same(collection, first.LastCollectionAdded);
        binding.Dispose();
        Assert.Equal(0, first.PropertySubscribers + first.CollectionSubscribers + second.PropertySubscribers + second.CollectionSubscribers);
    }

    [Fact]
    public void Changing_notification_capabilities_across_retargets_is_supported()
    {
        using var binding = Create();
        var plain = Model(0);
        var property = Model(1);
        var collection = Model(2);
        var both = Model(3);
        binding.Retarget(plain);
        binding.Retarget(property);
        property.Name = "Property";
        property.NotifyProperty();
        Assert.Equal("Property", binding.Value);
        binding.Retarget(collection);
        collection.Name = "Collection";
        collection.NotifyCollection();
        Assert.Equal("Collection", binding.Value);
        binding.Retarget(both);
        Assert.Same(property.LastPropertyAdded, both.LastPropertyAdded);
        Assert.Same(collection.LastCollectionAdded, both.LastCollectionAdded);
        binding.Retarget(plain);
        Assert.Equal(0, property.PropertySubscribers + collection.CollectionSubscribers + both.PropertySubscribers + both.CollectionSubscribers);
    }

    [Fact]
    public void Handlers_are_binding_owned_not_globally_shared()
    {
        using var first = Create();
        using var second = Create();
        var a = Model(3);
        var b = Model(3);
        first.Retarget(a);
        second.Retarget(b);
        Assert.NotSame(a.LastPropertyAdded, b.LastPropertyAdded);
        Assert.NotSame(a.LastCollectionAdded, b.LastCollectionAdded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_first_attachment_rolls_back_exact_handler_and_can_recover(bool collection)
    {
        var error = new InvalidOperationException("first add");
        var model = Model(collection ? 2 : 1);
        model.AddError = error;
        using var binding = Create();
        Assert.Same(error, Record.Exception(() => binding.Retarget(model)));
        Assert.Equal(0, model.PropertySubscribers + model.CollectionSubscribers);
        Assert.Same(model.LastPropertyAdded, model.LastPropertyRemoved);
        Assert.Same(model.LastCollectionAdded, model.LastCollectionRemoved);
        var handler = collection ? model.LastCollectionAdded : (Delegate?)model.LastPropertyAdded;
        model.AddError = null;
        binding.Retarget(model);
        Assert.Same(handler, collection ? model.LastCollectionAdded : (Delegate?)model.LastPropertyAdded);
        binding.Dispose();
        Assert.Equal(0, model.PropertySubscribers + model.CollectionSubscribers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retirement_during_first_attachment_preserves_complete_cleanup(bool collection)
    {
        var model = Model(collection ? 2 : 3);
        using var binding = Create();
        model.OnAdd = binding.Dispose;
        binding.Retarget(model);
        Assert.Equal(0, model.PropertySubscribers + model.CollectionSubscribers);
        Assert.Null(binding.Value);
        Assert.Same(model.LastPropertyAdded, model.LastPropertyRemoved);
        Assert.Same(model.LastCollectionAdded, model.LastCollectionRemoved);
    }

    [Fact]
    public void Warm_suspend_and_reattach_allocate_no_handler_storage()
    {
        var model = Model(3);
        using var binding = Create();
        for (var i = 0; i < 1024; ++i) { binding.Retarget(model); binding.Suspend(); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) { binding.Retarget(model); binding.Suspend(); }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(0, model.PropertySubscribers + model.CollectionSubscribers);
    }

    private static CellBinding<Probe, string> Create() => new(
        ValueColumn<Probe, string>.FromDelegate("Name", static model => model.Name), static () => { });
    // Reflection is confined to the test harness, outside any measured region.
    private static object? Handler(CellBinding<Probe, string> binding, string name) =>
        typeof(CellBinding<Probe, string>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(binding);
    private static Probe Model(int kind) => kind switch { 1 => new PropertyProbe(), 2 => new CollectionProbe(), 3 => new BothProbe(), _ => new Probe() };

    private class Probe
    {
        private static readonly PropertyChangedEventArgs PropertyArgs = new(nameof(Name));
        private static readonly NotifyCollectionChangedEventArgs CollectionArgs = new(NotifyCollectionChangedAction.Reset);
        protected PropertyChangedEventHandler? Properties;
        protected NotifyCollectionChangedEventHandler? Collections;
        internal string Name = "Value";
        internal Action? OnAdd;
        internal Exception? AddError;
        internal int PropertySubscribers, CollectionSubscribers;
        internal PropertyChangedEventHandler? LastPropertyAdded, LastPropertyRemoved;
        internal NotifyCollectionChangedEventHandler? LastCollectionAdded, LastCollectionRemoved;
        protected void AddProperty(PropertyChangedEventHandler? handler)
        {
            LastPropertyAdded = handler;
            Properties += handler;
            ++PropertySubscribers;
            var action = OnAdd; OnAdd = null; action?.Invoke();
            if (AddError is { } error) throw error;
        }
        protected void RemoveProperty(PropertyChangedEventHandler? handler)
        { LastPropertyRemoved = handler; Properties -= handler; --PropertySubscribers; }
        protected void AddCollection(NotifyCollectionChangedEventHandler? handler)
        {
            LastCollectionAdded = handler;
            Collections += handler;
            ++CollectionSubscribers;
            var action = OnAdd; OnAdd = null; action?.Invoke();
            if (AddError is { } error) throw error;
        }
        protected void RemoveCollection(NotifyCollectionChangedEventHandler? handler)
        { LastCollectionRemoved = handler; Collections -= handler; --CollectionSubscribers; }
        internal void NotifyProperty() => Properties?.Invoke(this, PropertyArgs);
        internal void NotifyCollection() => Collections?.Invoke(this, CollectionArgs);
    }
    private class PropertyProbe : Probe, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged { add => AddProperty(value); remove => RemoveProperty(value); }
    }
    private sealed class CollectionProbe : Probe, INotifyCollectionChanged
    {
        public event NotifyCollectionChangedEventHandler? CollectionChanged { add => AddCollection(value); remove => RemoveCollection(value); }
    }
    private sealed class BothProbe : PropertyProbe, INotifyCollectionChanged
    {
        public event NotifyCollectionChangedEventHandler? CollectionChanged { add => AddCollection(value); remove => RemoveCollection(value); }
    }
}
