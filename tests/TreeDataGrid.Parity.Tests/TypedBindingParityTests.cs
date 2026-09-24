using System;
using System.Collections.Generic;
using System.ComponentModel;
using Xunit;
using AB = Avalonia.Experimental.Data;
using UB = Uno.Experimental.Data;
using AV = Avalonia.Data;
using UV = Uno.Data;

namespace TreeDataGrid.Parity.Tests;

public sealed class TypedBindingParityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Zażółć 🐈")]
    public void Initial_and_changed_values_match_the_actual_reference(string? initial)
    {
        var model = new Model { Text = initial };
        var original = AB.TypedBinding<Model>.TwoWay(x => x.Text).Instance(model);
        using var native = UB.TypedBinding<Model>.TwoWay(x => x.Text).Instance(model);
        var a = new Recorder<AV.BindingValue<string?>>();
        var u = new Recorder<UV.BindingValue<string?>>();
        using var sa = original.Subscribe(a);
        using var su = native.Subscribe(u);
        Assert.Equal(a.Last.Value, u.Last.Value);
        model.Text = "External";
        Assert.Equal(a.Last.Value, u.Last.Value);
        native.OnNext("Native");
        Assert.Equal(a.Last.Value, u.Last.Value);
        original.OnNext("Reference");
        Assert.Equal("Reference", model.Text);
        Assert.Equal(a.Last.Value, u.Last.Value);
        Assert.False(a.Last.HasError);
        Assert.False(u.Last.HasError);
    }

    [Fact]
    public void Null_intermediate_fallback_and_recovery_match_the_reference()
    {
        var model = new Model { Child = new() { Text = "Child" } };
        var aDescriptor = AB.TypedBinding<Model>.OneWay(x => x.Child!.Text);
        var uDescriptor = UB.TypedBinding<Model>.OneWay(x => x.Child!.Text);
        aDescriptor.FallbackValue = "Fallback";
        uDescriptor.FallbackValue = "Fallback";
        var original = aDescriptor.Instance(model);
        using var native = uDescriptor.Instance(model);
        var a = new Recorder<AV.BindingValue<string?>>();
        var u = new Recorder<UV.BindingValue<string?>>();
        using var sa = original.Subscribe(a);
        using var su = native.Subscribe(u);
        Assert.Equal(a.Last.Value, u.Last.Value);
        model.Child = null;
        Assert.Equal(a.Last.Value, u.Last.Value);
        Assert.Equal(a.Last.HasError, u.Last.HasError);
        Assert.Equal(a.Last.Error!.GetType(), u.Last.Error!.GetType());
        model.Child = new() { Text = "Recovered" };
        Assert.Equal(a.Last.Value, u.Last.Value);
        Assert.False(u.Last.HasError);
    }

    [Fact]
    public void Rejected_subject_writes_preserve_the_reference_value()
    {
        var model = new Model { Text = "Accepted" };
        static void Reject(Model _, string? value) => throw new InvalidOperationException("Rejected");
        var original = AB.TypedBinding<Model>.TwoWay(x => x.Text, Reject).Instance(model);
        using var native = UB.TypedBinding<Model>.TwoWay(x => x.Text, Reject).Instance(model);
        var a = new Recorder<AV.BindingValue<string?>>();
        var u = new Recorder<UV.BindingValue<string?>>();
        using var sa = original.Subscribe(a);
        using var su = native.Subscribe(u);
        original.OnNext("No");
        native.OnNext("No");
        Assert.Equal("Accepted", model.Text);
        Assert.Equal(a.Last.Value, u.Last.Value);
    }

    private sealed class Recorder<T> : IObserver<T>
    {
        internal T Last = default!;
        public void OnNext(T value) => Last = value;
        public void OnError(Exception error) => throw error;
        public void OnCompleted() { }
    }
    private sealed class Model : INotifyPropertyChanged
    {
        private string? _text;
        private Model? _child;
        public string? Text { get => _text; set { _text = value; PropertyChanged?.Invoke(this, new(nameof(Text))); } }
        public Model? Child { get => _child; set { _child = value; PropertyChanged?.Invoke(this, new(nameof(Child))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
