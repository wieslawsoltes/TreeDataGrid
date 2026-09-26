using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Public-model construction checks shared by unit, native and published-browser consumers.</summary>
internal static class PublicExpanderConstructionChecks
{
    internal static IReadOnlyList<string> Cases { get; } =
    [
        "row-add-before", "row-add-after",
        "show-subscribe-before", "show-subscribe-after",
        "expanded-subscribe-before", "expanded-subscribe-after",
        "initial-show-callback", "initial-expanded-callback",
        "row-add-failure", "row-add-and-cleanup-failure",
        "show-subscribe-failure", "expanded-subscribe-failure",
        "ordered-cleanup-errors", "retired-Core-write",
        "permission-getter-retirement", "visibility-getter-retirement",
        "permission-getter-error", "visibility-getter-error", "independent-cell-recovery",
    ];

    internal static void RunAll()
    {
        foreach (var name in Cases)
        {
            Run(name);
            Console.WriteLine("UNO_PUBLIC_EXPANDER_CONSTRUCTION_CASE_PASSED: " + name);
        }
        Console.WriteLine($"UNO_RUNTIME_PUBLIC_EXPANDER_CONSTRUCTION_PASSED: cases={Cases.Count}; " +
            "event/subscription construction retirement, deferred cleanup, exception identity/order, " +
            "retired writes, permission/visibility callbacks, borrowed Core ownership and independent recovery");
    }

    internal static void Run(string name)
    {
        switch (name)
        {
            case "row-add-before": CancelConstruction(0, false); break;
            case "row-add-after": CancelConstruction(0, true); break;
            case "show-subscribe-before": CancelConstruction(1, false); break;
            case "show-subscribe-after": CancelConstruction(1, true); break;
            case "expanded-subscribe-before": CancelConstruction(2, false); break;
            case "expanded-subscribe-after": CancelConstruction(2, true); break;
            case "initial-show-callback": CancelInitialValue(false); break;
            case "initial-expanded-callback": CancelInitialValue(true); break;
            case "row-add-failure": RowAddFailure(false); break;
            case "row-add-and-cleanup-failure": RowAddFailure(true); break;
            case "show-subscribe-failure": SubscribeFailure(false); break;
            case "expanded-subscribe-failure": SubscribeFailure(true); break;
            case "ordered-cleanup-errors": CleanupErrors(); break;
            case "retired-Core-write": RetiredCoreWrite(); break;
            case "permission-getter-retirement": GetterRetirement(false); break;
            case "visibility-getter-retirement": GetterRetirement(true); break;
            case "permission-getter-error": GetterError(false); break;
            case "visibility-getter-error": GetterError(true); break;
            case "independent-cell-recovery": IndependentRecovery(); break;
            default: throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown expander construction case.");
        }
    }

    private static void CancelConstruction(int boundary, bool afterAttach)
    {
        var row = new Row();
        var inner = new ContentCell();
        var show = new Values(true);
        var expanded = new Values(true);
        Action retire = () => row.Cell!.Dispose();
        if (boundary == 0)
        {
            if (afterAttach) row.AfterAdd = retire; else row.BeforeAdd = retire;
        }
        else
        {
            var input = boundary == 1 ? show : expanded;
            if (afterAttach) input.AfterAttach = retire; else input.BeforeAttach = retire;
        }
        using var cell = new UI.ExpanderCell<object>(inner, row, show, expanded);
        Check(ReferenceEquals(row.Cell, cell), "The custom row did not capture the actual constructing cell.");
        Clean(row, inner, show, expanded);
        Check(!row.RemovedWhileAdding && !show.ReleasedWhileSubscribing && !expanded.ReleasedWhileSubscribing,
            "Construction-owned cleanup ran before the event/subscription accessor unwound.");
        Check(show.SubscribeCalls == (boundary == 0 ? 0 : 1) && expanded.SubscribeCalls == (boundary == 2 ? 1 : 0),
            "A cancelled constructor entered a later subscription stage.");
        Check(show.ReleaseCalls == show.SubscribeCalls && expanded.ReleaseCalls == expanded.SubscribeCalls,
            "A lease returned after retirement was not released exactly once.");
        var oldShow = row.RawShow;
        var oldExpanded = row.RawExpanded;
        show.Captured?.OnNext(!oldShow);
        expanded.Captured?.OnNext(!oldExpanded);
        show.Captured?.OnError(new InvalidOperationException("retired"));
        Check(row.RawShow == oldShow && row.RawExpanded == oldExpanded && cell.Error is null,
            "A callback from a cancelled construction changed the borrowed row or error state.");
        Throws<ObjectDisposedException>(() => cell.IsExpanded = !oldExpanded);
        Check(row.RawExpanded == oldExpanded, "A cancelled cell wrote to its borrowed row.");
        cell.Dispose();
        Clean(row, inner, show, expanded);
    }

    private static void CancelInitialValue(bool expansion)
    {
        var row = new Row();
        var inner = new ContentCell();
        var show = new Values(true);
        var expanded = new Values(true);
        if (expansion) row.ExpandedWrite = () => row.Cell!.Dispose();
        else row.ShowWrite = () => row.Cell!.Dispose();
        using var cell = new UI.ExpanderCell<object>(inner, row, show, expanded);
        Check(row.RawShow && row.RawExpanded == expansion, "The triggering value was not delivered to the Core row.");
        Clean(row, inner, show, expanded);
        Check(!show.ReleasedWhileSubscribing && !expanded.ReleasedWhileSubscribing,
            "An initial OnNext callback disposed a lease while Subscribe was still running.");
        Check(expanded.SubscribeCalls == (expansion ? 1 : 0), "Retirement did not stop subsequent subscription.");
        show.Captured!.OnNext(false);
        expanded.Captured?.OnNext(false);
        Check(row.RawShow && row.RawExpanded == expansion, "A captured callback survived constructor retirement.");
    }

    private static void RowAddFailure(bool cleanupFails)
    {
        var primary = new InvalidOperationException("row add");
        var removal = new InvalidOperationException("row remove");
        var contentFailure = new InvalidOperationException("content");
        var row = new Row { AfterAdd = () => throw primary };
        var inner = new ContentCell();
        if (cleanupFails)
        {
            row.OnRemove = () => throw removal;
            inner.Failure = contentFailure;
        }
        var show = new Values(true);
        var expanded = new Values(false);
        var error = Catch(() => _ = new UI.ExpanderCell<object>(inner, row, show, expanded));
        SameErrors(error, cleanupFails ? [primary, removal, contentFailure] : [primary]);
        Clean(row, inner, show, expanded);
        Check(show.SubscribeCalls == 0 && expanded.SubscribeCalls == 0 && !row.RemovedWhileAdding,
            "A failed row attachment continued construction or cleaned up before the accessor unwound.");
        row.Cell!.Dispose();
        Check(inner.Disposals == 1, "Cleanup failure caused a second content disposal.");
    }

    private static void SubscribeFailure(bool second)
    {
        var primary = new InvalidOperationException("Subscribe");
        var row = new Row();
        var inner = new ContentCell();
        var show = new Values(true);
        var expanded = new Values(true);
        (second ? expanded : show).BeforeAttach = () => throw primary;
        var error = Catch(() => _ = new UI.ExpanderCell<object>(inner, row, show, expanded));
        Check(ReferenceEquals(error, primary), "A constructor failure lost its original exception identity.");
        Clean(row, inner, show, expanded);
        Check(show.ReleaseCalls == (second ? 1 : 0) && expanded.ReleaseCalls == 0,
            "Failure did not release precisely the leases that Subscribe had returned.");
    }

    private static void CleanupErrors()
    {
        var errors = Enumerable.Range(0, 4).Select(i => new InvalidOperationException("cleanup " + i)).ToArray();
        var row = new Row();
        var inner = new ContentCell { Failure = errors[3] };
        var show = new Values(true) { ReleaseFailure = errors[1] };
        var expanded = new Values(true) { ReleaseFailure = errors[2] };
        var cell = new UI.ExpanderCell<object>(inner, row, show, expanded);
        row.OnRemove = () => { cell.Dispose(); throw errors[0]; };
        show.OnRelease = () => { cell.Dispose(); show.Captured!.OnNext(false); };
        expanded.OnRelease = () => expanded.Captured!.OnNext(false);
        inner.OnDispose = cell.Dispose;
        SameErrors(Catch(cell.Dispose), errors);
        Clean(row, inner, show, expanded);
        Check(row.RawShow && row.RawExpanded, "Disposal callbacks wrote through a retired cell.");
        cell.Dispose();
        Check(row.RemoveCalls == 1 && show.ReleaseCalls == 1 && expanded.ReleaseCalls == 1,
            "Recursive or repeated disposal repeated an ownership release.");
    }

    private static void RetiredCoreWrite()
    {
        var model = new Node();
        model.Children.Add(new());
        using var source = new HierarchicalTreeDataGridSource<Node>([model]);
        source.Columns.Add(new HierarchicalExpanderColumn<Node>(
            new TextColumn<Node, string>("Name", _ => "row"), node => node.Children));
        var row = (IExpanderRow<Node>)source.Rows[0];
        var inner = new ContentCell();
        var show = new Values(true);
        var expanded = new Values(false);
        var cell = new UI.ExpanderCell<Node>(inner, row, show, expanded);
        cell.Dispose();
        Throws<ObjectDisposedException>(() => cell.IsExpanded = true);
        Check(source.Rows.Count == 1 && !row.IsExpanded && ReferenceEquals(source.Rows[0], row),
            "Disposed public cell changed the actual Core hierarchy.");
        source.Expand(0);
        Check(source.Rows.Count == 2 && ReferenceEquals(source.Rows[0], row),
            "Cell cleanup disposed or replaced a caller-owned Core row/source.");
        show.Captured!.OnNext(false);
        expanded.Captured!.OnNext(false);
        Check(row.IsExpanded && show.Subscribers == 0 && expanded.Subscribers == 0 && inner.Disposals == 1,
            "Retired values changed a still-live Core source.");
    }

    private static void GetterRetirement(bool visibility)
    {
        var row = new Row();
        var inner = new ContentCell();
        var show = new Values(true);
        var expanded = new Values(true);
        using var cell = new UI.ExpanderCell<object>(inner, row, show, expanded);
        Func<bool> retire = () => { cell.Dispose(); return true; };
        if (visibility) row.ShowRead = retire; else inner.PermissionRead = retire;
        Check(!(visibility ? cell.ShowExpander : cell.CanEdit), "A reentrant getter returned true after retiring its cell.");
        Clean(row, inner, show, expanded);
        inner.PermissionRead = () => throw new InvalidOperationException("retired content getter");
        row.ShowRead = () => throw new InvalidOperationException("retired visibility getter");
        Check(!cell.CanEdit && !cell.ShowExpander, "Retired policy access invoked caller code.");
    }

    private static void GetterError(bool visibility)
    {
        var row = new Row();
        var inner = new ContentCell();
        var show = new Values(true);
        var expanded = new Values(false);
        using var cell = new UI.ExpanderCell<object>(inner, row, show, expanded);
        var error = new InvalidOperationException("getter");
        Func<bool> fail = () => throw error;
        if (visibility) row.ShowRead = fail; else inner.PermissionRead = fail;
        Check(ReferenceEquals(Catch(() => _ = visibility ? cell.ShowExpander : cell.CanEdit), error),
            "A caller policy exception was swallowed or replaced.");
        row.ShowRead = null;
        inner.PermissionRead = null;
        Check(cell.CanEdit && cell.ShowExpander && row.Subscribers == 1, "A getter error corrupted the live cell.");
        cell.Dispose();
        Clean(row, inner, show, expanded);
    }

    private static void IndependentRecovery()
    {
        var row = new Row();
        var show = new Values(true);
        var expanded = new Values(false);
        var oldInner = new ContentCell();
        using var old = new UI.ExpanderCell<object>(oldInner, row, show, expanded);
        var nextInner = new ContentCell();
        // Only the incoming constructor is retired. Its removal must not take
        // the old cell's event handler or independently returned leases with it.
        expanded.AfterAttach = () => row.Cell!.Dispose();
        using var next = new UI.ExpanderCell<object>(nextInner, row, show, expanded);
        Check(row.Subscribers == 1 && show.Subscribers == 1 && expanded.Subscribers == 1 &&
            oldInner.Disposals == 0 && nextInner.Disposals == 1, "Cancelled construction retired an independent live cell.");
        expanded.AfterAttach = null;
        expanded.Publish(true);
        Check(old.IsExpanded && row.RawExpanded, "The surviving cell lost its observation.");
        old.Dispose();
        var recoveredInner = new ContentCell();
        using var recovered = new UI.ExpanderCell<object>(recoveredInner, row, show, expanded);
        recovered.IsExpanded = false;
        Check(!row.RawExpanded && recovered.CanEdit, "The borrowed row was not reusable after cancellation.");
        recovered.Dispose();
        Clean(row, recoveredInner, show, expanded);
        Check(oldInner.Disposals == 1 && nextInner.Disposals == 1, "Independent content ownership was duplicated.");
    }

    private static void Clean(Row row, ContentCell inner, Values show, Values expanded)
    {
        Check(row.Subscribers == 0 && show.Subscribers == 0 && expanded.Subscribers == 0,
            $"Construction/disposal leaked observations: row={row.Subscribers}, show={show.Subscribers}, expanded={expanded.Subscribers}.");
        Check(inner.Disposals == 1, "Owned content was not disposed exactly once.");
        Check(row.Disposals == 0 && show.SourceDisposals == 0 && expanded.SourceDisposals == 0,
            "Cell cleanup disposed a borrowed row or observable source.");
    }

    private static void SameErrors(Exception error, IReadOnlyList<Exception> expected)
    {
        var actual = Flatten(error).ToArray();
        Check(actual.Length == expected.Count, "Cleanup failures were lost or duplicated.");
        for (var i = 0; i < actual.Length; ++i)
            Check(ReferenceEquals(actual[i], expected[i]), "Exception identity or ownership-release order changed.");
        static IEnumerable<Exception> Flatten(Exception value) => value is AggregateException aggregate
            ? aggregate.InnerExceptions.SelectMany(Flatten) : [value];
    }

    private static Exception Catch(Action action)
    {
        try { action(); } catch (Exception error) { return error; }
        throw new InvalidOperationException("Expected operation to fail.");
    }
    private static void Throws<T>(Action action) where T : Exception =>
        Check(Catch(action) is T, "Operation failed with the wrong exception type.");
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Node { public ObservableCollection<Node> Children { get; } = new(); }

    private sealed class Row : IExpanderRow<object>, IDisposable
    {
        private PropertyChangedEventHandler? _handlers;
        private bool _adding;
        internal UI.ExpanderCell<object>? Cell;
        internal Action? BeforeAdd, AfterAdd, OnRemove, ShowWrite, ExpandedWrite;
        internal Func<bool>? ShowRead;
        internal bool RawShow, RawExpanded, RemovedWhileAdding;
        internal int Disposals, RemoveCalls;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public object Model { get; } = new();
        public object? Header => null;
        public GridLength Height { get; set; }
        public void UpdateModelIndex(int delta) { }
        public bool ShowExpander => ShowRead?.Invoke() ?? RawShow;
        public bool IsExpanded
        {
            get => RawExpanded;
            set { RawExpanded = value; ExpandedWrite?.Invoke(); _handlers?.Invoke(this, new(nameof(IsExpanded))); }
        }
        public void UpdateShowExpander(bool value)
        { RawShow = value; ShowWrite?.Invoke(); _handlers?.Invoke(this, new(nameof(ShowExpander))); }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                if (value?.Target is UI.ExpanderCell<object> cell) Cell = cell;
                _adding = true;
                try { BeforeAdd?.Invoke(); _handlers += value; AfterAdd?.Invoke(); }
                finally { _adding = false; }
            }
            remove
            {
                ++RemoveCalls;
                RemovedWhileAdding |= _adding;
                _handlers -= value;
                OnRemove?.Invoke();
            }
        }
        public void Dispose() => ++Disposals;
    }

    private sealed class ContentCell : UI.ICell, IDisposable
    {
        internal int Disposals;
        internal Func<bool>? PermissionRead;
        internal Action? OnDispose;
        internal Exception? Failure;
        public bool CanEdit => PermissionRead?.Invoke() ?? true;
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.F2;
        public object? Value => "content";
        public void Dispose() { ++Disposals; OnDispose?.Invoke(); if (Failure is { } error) throw error; }
    }

    private sealed class Values(bool initial) : IObservable<bool>, IDisposable
    {
        private readonly List<IObserver<bool>> _observers = new();
        private bool _subscribing;
        internal IObserver<bool>? Captured;
        internal Action? BeforeAttach, AfterAttach, OnRelease;
        internal Exception? ReleaseFailure;
        internal bool ReleasedWhileSubscribing;
        internal int SubscribeCalls, ReleaseCalls, SourceDisposals;
        internal int Subscribers => _observers.Count;
        public IDisposable Subscribe(IObserver<bool> observer)
        {
            ++SubscribeCalls;
            Captured = observer;
            _subscribing = true;
            try
            {
                BeforeAttach?.Invoke();
                _observers.Add(observer);
                try { AfterAttach?.Invoke(); observer.OnNext(initial); }
                catch { _observers.Remove(observer); throw; } // The source owns rollback before returning a lease.
                return new Lease(() =>
                {
                    ++ReleaseCalls;
                    ReleasedWhileSubscribing |= _subscribing;
                    _observers.Remove(observer);
                    OnRelease?.Invoke();
                    if (ReleaseFailure is { } error) throw error;
                });
            }
            finally { _subscribing = false; }
        }
        internal void Publish(bool value) { foreach (var observer in _observers.ToArray()) observer.OnNext(value); }
        public void Dispose() { ++SourceDisposals; _observers.Clear(); }
    }

    private sealed class Lease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() { var action = _release; _release = null; action?.Invoke(); }
    }
}
