using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using Xunit;

namespace TreeDataGrid.Parity.Tests;

public sealed class ObservableContractParityTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Expression_chain_links_match_the_actual_reference(int variant)
    {
        Expression<Func<Node, string>> expression = variant switch
        {
            0 => x => x.Name,
            1 => x => x.Child!.Name,
            2 => x => x.Child!.Child!.Name,
            _ => x => x.Name.ToUpperInvariant(),
        };
        var a = Avalonia.Data.Core.Parsers.ExpressionChainVisitor<Node>.Build(expression);
        var u = Uno.Data.Core.Parsers.ExpressionChainVisitor<Node>.Build(expression);
        var root = new Node("root") { Child = new("child") { Child = new("grandchild") } };
        Assert.Equal(a.Length, u.Length);
        for (var i = 0; i < a.Length; ++i) Assert.Same(a[i](root), u[i](root));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Observable_lifetime_and_terminal_delivery_match(bool error)
    {
        var a = new ReferenceObservable();
        var u = new NativeObservable();
        var left = new Recorder(); var right = new Recorder();
        var a1 = a.Subscribe(left); var u1 = u.Subscribe(right);
        var a2 = a.Subscribe(left); var u2 = u.Subscribe(right);
        a.Next(42); u.Next(42);
        a1.Dispose(); u1.Dispose(); a1.Dispose(); u1.Dispose();
        a.Next(43); u.Next(43);
        Assert.Equal(left.Events, right.Events);
        a2.Dispose(); u2.Dispose();
        Assert.Equal(a.Initializations, u.Initializations);
        Assert.Equal(a.Deinitializations, u.Deinitializations);
        Assert.False(u.HasObservers);
        using var a3 = a.Subscribe(left); using var u3 = u.Subscribe(right);
        var failure = new InvalidOperationException("expected");
        if (error) { a.Fail(failure); u.Fail(failure); } else { a.Complete(); u.Complete(); }
        using var a4 = a.Subscribe(left); using var u4 = u.Subscribe(right);
        Assert.Equal(left.Events, right.Events);
        Assert.Equal(a.Initializations, u.Initializations);
        Assert.Equal(a.Deinitializations, u.Deinitializations);
        Assert.False(u.HasObservers);
    }

    [Fact]
    public void Single_observer_publication_has_no_per_value_allocation()
    {
        var observable = new NativeObservable();
        var observer = new Counter();
        using var subscription = observable.Subscribe(observer);
        for (var i = 0; i < 100; ++i) observable.Next(i);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; ++i) observable.Next(i);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(10_100, observer.Count);
    }

    private sealed class Counter : IObserver<int>
    {
        public int Count;
        public void OnNext(int value) => ++Count;
        public void OnCompleted() { }
        public void OnError(Exception error) => throw error;
    }
    private sealed class Recorder : IObserver<int>
    {
        public List<string> Events { get; } = new();
        public void OnNext(int value) => Events.Add("next:" + value);
        public void OnCompleted() => Events.Add("completed");
        public void OnError(Exception error) => Events.Add("error:" + error.Message);
    }
    private sealed class ReferenceObservable : Avalonia.Experimental.Data.Core.LightweightObservableBase<int>
    {
        public int Initializations, Deinitializations;
        protected override void Initialize() => ++Initializations;
        protected override void Deinitialize() => ++Deinitializations;
        public void Next(int value) => PublishNext(value);
        public void Complete() => PublishCompleted();
        public void Fail(Exception error) => PublishError(error);
    }
    private sealed class NativeObservable : Uno.Experimental.Data.Core.LightweightObservableBase<int>
    {
        public int Initializations, Deinitializations;
        protected override void Initialize() => ++Initializations;
        protected override void Deinitialize() => ++Deinitializations;
        public void Next(int value) => PublishNext(value);
        public void Complete() => PublishCompleted();
        public void Fail(Exception error) => PublishError(error);
    }
    private sealed class Node(string name)
    {
        public string Name { get; } = name;
        public Node? Child { get; set; }
    }
}
