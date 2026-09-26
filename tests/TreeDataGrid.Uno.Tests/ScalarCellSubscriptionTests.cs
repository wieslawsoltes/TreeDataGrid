using System;
using System.ComponentModel;
using System.Reflection;
using Uno.Data;
using Xunit;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class ScalarCellSubscriptionTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Synchronous_retirement_releases_the_returning_lease_once(int kind)
    {
        var fixture = Create(kind); var source = fixture.Source;
        source.InsideSubscribe = () =>
        {
            source.Emit(false);
            ((IDisposable)source.Cell!).Dispose();
            source.Emit(true);
        };
        using var cell = fixture.Construct();
        Assert.Equal(source.First, source.Cell!.Value);
        Assert.Equal(1, source.LeasesReturned);
        Assert.Equal(1, source.LeaseDisposals);
        Assert.Equal(0, source.Active);
        cell.Dispose();
        Assert.Equal(1, source.LeaseDisposals);
        Assert.Equal(0, source.SourceDisposals);
        Assert.Throws<ObjectDisposedException>(() => Write(source.Cell, source.Second));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Returning_lease_cleanup_failure_is_preserved_and_never_retried(int kind)
    {
        var fixture = Create(kind); var source = fixture.Source;
        var failure = new InvalidOperationException("late lease cleanup");
        source.CleanupFailure = failure;
        source.InsideSubscribe = () => ((IDisposable)source.Cell!).Dispose();
        Assert.Same(failure, Record.Exception(() => fixture.Construct()));
        Assert.Equal(1, source.LeasesReturned);
        Assert.Equal(1, source.LeaseDisposals);
        ((IDisposable)source.Cell!).Dispose();
        source.Emit(false); source.Emit(true); source.Fail(failure);
        Assert.Equal(1, source.LeaseDisposals);
        Assert.Equal(0, source.SourceDisposals);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Subscribe_failure_retires_the_escaped_cell_without_disposing_the_source(int kind)
    {
        var fixture = Create(kind); var source = fixture.Source;
        var failure = new InvalidOperationException("subscribe");
        source.SubscribeFailure = failure;
        source.InsideSubscribe = () => source.Emit(false);
        Assert.Same(failure, Record.Exception(() => fixture.Construct()));
        var delivered = 0;
        ((INotifyPropertyChanged)source.Cell!).PropertyChanged += (_, _) => ++delivered;
        source.Emit(true); source.Fail(new Exception("late error")); source.Complete();
        Assert.Equal(0, delivered);
        Assert.Equal(source.First, source.Cell!.Value);
        Assert.Throws<ObjectDisposedException>(() => Write(source.Cell, source.Second));
        Assert.Equal(0, source.LeasesReturned);
        Assert.Equal(0, source.LeaseDisposals);
        // No handle was returned. Only the publisher can undo its registration.
        Assert.Equal(1, source.Active);
        Assert.Equal(0, source.SourceDisposals);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Initial_notification_failure_retains_exception_identity_and_rejects_late_callbacks(int kind)
    {
        var fixture = Create(kind); var source = fixture.Source;
        var failure = new InvalidOperationException("consumer notification");
        var delivered = 0;
        source.InsideSubscribe = () =>
        {
            ((INotifyPropertyChanged)source.Cell!).PropertyChanged += (_, _) => { ++delivered; throw failure; };
            source.Emit(false);
        };
        Assert.Same(failure, Record.Exception(() => fixture.Construct()));
        Assert.Equal(1, delivered);
        source.Emit(true); source.Fail(failure);
        Assert.Equal(1, delivered);
        Assert.Equal(0, source.LeasesReturned);
        Assert.Equal(0, source.LeaseDisposals);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Completion_keeps_last_value_and_preserves_existing_live_update_behavior(int kind)
    {
        var fixture = Create(kind); var source = fixture.Source;
        source.InsideSubscribe = () => source.Emit(false);
        using var cell = fixture.Construct();
        Assert.Equal(source.First, source.Cell!.Value);
        source.Complete();
        Assert.Equal(0, source.LeaseDisposals);
        source.Emit(true);
        Assert.Equal(source.Second, source.Cell.Value);
        cell.Dispose();
        var calls = 0;
        ((INotifyPropertyChanged)source.Cell).PropertyChanged += (_, _) => ++calls;
        source.Emit(false); source.Fail(new Exception("retired")); source.Complete();
        Assert.Equal(0, calls);
        Assert.Equal(1, source.LeaseDisposals);
        Assert.Equal(0, source.Active);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Initial_error_can_recover_with_a_value_and_does_not_own_the_publisher(int kind)
    {
        var fixture = Create(kind); var source = fixture.Source;
        var failure = new InvalidOperationException("initial error");
        source.InsideSubscribe = () => source.Fail(failure);
        using var cell = fixture.Construct();
        Assert.Same(failure, Error(source.Cell!));
        source.Emit(false);
        Assert.Null(Error(source.Cell!));
        Assert.Equal(source.First, source.Cell!.Value);
        cell.Dispose();
        Assert.Equal(1, source.LeaseDisposals);
        Assert.Equal(0, source.SourceDisposals);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Initial_values_do_not_write_back_but_a_live_edit_does(int kind)
    {
        var fixture = Create(kind); var source = fixture.Source;
        source.InsideSubscribe = () => source.Emit(false);
        using var cell = fixture.Construct();
        Assert.Equal(0, source.Writes);
        Write(source.Cell!, source.Second);
        Assert.Equal(1, source.Writes);
        Assert.Equal(source.Second, source.Cell!.Value);
        cell.Dispose();
        Assert.Throws<ObjectDisposedException>(() => Write(source.Cell, source.First));
        Assert.Equal(1, source.Writes);
        Assert.Equal(1, source.LeaseDisposals);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Reentrant_disposal_from_the_lease_cannot_release_twice(int kind)
    {
        var fixture = Create(kind); var source = fixture.Source;
        using var cell = fixture.Construct();
        source.OnLeaseDispose = cell.Dispose;
        cell.Dispose(); cell.Dispose();
        Assert.Equal(1, source.LeaseDisposals);
        Assert.Equal(0, source.Active);
        Assert.Equal(0, source.SourceDisposals);
    }

    private static Exception? Error(UI.ICell cell) => cell switch
    {
        UI.TextCell<int> text => text.Error,
        UI.CheckBoxCell check => check.Error,
        _ => throw new InvalidOperationException(),
    };
    private static void Write(UI.ICell cell, object value)
    {
        if (cell is UI.TextCell<int> text) text.Value = (int)value;
        else ((UI.CheckBoxCell)cell).Value = (bool)value;
    }
    private static Fixture Create(int kind) => kind switch
    {
        0 => Make<int>(11, 23, 11, 23, source => new UI.TextCell<int>(source, false)),
        1 => Make<bool?>(true, false, true, false, source => new UI.CheckBoxCell(source, false, true)),
        2 => Make<BindingValue<int>>(11, 23, 11, 23, source => new UI.TextCell<int>(source, false)),
        3 => Make<BindingValue<bool?>>((bool?)true, (bool?)false, true, false, source => new UI.CheckBoxCell(source, false, true)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    private static Fixture Make<T>(T first, T second, object firstValue, object secondValue, Func<ProbeSource<T>, IDisposable> create)
    {
        var source = new ProbeSource<T>(first, second, firstValue, secondValue);
        return new(source, () => create(source));
    }
    private sealed record Fixture(SourceBase Source, Func<IDisposable> Construct);
    private abstract class SourceBase(object first, object second) : IDisposable
    {
        internal object First { get; } = first;
        internal object Second { get; } = second;
        internal UI.ICell? Cell;
        internal Action? InsideSubscribe, OnLeaseDispose;
        internal Exception? SubscribeFailure, CleanupFailure;
        internal int Active, LeasesReturned, LeaseDisposals, SourceDisposals, Writes;
        internal abstract void Emit(bool second);
        internal abstract void Fail(Exception error);
        internal abstract void Complete();
        public void Dispose() => ++SourceDisposals;

        // Test-only discovery of a constructing cell. Supports both the original
        // delegate wrapper and the direct-owner observer; no production hook or
        // private reflection is required by shipped native/trimmed consumers.
        protected static UI.ICell FindCell(object observer)
        {
            foreach (var field in observer.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var value = field.GetValue(observer);
                if (value is UI.ICell cell) return cell;
                if (value is Delegate { Target: UI.ICell target }) return target;
            }
            throw new InvalidOperationException("The test observer does not expose its cell owner.");
        }
        protected sealed class Lease(SourceBase source) : IDisposable
        {
            public void Dispose()
            {
                ++source.LeaseDisposals;
                --source.Active;
                source.OnLeaseDispose?.Invoke();
                if (source.CleanupFailure is { } error) throw error;
            }
        }
    }
    private sealed class ProbeSource<T>(T first, T second, object firstValue, object secondValue)
        : SourceBase(firstValue, secondValue), IObservable<T>, IObserver<T>
    {
        private IObserver<T>? _observer;
        public IDisposable Subscribe(IObserver<T> observer)
        {
            Cell = FindCell(observer);
            _observer = observer;
            ++Active;
            InsideSubscribe?.Invoke();
            if (SubscribeFailure is { } error) throw error;
            ++LeasesReturned;
            return new Lease(this);
        }
        internal override void Emit(bool useSecond) => _observer!.OnNext(useSecond ? second : first);
        internal override void Fail(Exception error) => _observer!.OnError(error);
        internal override void Complete() => _observer!.OnCompleted();
        public void OnNext(T value) { ++Writes; _observer!.OnNext(value); }
        public void OnError(Exception error) => throw error;
        public void OnCompleted() { }
    }
}
