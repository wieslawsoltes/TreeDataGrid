using System;
using System.Collections.Generic;
using System.ComponentModel;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Data;
using Uno.Experimental.Data;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class TypedCellConstructorTests
{
    [Fact]
    public void Typed_text_binding_observes_writes_and_releases_only_its_subscription()
    {
        var model = new Model { Name = "Initial" };
        using var expression = TypedBinding<Model>.TwoWay(x => x.Name, (x, value) => x.Name = value).Instance(model);
        using var cell = new TextCell<string?>(expression, isReadOnly: false);
        Assert.Equal("Initial", cell.Value);
        model.Name = "External";
        Assert.Equal("External", cell.Text);
        cell.Text = "Edited";
        Assert.Equal("Edited", model.Name);
        cell.Dispose();
        Assert.Equal(0, model.Subscribers);
        // The expression is caller-owned and can be observed again after cell disposal.
        using var another = new TextCell<string?>(expression, isReadOnly: false);
        another.Text = "Reopened";
        Assert.Equal("Reopened", model.Name);
    }

    [Fact]
    public void Nullable_checkbox_preserves_three_states_and_subject_ownership()
    {
        var source = new ValueStream<bool?>(false);
        using var cell = new CheckBoxCell(source, isReadOnly: false, isThreeState: true);
        Assert.True(cell.IsThreeState);
        Assert.False(cell.Value);
        cell.Value = true;
        Assert.True(source.Current.Value);
        cell.Value = null;
        Assert.True(source.Current.HasValue);
        Assert.Null(source.Current.Value);
        source.Publish(false);
        Assert.False(cell.Value);
        Assert.Equal(2, source.Writes);
        cell.Dispose();
        Assert.Equal(0, source.Subscribers);
        Assert.False(source.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unset_and_do_nothing_do_not_replace_the_last_scalar(bool checkBox)
    {
        if (checkBox)
        {
            var source = new ValueStream<bool?>(true);
            using var cell = new CheckBoxCell(source, true, true);
            source.Publish(BindingValue<bool?>.Unset);
            source.Publish(BindingValue<bool?>.DoNothing);
            Assert.True(cell.Value);
        }
        else
        {
            var source = new ValueStream<string?>("Accepted");
            using var cell = new TextCell<string?>(source, true);
            source.Publish(BindingValue<string?>.Unset);
            source.Publish(BindingValue<string?>.DoNothing);
            Assert.Equal("Accepted", cell.Value);
        }
    }

    [Fact]
    public void Typed_diagnostics_and_fallback_are_preserved_and_recover()
    {
        var source = new ValueStream<string?>("Initial");
        using var cell = new TextCell<string?>(source, true);
        var error = new InvalidOperationException("Binding failure");
        source.Publish(BindingValue<string?>.BindingError(error));
        Assert.Equal("Initial", cell.Value);
        Assert.Same(error, cell.Error);
        source.Publish(BindingValue<string?>.DataValidationError(error, "Fallback"));
        Assert.Equal("Fallback", cell.Value);
        Assert.Same(error, cell.Error);
        source.Publish("Recovered");
        Assert.Null(cell.Error);
        Assert.Equal("Recovered", cell.Value);
    }

    [Fact]
    public void Nested_text_publication_cannot_restore_an_old_fallback_error()
    {
        var source = new ValueStream<string?>("Initial");
        using var cell = new TextCell<string?>(source, true);
        cell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "Value" && cell.Value == "Fallback") source.Publish("Newer");
        };
        source.Publish(BindingValue<string?>.BindingError(new Exception("Obsolete"), "Fallback"));
        Assert.Equal("Newer", cell.Value);
        Assert.Null(cell.Error);
    }

    [Fact]
    public void Nested_checkbox_publication_cannot_restore_an_old_fallback_error()
    {
        var source = new ValueStream<bool?>(false);
        using var cell = new CheckBoxCell(source, true, true);
        cell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "Value" && cell.Value == true) source.Publish((bool?)null);
        };
        source.Publish(BindingValue<bool?>.BindingError(new Exception("Obsolete"), true));
        Assert.Null(cell.Value);
        Assert.Null(cell.Error);
    }

    [Fact]
    public void Explicit_separate_writer_and_readonly_policy_are_respected()
    {
        var source = new ValueStream<string?>("Start");
        var observable = new ReadOnlyStream<string?>(source);
        Assert.Throws<ArgumentException>(() => new TextCell<string?>(observable, false));
        using var writable = new TextCell<string?>(observable, source, false);
        writable.Text = "Written";
        Assert.Equal("Written", source.Current.Value);
        using var readOnly = new TextCell<string?>(observable, true);
        readOnly.Value = "Local only";
        Assert.Equal(1, source.Writes);
        Assert.Equal("Written", source.Current.Value);
    }

    private sealed class ReadOnlyStream<T>(ValueStream<T> source) : IObservable<BindingValue<T>>
    {
        public IDisposable Subscribe(IObserver<BindingValue<T>> observer) => source.Subscribe(observer);
    }
    private sealed class ValueStream<T>(T initial) : IObservable<BindingValue<T>>, IObserver<BindingValue<T>>, IDisposable
    {
        private readonly List<IObserver<BindingValue<T>>> _observers = new();
        public BindingValue<T> Current { get; private set; } = initial;
        public int Subscribers => _observers.Count;
        public int Writes { get; private set; }
        public bool Disposed { get; private set; }
        public IDisposable Subscribe(IObserver<BindingValue<T>> observer)
        {
            _observers.Add(observer);
            observer.OnNext(Current);
            return new Subscription(_observers, observer);
        }
        public void Publish(BindingValue<T> value)
        {
            Current = value;
            foreach (var observer in _observers.ToArray()) observer.OnNext(value);
        }
        public void OnNext(BindingValue<T> value) { ++Writes; Publish(value); }
        public void OnError(Exception error) { foreach (var observer in _observers.ToArray()) observer.OnError(error); }
        public void OnCompleted() { }
        public void Dispose() => Disposed = true;
        private sealed class Subscription(List<IObserver<BindingValue<T>>> observers, IObserver<BindingValue<T>> observer) : IDisposable
        {
            public void Dispose() => observers.Remove(observer);
        }
    }
    private sealed class Model : INotifyPropertyChanged
    {
        private string? _name;
        private PropertyChangedEventHandler? _changed;
        public string? Name { get => _name; set { _name = value; _changed?.Invoke(this, new(nameof(Name))); } }
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
