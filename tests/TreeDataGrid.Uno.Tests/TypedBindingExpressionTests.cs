using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Uno.Data;
using Uno.Experimental.Data;
using Uno.Experimental.Data.Core;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class TypedBindingExpressionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Zażółć 🐈")]
    public void Initial_and_later_observers_receive_the_current_typed_value(string? value)
    {
        var row = new Model { Text = value };
        using var expression = TypedBinding<Model>.OneWay(x => x.Text).Instance(row);
        var first = new Recorder<string?>();
        var second = new Recorder<string?>();
        using var a = expression.Subscribe(first);
        using var b = expression.Subscribe(second);
        Assert.Equal(value, Assert.Single(first.Values).Value);
        Assert.Equal(value, Assert.Single(second.Values).Value);
        Assert.Equal(1, row.Subscribers);
        row.Text = "Changed";
        Assert.Equal("Changed", first.Last.Value);
        Assert.Equal("Changed", second.Last.Value);
        a.Dispose();
        Assert.Equal(1, row.Subscribers);
        b.Dispose();
        Assert.Equal(0, row.Subscribers);
    }

    [Fact]
    public void Nested_owner_replacement_and_null_recovery_remove_old_handlers()
    {
        var old = new Model { Text = "Old" };
        var current = new Model { Text = "Current" };
        var root = new Model { Child = old };
        var binding = TypedBinding<Model>.OneWay(x => x.Child!.Text);
        binding.FallbackValue = "Fallback";
        using var expression = binding.Instance(root);
        var values = new Recorder<string?>();
        using var subscription = expression.Subscribe(values);
        Assert.Equal("Old", values.Last.Value);
        root.Child = null;
        Assert.True(values.Last.HasError);
        Assert.Equal("Fallback", values.Last.Value);
        Assert.Equal(0, old.Subscribers);
        root.Child = current;
        Assert.Equal("Current", values.Last.Value);
        Assert.False(values.Last.HasError);
        Assert.Equal(1, current.Subscribers);
        old.Text = "Retired";
        Assert.Equal("Current", values.Last.Value);
        expression.Dispose();
        Assert.Equal(0, root.Subscribers);
        Assert.Equal(0, current.Subscribers);
        Assert.Equal(1, values.Completions);
    }

    [Fact]
    public void Generated_links_are_snapshotted_and_can_observe_collection_indexers()
    {
        var row = new Model();
        row.Items.Add("First");
        Func<Model, object>[] links = [static x => x.Items];
        var descriptor = TypedBinding<Model>.OneWay(static x => x.Items[0], links);
        using var expression = descriptor.Instance(row);
        links[0] = static _ => throw new InvalidOperationException("Mutated descriptor array");
        var values = new Recorder<string>();
        using var subscription = expression.Subscribe(values);
        Assert.Equal("First", values.Last.Value);
        row.Items[0] = "Second";
        Assert.Equal("Second", values.Last.Value);
    }

    [Fact]
    public void Alias_owners_are_subscribed_once()
    {
        var row = new Model { Text = "First" };
        var descriptor = TypedBinding<Model>.OneWay(x => x.Text,
            [static x => x, static x => x, static x => x]);
        using var expression = descriptor.Instance(row);
        using var subscription = expression.Subscribe(new Recorder<string?>());
        Assert.Equal(1, row.Subscribers);
        expression.Dispose();
        Assert.Equal(0, row.Subscribers);
    }

    [Fact]
    public void Subject_writes_recover_after_rejected_setters()
    {
        var row = new Model { Text = "Before" };
        var fail = true;
        var descriptor = TypedBinding<Model>.TwoWay(x => x.Text, (model, value) =>
        {
            if (fail) throw new InvalidOperationException("Rejected");
            model.Text = value;
        });
        using var expression = descriptor.Instance(row);
        var values = new Recorder<string?>();
        using var subscription = expression.Subscribe(values);
        expression.OnNext("Rejected");
        Assert.Equal("Before", row.Text);
        Assert.Equal("Before", values.Last.Value);
        fail = false;
        expression.OnNext("Accepted");
        Assert.Equal("Accepted", row.Text);
        Assert.Equal("Accepted", values.Last.Value);
    }

    [Fact]
    public void Inferred_setter_updates_the_property_and_ignores_unset_input()
    {
        var row = new Model { Text = "Before" };
        using var expression = TypedBinding<Model>.TwoWay(x => x.Text).Instance(row);
        using var subscription = expression.Subscribe(new Recorder<string?>());
        expression.OnNext(BindingValue<string?>.Unset);
        expression.OnNext(BindingValue<string?>.DoNothing);
        Assert.Equal("Before", row.Text);
        expression.OnNext("After");
        Assert.Equal("After", row.Text);
    }

    [Fact]
    public void Last_unsubscribe_releases_owners_and_a_later_subscriber_reinitializes()
    {
        var row = new Model { Text = "First" };
        using var expression = TypedBinding<Model>.OneWay(x => x.Text).Instance(row);
        expression.Subscribe(new Recorder<string?>()).Dispose();
        Assert.Equal(0, row.Subscribers);
        row.Text = "Second";
        var values = new Recorder<string?>();
        using var subscription = expression.Subscribe(values);
        Assert.Equal("Second", Assert.Single(values.Values).Value);
        Assert.Equal(1, row.Subscribers);
    }

    [Fact]
    public void Observable_roots_switch_models_and_release_the_root_subscription()
    {
        var first = new Model { Text = "First" };
        var second = new Model { Text = "Second" };
        var roots = new RootSource();
        using var expression = new TypedBindingExpression<Model, string?>(roots, x => x.Text, null, [x => x], "Missing");
        var values = new Recorder<string?>();
        using var subscription = expression.Subscribe(values);
        Assert.Empty(values.Values);
        roots.Send(first);
        Assert.Equal("First", values.Last.Value);
        roots.Send(second);
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(1, second.Subscribers);
        roots.Send(null);
        Assert.True(values.Last.HasError);
        Assert.Equal("Missing", values.Last.Value);
        Assert.Equal(0, second.Subscribers);
        subscription.Dispose();
        Assert.Equal(0, roots.Subscribers);
    }

    [Fact]
    public void Disposal_during_getter_rejects_its_returning_value_and_detaches()
    {
        var row = new Model { Text = "First" };
        TypedBindingExpression<Model, string?>? expression = null;
        var descriptor = TypedBinding<Model>.OneWay<string?>(model =>
        {
            expression!.Dispose();
            return "Stale";
        }, [x => x]);
        using (expression = descriptor.Instance(row))
        {
            var values = new Recorder<string?>();
            using var subscription = expression.Subscribe(values);
            Assert.Empty(values.Values);
            Assert.Equal(1, values.Completions);
            Assert.Equal(0, row.Subscribers);
        }
    }

    [Fact]
    public void OneTime_does_not_observe_model_notifications()
    {
        var row = new Model { Text = "First" };
        using var expression = TypedBinding<Model>.OneTime(x => x.Text).Instance(row, BindingMode.OneTime);
        var values = new Recorder<string?>();
        using var subscription = expression.Subscribe(values);
        Assert.Equal("First", values.Last.Value);
        Assert.Equal(0, row.Subscribers);
        row.Text = "Second";
        Assert.Single(values.Values);
    }

    [Fact]
    public void Invalid_descriptors_and_inferred_setters_fail_before_observation()
    {
        var descriptor = new TypedBinding<Model, string?>();
        Assert.Throws<InvalidOperationException>(() => descriptor.Instance(new()));
        descriptor.Read = x => x.Text;
        Assert.Throws<InvalidOperationException>(() => descriptor.Instance(new()));
        descriptor.Links = [];
        Assert.Throws<InvalidOperationException>(() => descriptor.Instance(new(), BindingMode.TwoWay));
        Assert.Throws<ArgumentOutOfRangeException>(() => descriptor.Instance(new(), (BindingMode)99));
        Assert.Throws<ArgumentException>(() => TypedBinding<Model>.TwoWay(x => x.Text + "!"));
        Assert.Throws<ArgumentException>(() => TypedBinding<Model>.TwoWay(x => x.ReadOnly));
    }

    [Fact]
    public void Optional_null_and_binding_errors_preserve_distinct_value_states()
    {
        Optional<string?> unset = default;
        Optional<string?> suppliedNull = new(null);
        Assert.False(unset.HasValue);
        Assert.True(suppliedNull.HasValue);
        Assert.NotEqual(unset, suppliedNull);
        var error = new InvalidOperationException("Error");
        var failed = BindingValue<string?>.BindingError(error);
        var fallback = BindingValue<string?>.BindingError(error, suppliedNull);
        Assert.False(failed.HasValue);
        Assert.True(failed.HasError);
        Assert.Throws<InvalidOperationException>(() => failed.Value);
        Assert.True(fallback.HasValue);
        Assert.Null(fallback.Value);
        Assert.Same(error, fallback.Error);
        Assert.Equal(BindingValueType.BindingErrorWithFallback, fallback.Type);
        Assert.Equal("Recovered", fallback.WithValue("Recovered").Value);
    }

    [Fact]
    public void Warm_single_observer_notifications_do_not_allocate_binding_results()
    {
        var row = new Model { Text = "Same" };
        using var expression = TypedBinding<Model>.OneWay(static x => x.Text, [static x => x]).Instance(row);
        var observer = new Counter<string?>();
        using var subscription = expression.Subscribe(observer);
        for (var i = 0; i < 1024; ++i) row.Notify();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) row.Notify();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(5121, observer.Count);
    }

    private sealed class Counter<T> : IObserver<BindingValue<T>>
    {
        internal int Count;
        public void OnNext(BindingValue<T> value) => ++Count;
        public void OnError(Exception error) => throw error;
        public void OnCompleted() { }
    }
    private sealed class Recorder<T> : IObserver<BindingValue<T>>
    {
        internal readonly List<BindingValue<T>> Values = new();
        internal BindingValue<T> Last => Values[^1];
        internal int Completions;
        public void OnNext(BindingValue<T> value) => Values.Add(value);
        public void OnError(Exception error) => throw error;
        public void OnCompleted() => ++Completions;
    }
    private sealed class Model : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs Changed = new(null);
        private PropertyChangedEventHandler? _changed;
        private string? _text;
        private Model? _child;
        public string? Text { get => _text; set { _text = value; Notify(); } }
        public string ReadOnly => "Read only";
        public Model? Child { get => _child; set { _child = value; Notify(); } }
        public ObservableCollection<string> Items { get; } = new();
        internal int Subscribers;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
        internal void Notify() => _changed?.Invoke(this, Changed);
    }
    private sealed class RootSource : IObservable<Model?>
    {
        private IObserver<Model?>? _observer;
        internal int Subscribers;
        public IDisposable Subscribe(IObserver<Model?> observer)
        { _observer = observer; ++Subscribers; return new Token(this); }
        internal void Send(Model? value) => _observer?.OnNext(value);
        private sealed class Token(RootSource owner) : IDisposable
        {
            private RootSource? _owner = owner;
            public void Dispose()
            {
                if (_owner is not { } target) return;
                _owner = null;
                target._observer = null;
                --target.Subscribers;
            }
        }
    }
}
