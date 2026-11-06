using System;
using System.ComponentModel;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class CellBindingAllocationTests
{
    [Fact]
    public void Warm_Retarget_Does_Not_Allocate_Transaction_Or_Subscription_Objects()
    {
        var first = new Row("first");
        var second = new Row("second");
        using var binding = new CellBinding<Row, string>(
            ValueColumn<Row, string>.FromDelegate("Name", static row => row.Name), static () => { });
        for (var i = 0; i < 1024; ++i) binding.Retarget((i & 1) == 0 ? first : second);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) binding.Retarget((i & 1) == 0 ? first : second);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal("second", binding.Value);
        Assert.Equal(0, first.SubscriberCount);
        Assert.Equal(1, second.SubscriberCount);
        binding.Suspend();
        Assert.Equal(0, second.SubscriberCount);
    }

    private sealed class Row(string name) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public string Name { get; } = name;
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => _changed += value;
            remove => _changed -= value;
        }
    }
}
