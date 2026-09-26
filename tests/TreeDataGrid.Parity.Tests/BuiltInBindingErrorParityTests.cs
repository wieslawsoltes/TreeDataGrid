using System;
using System.ComponentModel;
using TreeDataGridCore.Models;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = global::Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Parity.Tests;

public sealed class BuiltInBindingErrorParityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Merely_requesting_descriptor_does_not_change_text_getter_failure_behavior(bool requestDescriptor)
    {
        var a = new A.TextColumn<Item, string?>("Name", x => x.Name);
        var u = new U.TextColumn<Item, string?>("Name", x => x.Name);
        if (requestDescriptor) { _ = a.Binding; _ = u.Binding; }
        var am = new Item(); var um = new Item();
        var ac = a.CreateCell(new Row(am));
        using var uc = u.CreateCell(new Row(um));
        using var lifetime = (IDisposable)ac;
        am.Failure = um.Failure = new InvalidOperationException("Expected getter failure");
        am.Notify(); um.Notify();
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal("Current", uc.Value);
        Assert.Same(um.Failure, uc.Error);
        am.Failure = um.Failure = null;
        am.Text = um.Text = "Recovered";
        am.Notify(); um.Notify();
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal("Recovered", uc.Value);
        Assert.Null(uc.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Merely_requesting_descriptor_does_not_change_nullable_checkbox_failure_behavior(bool requestDescriptor)
    {
        var a = new A.CheckBoxColumn<Item>("Flag", x => x.NullableFlag);
        var u = new U.CheckBoxColumn<Item>("Flag", x => x.NullableFlag);
        if (requestDescriptor) { _ = a.Binding; _ = u.Binding; }
        var am = new Item(); var um = new Item();
        var ac = a.CreateCell(new Row(am));
        using var uc = u.CreateCell(new Row(um));
        using var lifetime = (IDisposable)ac;
        am.Failure = um.Failure = new InvalidOperationException("Expected getter failure");
        am.Notify(); um.Notify();
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal(true, uc.Value);
        Assert.Same(um.Failure, uc.Error);
        am.Failure = um.Failure = null;
        am.Flag = um.Flag = null;
        am.Notify(); um.Notify();
        Assert.Equal(ac.Value, uc.Value);
        Assert.Null(uc.Value);
        Assert.Null(uc.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Merely_requesting_descriptor_does_not_change_boolean_checkbox_failure_behavior(bool requestDescriptor)
    {
        var a = new A.CheckBoxColumn<Item>("Flag", x => x.BooleanFlag);
        var u = new U.CheckBoxColumn<Item>("Flag", x => x.BooleanFlag);
        if (requestDescriptor) { _ = a.Binding; _ = u.Binding; }
        var am = new Item(); var um = new Item();
        var ac = a.CreateCell(new Row(am));
        using var uc = u.CreateCell(new Row(um));
        using var lifetime = (IDisposable)ac;
        am.Failure = um.Failure = new InvalidOperationException("Expected getter failure");
        am.Notify(); um.Notify();
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal(true, uc.Value);
        Assert.False(u.IsThreeState);
        am.Failure = um.Failure = null;
        am.Flag = um.Flag = false;
        am.Notify(); um.Notify();
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal(false, uc.Value);
        Assert.Null(uc.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retarget_failure_retains_previous_value_but_initial_failure_has_no_value(bool requestDescriptor)
    {
        var a = new A.TextColumn<Item, string?>("Name", x => x.Name);
        var u = new U.TextColumn<Item, string?>("Name", x => x.Name);
        if (requestDescriptor) { _ = a.Binding; _ = u.Binding; }
        var am = new Item(); var um = new Item();
        var ac = a.CreateCell(new Row(am));
        using var uc = u.CreateCell(new Row(um));
        using var lifetime = (IDisposable)ac;
        var anew = new Item { Failure = new InvalidOperationException("Expected") };
        var unew = new Item { Failure = new InvalidOperationException("Expected") };
        Assert.True(a.TryReuseCell(ac, new Row(anew)));
        Assert.True(u.TryReuseCell(uc, new Row(unew)));
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal("Current", uc.Value);
        Assert.Equal(0, am.Subscribers);
        Assert.Equal(0, um.Subscribers);
        var freshA = a.CreateCell(new Row(anew));
        using var freshU = u.CreateCell(new Row(unew));
        using var freshLifetime = (IDisposable)freshA;
        Assert.Equal(freshA.Value, freshU.Value);
        Assert.Null(freshU.Value);
        unew.Failure = anew.Failure = null;
        unew.Text = anew.Text = "Retarget recovered";
        anew.Notify(); unew.Notify();
        Assert.Equal(ac.Value, uc.Value);
        Assert.Equal(freshA.Value, freshU.Value);
        Assert.Equal("Retarget recovered", uc.Value);
    }

    private sealed class Row(Item model) : IRow<Item>
    {
        public Item Model => model;
        public object? Header => null;
        public TreeDataGridCore.GridLength Height { get; set; }
        public void UpdateModelIndex(int delta) { }
    }
    private sealed class Item : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public string Text { get; set; } = "Current";
        public bool? Flag { get; set; } = true;
        public Exception? Failure { get; set; }
        public string? Name => Failure is { } error ? throw error : Text;
        public bool? NullableFlag => Failure is { } error ? throw error : Flag;
        public bool BooleanFlag => Failure is { } error ? throw error : Flag ?? false;
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
        public void Notify() => _changed?.Invoke(this, new(null));
    }
}
