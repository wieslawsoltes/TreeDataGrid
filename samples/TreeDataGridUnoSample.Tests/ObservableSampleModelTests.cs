using System;
using System.Collections.Generic;
using TreeDataGridUnoShared;
using Xunit;

namespace TreeDataGridUnoSample.Tests;

public class ObservableSampleModelTests
{
    [Fact]
    public void Notifications_Bracket_Assignment_And_Keep_The_Declared_Property_Name()
    {
        var row = new Row();
        var observed = new List<string>();
        row.PropertyChanging += (_, e) => observed.Add($"before:{e.PropertyName}:{row.Value}");
        row.PropertyChanged += (_, e) => observed.Add($"after:{e.PropertyName}:{row.Value}");
        row.Value = 12;
        row.Value = 12;
        row.Value = 34;
        Assert.Equal(new[] { "before:Value:0", "after:Value:12", "before:Value:12", "after:Value:34" }, observed);
    }

    [Fact]
    public void Null_And_Equal_String_Assignments_Follow_Value_Equality()
    {
        var row = new Row();
        var changes = 0;
        row.PropertyChanged += (_, _) => ++changes;
        row.Name = null;
        row.Name = "name";
        row.Name = new string("name".ToCharArray());
        row.Name = null;
        Assert.Equal(2, changes);
        Assert.Null(row.Name);
    }

    [Fact]
    public void Warmed_Notifications_Do_Not_Allocate_Per_Assignment()
    {
        var row = new Row();
        var changes = 0;
        row.PropertyChanged += (_, _) => ++changes;
        for (var i = 0; i < 1024; ++i) row.Value = i;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) row.Value = i;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(5119, changes);
    }

    [Fact]
    public void Subscriber_Removal_Stops_Notifications()
    {
        var row = new Row();
        var changes = 0;
        System.ComponentModel.PropertyChangedEventHandler handler = (_, _) => ++changes;
        row.PropertyChanged += handler;
        row.Value = 1;
        row.PropertyChanged -= handler;
        row.Value = 2;
        Assert.Equal(1, changes);
    }

    private sealed class Row : ObservableSampleModel
    {
        private int _value;
        private string? _name;
        public int Value { get => _value; set => RaiseAndSetIfChanged(ref _value, value); }
        public string? Name { get => _name; set => RaiseAndSetIfChanged(ref _name, value); }
    }
}
