using System;
using System.Collections.Generic;
using Uno.Data;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

internal static partial class TypedBindingPlanRuntimeChecks
{
    private static void VerifyScalarSubscriptions()
    {
        VerifyScalar<int>(11, 23, 77, source => new UI.TextCell<int>(source, false),
            cell => ((UI.TextCell<int>)cell).Value = 77);
        VerifyScalar<bool?>(true, false, null, source => new UI.CheckBoxCell(source, false, true),
            cell => ((UI.CheckBoxCell)cell).Value = null);
        VerifyScalar<BindingValue<int>>(11, 23, 77, source => new UI.TextCell<int>(source, false),
            cell => ((UI.TextCell<int>)cell).Value = 77);
        VerifyScalar<BindingValue<bool?>>((bool?)true, (bool?)false, null, source => new UI.CheckBoxCell(source, false, true),
            cell => ((UI.CheckBoxCell)cell).Value = null);

        // Exercise a throwing initial user equality callback using public APIs
        // only. No reflection or private test hook is used by this consumer.
        var failure = new InvalidOperationException("initial scalar equality");
        var first = new ScalarValue(failure);
        var bad = new ScalarSource<ScalarValue>(first)
        {
            OnAttach = observer => observer.OnNext(new ScalarValue(null)),
        };
        Exception? observed = null;
        try { using var rejected = new UI.TextCell<ScalarValue>(bad, true); }
        catch (Exception error) { observed = error; }
        Check(ReferenceEquals(observed, failure) && first.Comparisons == 1 && bad.Leases == 0,
            "The scalar constructor replaced an initial callback exception or returned a lease after failure.");
        bad.Last!.OnNext(new ScalarValue(null));
        bad.Last.OnError(failure);
        Check(first.Comparisons == 1 && bad.Disposals == 0,
            "A failed scalar constructor accepted a late callback or disposed the caller's source.");
        // The publisher owns rollback when Subscribe throws before returning.
        bad.Dispose();
        Console.WriteLine("UNO_RUNTIME_SCALAR_SUBSCRIPTIONS_PASSED: raw/typed text and nullable checkbox, independent owners, synchronous values, writeback, captured stale callbacks, cleanup and throwing-constructor retirement");
    }

    private static void VerifyScalar<T>(T initial, T next, object? edited,
        Func<ScalarSource<T>, UI.ICell> create, Action<UI.ICell> edit)
    {
        var source = new ScalarSource<T>(initial);
        var first = create(source);
        var stale = source.Last!;
        var second = create(source);
        try
        {
            Check(source.Count == 2 && source.Writes == 0, "Scalar initial delivery wrote back or lost an owner.");
            source.Publish(next);
            Check(Equals(first.Value, second.Value), "Scalar cells disagree after the same live publication.");
            edit(first);
            Check(Equals(first.Value, edited) && Equals(second.Value, edited) && source.Writes == 1,
                "Scalar writeback failed or duplicated a writer notification.");
            ((IDisposable)first).Dispose();
            stale.OnNext(initial);
            stale.OnError(new Exception("retired"));
            stale.OnCompleted();
            Check(Equals(second.Value, edited) && source.Count == 1 && source.LeaseDisposals == 1,
                "A retired scalar observer contaminated another owner or lost its lease.");
            source.Publish(next);
            ((IDisposable)second).Dispose();
            Check(source.Count == 0 && source.LeaseDisposals == 2 && source.Disposals == 0,
                "Scalar cells retained observers or disposed their borrowed publisher.");
        }
        finally
        {
            try { ((IDisposable)first).Dispose(); }
            finally { ((IDisposable)second).Dispose(); source.Dispose(); }
        }
    }

    private sealed class ScalarValue(Exception? failure) : IEquatable<ScalarValue>
    {
        public int Comparisons;
        public bool Equals(ScalarValue? other)
        {
            ++Comparisons;
            if (failure is not null) throw failure;
            return ReferenceEquals(this, other);
        }
    }

    private sealed class ScalarSource<T>(T initial) : IObservable<T>, IObserver<T>, IDisposable
    {
        private readonly List<IObserver<T>> _observers = new();
        internal IObserver<T>? Last;
        internal Action<IObserver<T>>? OnAttach;
        internal int Writes, Leases, LeaseDisposals, Disposals;
        internal int Count => _observers.Count;
        public IDisposable Subscribe(IObserver<T> observer)
        {
            Last = observer;
            _observers.Add(observer);
            observer.OnNext(initial);
            OnAttach?.Invoke(observer);
            ++Leases;
            return new Lease(this, observer);
        }
        internal void Publish(T value)
        {
            foreach (var observer in _observers.ToArray()) observer.OnNext(value);
        }
        public void OnNext(T value) { ++Writes; Publish(value); }
        public void OnError(Exception error) => throw error;
        public void OnCompleted() { }
        public void Dispose() { ++Disposals; _observers.Clear(); Last = null; }
        private sealed class Lease(ScalarSource<T> source, IObserver<T> observer) : IDisposable
        {
            public void Dispose() { ++source.LeaseDisposals; source._observers.Remove(observer); }
        }
    }
}
