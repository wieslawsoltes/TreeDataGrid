using System;
using System.ComponentModel;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class CellBindingReentrancyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Getter_Cannot_Republish_A_Retired_Value(bool dispose)
    {
        var row = new Row("old");
        var changes = 0;
        using var binding = new CellBinding<Row, string>(
            ValueColumn<Row, string>.FromDelegate("Name", x => x.Read()), () => ++changes);
        row.OnRead = () => { if (dispose) binding.Dispose(); else binding.Suspend(); };
        binding.Retarget(row);
        Assert.Null(binding.Value);
        Assert.Null(binding.Error);
        Assert.Equal(0, changes);
        Assert.Equal(0, row.Subscribers);
        if (dispose) Assert.Throws<ObjectDisposedException>(() => binding.Retarget(row));
        else
        {
            binding.Retarget(row);
            Assert.Equal("old", binding.Value);
            Assert.Equal(1, row.Subscribers);
        }
    }

    [Fact]
    public void Getter_Retarget_Publishes_Only_The_Current_Row()
    {
        var first = new Row("first");
        var second = new Row("second");
        var changes = new System.Collections.Generic.List<string?>();
        CellBinding<Row, string>? binding = null;
        using (binding = new(ValueColumn<Row, string>.FromDelegate("Name", x => x.Read()),
                   () => changes.Add(binding!.Value)))
        {
            first.OnRead = () => binding.Retarget(second);
            binding.Retarget(first);
            Assert.Equal(new[] { "second" }, changes);
            Assert.Equal("second", binding.Value);
            Assert.Equal(0, first.Subscribers);
            Assert.Equal(1, second.Subscribers);
        }
        Assert.Equal(0, second.Subscribers);
    }

    [Fact]
    public void Retired_Getter_Exception_Cannot_Republish_Error()
    {
        var row = new Row("old");
        var changes = 0;
        using var binding = new CellBinding<Row, string>(
            ValueColumn<Row, string>.FromDelegate("Name", x => x.Read()), () => ++changes);
        row.OnRead = () => { binding.Suspend(); throw new InvalidOperationException("retired getter"); };
        binding.Retarget(row);
        Assert.Null(binding.Value);
        Assert.Null(binding.Error);
        Assert.Equal(0, changes);
        Assert.Equal(0, row.Subscribers);
    }

    [Fact]
    public void Dispose_Inside_Event_Attachment_Does_Not_Leak_The_Handler()
    {
        var row = new Row("old");
        var reads = 0;
        using var binding = new CellBinding<Row, string>(
            ValueColumn<Row, string>.FromDelegate("Name", x => { ++reads; return x.Read(); }), static () => { });
        row.OnSubscribe = binding.Dispose;
        binding.Retarget(row);
        Assert.Equal(0, row.Subscribers);
        Assert.Equal(0, reads);
        Assert.Null(binding.Value);
    }

    [Fact]
    public void Retarget_Inside_Event_Detachment_Survives_Suspend_Cleanup()
    {
        var first = new Row("first");
        var second = new Row("second");
        using var binding = new CellBinding<Row, string>(
            ValueColumn<Row, string>.FromDelegate("Name", x => x.Read()), static () => { });
        binding.Retarget(first);
        first.OnUnsubscribe = () => binding.Retarget(second);
        binding.Suspend();
        Assert.Equal("second", binding.Value);
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(1, second.Subscribers);
        binding.Dispose();
        Assert.Equal(0, second.Subscribers);
    }

    [Fact]
    public void Nested_Owner_Getter_Retirement_Does_Not_Attach_The_Leaf()
    {
        var leaf = new Row("leaf");
        var root = new Row("root") { Child = leaf };
        var changes = 0;
        using var binding = new CellBinding<Row, string>(
            new ValueColumn<Row, string>("Name", x => x.GetChild().Name), () => ++changes);
        root.OnRead = binding.Suspend;
        binding.Retarget(root);
        Assert.Equal(0, root.Subscribers);
        Assert.Equal(0, leaf.Subscribers);
        Assert.Null(binding.Value);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Notification_Retarget_Converges_Without_Recursive_Refresh()
    {
        var first = new Row("first");
        var second = new Row("second");
        var depth = 0;
        var maximumDepth = 0;
        var changes = 0;
        CellBinding<Row, string>? binding = null;
        using (binding = new(ValueColumn<Row, string>.FromDelegate("Name", x => x.Read()), () =>
               {
                   maximumDepth = Math.Max(maximumDepth, ++depth);
                   if (++changes == 1) binding!.Retarget(second);
                   --depth;
               }))
        {
            binding.Retarget(first);
            Assert.Equal(2, changes);
            Assert.Equal(1, maximumDepth);
            Assert.Equal("second", binding.Value);
            Assert.Equal(0, first.Subscribers);
            Assert.Equal(1, second.Subscribers);
        }
    }

    [Fact]
    public void Throwing_Notification_Still_Completes_Requested_Disposal()
    {
        var row = new Row("row");
        var error = new InvalidOperationException("application callback");
        CellBinding<Row, string>? binding = null;
        using (binding = new(ValueColumn<Row, string>.FromDelegate("Name", x => x.Read()), () =>
               {
                   binding!.Dispose();
                   throw error;
               }))
        {
            Assert.Same(error, Assert.Throws<InvalidOperationException>(() => binding.Retarget(row)));
            Assert.Equal(0, row.Subscribers);
            Assert.Null(binding.Value);
            Assert.Null(binding.Error);
        }
    }

    [Fact]
    public void Same_Row_Retarget_Still_Retires_The_InFlight_Read()
    {
        var row = new Row("row");
        var reads = 0;
        var changes = new System.Collections.Generic.List<string?>();
        CellBinding<Row, string>? binding = null;
        using (binding = new(ValueColumn<Row, string>.FromDelegate("Name", _ =>
               {
                   if (++reads != 1) return "current";
                   binding!.Retarget(row);
                   return "retired";
               }), () => changes.Add(binding!.Value)))
        {
            binding.Retarget(row);
            Assert.Equal(new[] { "current" }, changes);
            Assert.Equal(2, reads);
            Assert.Equal(1, row.Subscribers);
        }
        Assert.Equal(0, row.Subscribers);
    }

    [Fact]
    public void Value_Equality_Cannot_Republish_After_Retirement()
    {
        var row = new Row("row");
        var selected = new ReentrantValue();
        var changes = 0;
        using var binding = new CellBinding<Row, ReentrantValue>(
            ValueColumn<Row, ReentrantValue>.FromDelegate("Value", _ => selected), () => ++changes);
        binding.Retarget(row);
        selected.OnEquals = binding.Suspend;
        selected = new ReentrantValue();
        binding.Retarget(row);
        Assert.Null(binding.Value);
        Assert.Null(binding.Error);
        Assert.Equal(0, row.Subscribers);
        Assert.Equal(1, changes);
    }

    private sealed class ReentrantValue : IEquatable<ReentrantValue>
    {
        public Action? OnEquals;
        public bool Equals(ReentrantValue? other)
        {
            var callback = OnEquals;
            OnEquals = null;
            callback?.Invoke();
            return ReferenceEquals(this, other);
        }
        public override bool Equals(object? other) => other is ReentrantValue value && Equals(value);
        public override int GetHashCode() => 0;
    }

    private sealed class Row(string name) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public string Name { get; } = name;
        public Row? Child { get; init; }
        public Action? OnRead;
        public Action? OnSubscribe;
        public Action? OnUnsubscribe;
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public string Read() { InvokeOnce(ref OnRead); return Name; }
        public Row GetChild() { InvokeOnce(ref OnRead); return Child!; }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; InvokeOnce(ref OnSubscribe); }
            remove { _changed -= value; InvokeOnce(ref OnUnsubscribe); }
        }
        private static void InvokeOnce(ref Action? callback)
        {
            var action = callback;
            callback = null;
            action?.Invoke();
        }
    }
}
