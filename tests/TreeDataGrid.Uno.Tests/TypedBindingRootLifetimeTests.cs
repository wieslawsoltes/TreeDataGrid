using System;
using System.Collections.Generic;
using System.ComponentModel;
using Uno.Data;
using Uno.Experimental.Data.Core;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class TypedBindingRootLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Obsolete_root_values_cannot_retarget_a_new_activation(bool sendNull)
    {
        var roots = new Roots();
        var oldModel = new Model("Old");
        var currentModel = new Model("Current");
        using var expression = Create(roots);
        using var first = expression.Subscribe(new Probe());
        var obsolete = roots.Current;
        obsolete.Next(oldModel);
        first.Dispose();
        var probe = new Probe();
        using var second = expression.Subscribe(probe);
        roots.Current.Next(currentModel);
        obsolete.Next(sendNull ? null : oldModel);
        Assert.Equal(1, probe.NextCount);
        Assert.Equal("Current", probe.Last.Value);
        Assert.False(probe.Last.HasError);
        Assert.Equal(1, oldModel.Reads);
        Assert.Equal(0, oldModel.Subscribers);
        Assert.Equal(1, currentModel.Subscribers);
        Assert.Equal(1, roots.ActiveCount);
    }

    [Fact]
    public void Obsolete_error_cannot_terminate_a_new_activation()
    {
        var roots = new Roots();
        using var expression = Create(roots);
        using var first = expression.Subscribe(new Probe());
        var obsolete = roots.Current;
        first.Dispose();
        var currentModel = new Model("Current");
        var probe = new Probe();
        using var second = expression.Subscribe(probe);
        roots.Current.Next(currentModel);
        obsolete.Fail(new InvalidOperationException("Obsolete root failure"));
        Assert.Null(probe.LastError);
        Assert.True(expression.HasObservers);
        Assert.Equal(1, roots.ActiveCount);
        currentModel.Text = "Updated";
        Assert.Equal("Updated", probe.Last.Value);
        Assert.Equal(2, probe.NextCount);
    }

    [Fact]
    public void Obsolete_error_while_inactive_does_not_poison_resubscription()
    {
        var roots = new Roots();
        using var expression = Create(roots);
        using var first = expression.Subscribe(new Probe());
        var obsolete = roots.Current;
        first.Dispose();
        obsolete.Fail(new InvalidOperationException("Inactive root failure"));
        var probe = new Probe();
        using var second = expression.Subscribe(probe);
        Assert.Equal(2, roots.Sessions.Count);
        roots.Current.Next(new Model("Recovered"));
        Assert.Null(probe.LastError);
        Assert.Equal("Recovered", probe.Last.Value);
        Assert.True(expression.HasObservers);
        Assert.Equal(1, roots.ActiveCount);
    }

    [Fact]
    public void Token_cleanup_can_reactivate_without_accepting_old_callbacks()
    {
        var roots = new Roots();
        var oldModel = new Model("Old");
        var currentModel = new Model("Current");
        using var expression = Create(roots);
        using var first = expression.Subscribe(new Probe());
        var obsolete = roots.Current;
        obsolete.Next(oldModel);
        var probe = new Probe();
        IDisposable? replacement = null;
        obsolete.OnDispose = () =>
        {
            replacement = expression.Subscribe(probe);
            roots.Current.Next(currentModel);
            obsolete.Next(oldModel);
            obsolete.Fail(new InvalidOperationException("Retired callback"));
        };
        try
        {
            first.Dispose();
            Assert.NotNull(replacement);
            Assert.True(expression.HasObservers);
            Assert.Null(probe.LastError);
            Assert.Equal(1, probe.NextCount);
            Assert.Equal("Current", probe.Last.Value);
            Assert.Equal(0, oldModel.Subscribers);
            Assert.Equal(1, currentModel.Subscribers);
            Assert.Equal(1, roots.ActiveCount);
        }
        finally { replacement?.Dispose(); }
        Assert.Equal(0, currentModel.Subscribers);
        Assert.Equal(0, roots.ActiveCount);
        Assert.Equal(0, roots.DisposeCalls);
    }

    [Fact]
    public void Rejected_write_does_not_refresh_a_replacement_activation()
    {
        var roots = new Roots();
        var oldModel = new Model("Old");
        var currentModel = new Model("Current");
        var probe = new Probe();
        Model? writtenModel = null;
        IDisposable? first = null;
        IDisposable? replacement = null;
        TypedBindingExpression<Model, string>? expression = null;
        using var ownedExpression = expression = Create(roots, (model, _) =>
        {
            writtenModel = model;
            first!.Dispose();
            replacement = expression!.Subscribe(probe);
            roots.Current.Next(currentModel);
            throw new InvalidOperationException("Rejected old write");
        });
        try
        {
            first = ownedExpression.Subscribe(new Probe());
            roots.Current.Next(oldModel);
            var failure = Record.Exception(() => ownedExpression.OnNext(new BindingValue<string>("Rejected")));
            Assert.Null(failure);
            Assert.Same(oldModel, writtenModel);
            Assert.NotNull(replacement);
            Assert.Equal("Old", oldModel.Text);
            Assert.Equal("Current", currentModel.Text);
            Assert.Equal(1, probe.NextCount);
            Assert.Equal("Current", probe.Last.Value);
            Assert.Equal(0, oldModel.Subscribers);
            Assert.Equal(1, currentModel.Subscribers);
            currentModel.Text = "Still active";
            Assert.Equal("Still active", probe.Last.Value);
            Assert.Equal(2, probe.NextCount);
        }
        finally { first?.Dispose(); replacement?.Dispose(); }
    }

    [Fact]
    public void Root_completion_keeps_the_final_model_observed()
    {
        var roots = new Roots();
        var model = new Model("Before");
        var probe = new Probe();
        using var expression = Create(roots);
        using var subscription = expression.Subscribe(probe);
        roots.Current.Next(model);
        roots.Current.Complete();
        model.Text = "After";
        Assert.Equal("After", probe.Last.Value);
        Assert.Equal(2, probe.NextCount);
        Assert.Equal(0, probe.Completions);
        Assert.True(expression.HasObservers);
        Assert.Equal(1, model.Subscribers);
    }

    [Fact]
    public void Error_observer_and_cleanup_failures_preserve_identity_and_order()
    {
        var roots = new Roots();
        var model = new Model("Current");
        var sourceFailure = new InvalidOperationException("Source failure");
        var observerFailure = new InvalidOperationException("Observer failure");
        var cleanupFailure = new InvalidOperationException("Cleanup failure");
        var probe = new Probe { ErrorAction = _ => throw observerFailure };
        using var expression = Create(roots);
        using var subscription = expression.Subscribe(probe);
        roots.Current.Next(model);
        var session = roots.Current;
        session.OnDispose = () => throw cleanupFailure;
        var aggregate = Assert.Throws<AggregateException>(() => session.Fail(sourceFailure));
        Assert.Collection(aggregate.InnerExceptions,
            error => Assert.Same(observerFailure, error),
            error => Assert.Same(cleanupFailure, error));
        Assert.Same(sourceFailure, probe.LastError);
        Assert.False(expression.HasObservers);
        Assert.Equal(0, roots.ActiveCount);
        Assert.Equal(0, model.Subscribers);
        Assert.Equal(0, roots.DisposeCalls);
    }

    [Fact]
    public void Warm_current_and_obsolete_callbacks_allocate_no_managed_storage()
    {
        var roots = new Roots();
        using var expression = Create(roots);
        using var first = expression.Subscribe(new Probe());
        var obsolete = roots.Current;
        first.Dispose();
        var probe = new Probe();
        using var second = expression.Subscribe(probe);
        var current = roots.Current;
        var currentModel = new Model("Current");
        var ignoredModel = new Model("Ignored");
        current.Next(currentModel);
        for (var iteration = 0; iteration < 1024; ++iteration)
        {
            obsolete.Next(ignoredModel);
            current.Next(currentModel);
        }
        probe.NextCount = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 4096; ++iteration)
        {
            obsolete.Next(ignoredModel);
            current.Next(currentModel);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(4096, probe.NextCount);
        Assert.Equal(0, ignoredModel.Reads);
        Assert.Equal(0, ignoredModel.Subscribers);
        Assert.Equal(1, currentModel.Subscribers);
        Assert.Equal("Current", probe.Last.Value);
    }

    private static TypedBindingExpression<Model, string> Create(Roots roots, Action<Model, string>? writer = null) =>
        new(roots, static model => model.Read(), writer, [static model => model], "Missing");

    private sealed class Probe : IObserver<BindingValue<string>>
    {
        internal int NextCount;
        internal int Completions;
        internal BindingValue<string> Last;
        internal Exception? LastError;
        internal Action<Exception>? ErrorAction;
        public void OnNext(BindingValue<string> value) { ++NextCount; Last = value; }
        public void OnError(Exception error) { LastError = error; ErrorAction?.Invoke(error); }
        public void OnCompleted() => ++Completions;
    }
    private sealed class Model(string text) : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs Changed = new(nameof(Text));
        private string _text = text;
        private PropertyChangedEventHandler? _changed;
        internal int Reads { get; private set; }
        internal int Subscribers { get; private set; }
        public string Text { get => _text; set { _text = value; _changed?.Invoke(this, Changed); } }
        internal string Read() { ++Reads; return _text; }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
    private sealed class Roots : IObservable<Model?>, IDisposable
    {
        internal List<RootSession> Sessions { get; } = new();
        internal RootSession Current => Sessions[^1];
        internal int DisposeCalls { get; private set; }
        internal int ActiveCount
        {
            get
            {
                var count = 0;
                foreach (var session in Sessions) if (session.Active) ++count;
                return count;
            }
        }
        public IDisposable Subscribe(IObserver<Model?> observer)
        {
            var session = new RootSession(observer);
            Sessions.Add(session);
            return session;
        }
        public void Dispose() => ++DisposeCalls;
    }
    private sealed class RootSession(IObserver<Model?> observer) : IDisposable
    {
        internal bool Active { get; private set; } = true;
        internal Action? OnDispose { get; set; }
        // Explicit replay models a callback captured before token disposal.
        internal void Next(Model? model) => observer.OnNext(model);
        internal void Fail(Exception error) => observer.OnError(error);
        internal void Complete() => observer.OnCompleted();
        public void Dispose()
        {
            if (!Active) return;
            Active = false;
            var callback = OnDispose;
            OnDispose = null;
            callback?.Invoke();
        }
    }
}
