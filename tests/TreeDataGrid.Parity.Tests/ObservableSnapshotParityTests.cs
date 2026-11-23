using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace TreeDataGrid.Parity.Tests;

public sealed class ObservableSnapshotParityTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Nested_membership_terminal_and_throwing_callbacks_match_Avalonia(int mutation)
    {
        var reference = new ReferenceObservable();
        var native = new NativeObservable();
        var expected = Trace(reference, mutation);
        var actual = Trace(native, mutation);
        Assert.Equal(expected, actual);
        Assert.Equal(reference.Initializations, native.Initializations);
        Assert.Equal(reference.Deinitializations, native.Deinitializations);
        Assert.False(native.HasObservers);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(128)]
    public void Stable_multi_observer_publication_eliminates_reference_snapshot_allocation(int count)
    {
        const int iterations = 4096;
        var reference = new ReferenceObservable();
        var native = new NativeObservable();
        var left = new Counter();
        var right = new Counter();
        var subscriptions = new List<IDisposable>();
        try
        {
            // Duplicate observer identities are distinct subscriptions in the
            // reference contract. Each still receives every publication.
            for (var index = 0; index < count; ++index)
            {
                subscriptions.Add(reference.Subscribe(left));
                subscriptions.Add(native.Subscribe(right));
            }
            for (var index = 0; index < 1024; ++index) { reference.Next(index); native.Next(index); }
            left.Count = 0;
            right.Count = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < iterations; ++index) reference.Next(index);
            var referenceBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < iterations; ++index) native.Next(index);
            var nativeBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            output.WriteLine($"observers={count}; publications={iterations}; referenceBytes={referenceBytes}; nativeBytes={nativeBytes}");
            Assert.True(referenceBytes >= (long)iterations * count * IntPtr.Size);
            Assert.Equal(0L, nativeBytes);
            Assert.Equal(iterations * count, left.Count);
            Assert.Equal(left.Count, right.Count);
        }
        finally { foreach (var subscription in subscriptions) subscription.Dispose(); }
        Assert.False(native.HasObservers);
        Assert.Equal(reference.Deinitializations, native.Deinitializations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Cached_snapshot_does_not_retain_removed_or_terminal_observers(int retirement)
    {
        var (observable, payload) = Retire(retirement);
        for (var index = 0; index < 3 && payload.IsAlive; ++index)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(payload.IsAlive);
        Assert.Equal(retirement == 0, observable.HasObservers);
        GC.KeepAlive(observable);
    }

    private static List<string> Trace(IProbe observable, int mutation)
    {
        var events = new List<string>();
        var subscriptions = new List<IDisposable>();
        IDisposable? second = null;
        var entered = false;
        var b = new Recorder("b", events);
        var c = new Recorder("c", events);
        var a = new Recorder("a", events, value =>
        {
            if (value != 10 || entered) return;
            entered = true;
            switch (mutation)
            {
                case 0: second!.Dispose(); break;
                case 1: subscriptions.Add(observable.Subscribe(c)); break;
                case 2:
                    second!.Dispose();
                    subscriptions.Add(observable.Subscribe(b));
                    break;
                case 3: observable.Complete(); break;
                case 4: observable.Fail(new InvalidOperationException("terminal")); break;
                case 5: throw new InvalidOperationException("observer");
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }
            observable.Next(20);
        });
        subscriptions.Add(observable.Subscribe(a));
        subscriptions.Add(second = observable.Subscribe(b));
        try
        {
            // Construct an immutable snapshot before reentrant membership work.
            observable.Next(0);
            try { observable.Next(10); }
            catch (InvalidOperationException error) { events.Add("caught:" + error.Message); }
            observable.Next(30);
        }
        finally { foreach (var subscription in subscriptions) subscription.Dispose(); }
        return events;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (NativeObservable Observable, WeakReference Payload) Retire(int retirement)
    {
        var observable = new NativeObservable();
        var payload = new object();
        var weak = new WeakReference(payload);
        var observer = new PayloadObserver(payload);
        var first = observable.Subscribe(observer);
        observable.Subscribe(new Counter());
        observable.Next(1);
        if (retirement == 0) first.Dispose();
        else if (retirement == 1) observable.Complete();
        else observable.Fail(new InvalidOperationException("terminal"));
        return (observable, weak);
    }

    private interface IProbe : IObservable<int>
    {
        void Next(int value);
        void Complete();
        void Fail(Exception error);
    }
    private sealed class NativeObservable : Uno.Experimental.Data.Core.LightweightObservableBase<int>, IProbe
    {
        internal int Initializations;
        internal int Deinitializations;
        protected override void Initialize() => ++Initializations;
        protected override void Deinitialize() => ++Deinitializations;
        public void Next(int value) => PublishNext(value);
        public void Complete() => PublishCompleted();
        public void Fail(Exception error) => PublishError(error);
    }
    private sealed class ReferenceObservable : Avalonia.Experimental.Data.Core.LightweightObservableBase<int>, IProbe
    {
        internal int Initializations;
        internal int Deinitializations;
        protected override void Initialize() => ++Initializations;
        protected override void Deinitialize() => ++Deinitializations;
        public void Next(int value) => PublishNext(value);
        public void Complete() => PublishCompleted();
        public void Fail(Exception error) => PublishError(error);
    }
    private sealed class Counter : IObserver<int>
    {
        internal int Count;
        public void OnNext(int value) => ++Count;
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
    private sealed class PayloadObserver(object payload) : IObserver<int>
    {
        public void OnNext(int value) => GC.KeepAlive(payload);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
    private sealed class Recorder(string name, List<string> events, Action<int>? next = null) : IObserver<int>
    {
        public void OnNext(int value) { events.Add(name + ":" + value); next?.Invoke(value); }
        public void OnError(Exception error) => events.Add(name + ":error:" + error.Message);
        public void OnCompleted() => events.Add(name + ":completed");
    }
}
