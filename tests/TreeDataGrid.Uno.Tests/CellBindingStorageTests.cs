using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class CellBindingStorageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Root_only_binding_construction_stays_within_the_allocation_budget(bool expression)
    {
        const int count = 1024;
        var row = new PlainRow();
        var column = expression
            ? new ValueColumn<PlainRow, string>("Name", x => x.Name)
            : ValueColumn<PlainRow, string>.FromDelegate("Name", static x => x.Name);
        Action changed = static () => { };
        using (var warm = new CellBinding<PlainRow, string>(column, changed)) warm.Retarget(row);
        var bindings = new CellBinding<PlainRow, string>[count];
        try
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < count; ++i)
            {
                var binding = new CellBinding<PlainRow, string>(column, changed);
                bindings[i] = binding;
                binding.Retarget(row);
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            // Includes the binding and its two instance event delegates. A flat
            // binding must not also allocate owner/accessor singleton arrays.
            Assert.InRange(allocated, 1, 256L * count);
            Assert.All(bindings, binding => Assert.Equal("Name", binding.Value));
        }
        finally { foreach (var binding in bindings) binding?.Dispose(); }
    }

    [Fact]
    public void Inline_root_and_aliased_nested_owners_share_exactly_one_subscription()
    {
        var root = new ObservedRow("Root");
        root.Child = root;
        var column = new ValueColumn<ObservedRow, string>("Name", x => x.Child!.Name);
        using var binding = new CellBinding<ObservedRow, string>(column, static () => { });
        binding.Retarget(root);
        Assert.Equal(1, root.Subscribers);
        var child = new ObservedRow("Child");
        root.Child = child;
        root.Notify();
        Assert.Equal("Child", binding.Value);
        Assert.Equal(1, root.Subscribers);
        Assert.Equal(1, child.Subscribers);
        root.Child = root;
        root.Notify();
        Assert.Equal("Root", binding.Value);
        Assert.Equal(1, root.Subscribers);
        Assert.Equal(0, child.Subscribers);
        binding.Suspend();
        Assert.Equal(0, root.Subscribers);
        Assert.Equal(0, child.Subscribers);
    }

    [Fact]
    public void Suspended_root_does_not_leak_through_shared_accessor_storage()
    {
        var (binding, model) = MakeSuspended();
        using (binding)
        {
            for (var i = 0; i < 3 && model.IsAlive; ++i)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            }
            Assert.False(model.IsAlive);
            binding.Retarget(new PlainRow());
            Assert.Equal("Name", binding.Value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (CellBinding<PlainRow, string> Binding, WeakReference Model) MakeSuspended()
    {
        var model = new PlainRow();
        var column = ValueColumn<PlainRow, string>.FromDelegate("Name", static x => x.Name);
        var binding = new CellBinding<PlainRow, string>(column, static () => { });
        binding.Retarget(model);
        binding.Suspend();
        return (binding, new WeakReference(model));
    }

    private sealed class PlainRow { public string Name => "Name"; }
    private sealed class ObservedRow(string name) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        private static readonly PropertyChangedEventArgs Changed = new(null);
        public string Name => name;
        public ObservedRow? Child { get; set; }
        public int Subscribers;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
        public void Notify() => _changed?.Invoke(this, Changed);
    }
}
