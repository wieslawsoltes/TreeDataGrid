using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class NestedCellBindingAllocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Jit_nested_owner_retarget_has_no_per_call_interpreter_allocation(bool indexer)
    {
        // This project validates the net10 JIT host. AOT/interpreted behavior is
        // exercised separately by the published trimmed/browser consumers.
        Assert.True(RuntimeFeature.IsDynamicCodeCompiled);
        var first = new Node { Child = new() { Name = "first" } };
        var second = new Node { Child = new() { Name = "second" } };
        first.Children.Add(first.Child);
        second.Children.Add(second.Child);
        Expression<Func<Node, string>> expression = indexer ? node => node.Children[0].Name : node => node.Child!.Name;
        using var binding = new CellBinding<Node, string>(new ValueColumn<Node, string>("Name", expression), static () => { });
        for (var iteration = 0; iteration < 1024; ++iteration)
            binding.Retarget((iteration & 1) == 0 ? first : second);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 4096; ++iteration)
            binding.Retarget((iteration & 1) == 0 ? first : second);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal("second", binding.Value);
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(0, first.Child.Subscribers);
        Assert.Equal(1, second.Subscribers);
        Assert.Equal(1, second.Child.Subscribers);
        binding.Suspend();
        Assert.Equal(0, second.Subscribers);
        Assert.Equal(0, second.Child.Subscribers);
    }

    private sealed class Node : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public string Name { get; init; } = "";
        public Node? Child { get; init; }
        public ObservableCollection<Node> Children { get; } = new();
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
