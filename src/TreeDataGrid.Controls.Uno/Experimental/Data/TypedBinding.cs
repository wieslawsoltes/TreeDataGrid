using System;
using Uno.Data;
using TreeDataGridCore.Models;
using Uno.Experimental.Data.Core;

namespace Uno.Experimental.Data;

/// <summary>Reusable delegate-based binding description. Instantiation snapshots the delegates and links.</summary>
/// <remarks>
/// Native target attachment uses dependency-property local values, not Avalonia's
/// styled/direct-property or priority stack. Sources and descriptor inputs remain
/// caller-owned; each expression owns only its observations.
/// </remarks>
public partial class TypedBinding<TIn, TOut> where TIn : class
{
    private Func<TIn, TOut>? _read;
    private Action<TIn, TOut>? _write;
    private Func<TIn, object>[]? _links;
    private Optional<TOut> _fallbackValue;
    private ValueColumn<TIn, TOut>? _expressionColumn;
    private Func<TIn, object?>[]? _expressionLinks;
    // Cells capture descriptors, not live settings. Version the scalar settings
    // without invoking application equality; arrays additionally need an element
    // identity check because callers can change Links in place.
    internal int CellRevision { get; private set; }
    public Func<TIn, TOut>? Read
    {
        get => _read;
        set { _read = value; _expressionColumn = null; unchecked { ++CellRevision; } }
    }
    public Action<TIn, TOut>? Write
    {
        get => _write;
        set { _write = value; _expressionColumn = null; unchecked { ++CellRevision; } }
    }
    public Func<TIn, object>[]? Links
    {
        get => _links;
        set { _links = value; _expressionLinks = null; unchecked { ++CellRevision; } }
    }
    public BindingMode Mode { get; set; }
    public Optional<TOut> FallbackValue
    {
        get => _fallbackValue;
        set { _fallbackValue = value; unchecked { ++CellRevision; } }
    }
    public Optional<TIn> Source { get; set; }

    public TypedBindingExpression<TIn, TOut> Instance(TIn? source, BindingMode mode = BindingMode.OneWay)
    {
        Validate(mode);
        return new(new ConstantRoot(source), GetPlan(mode), FallbackValue);
    }

    internal TypedBindingExpression<TIn, TOut> InstanceForCell(TIn? source, BindingMode mode = BindingMode.OneWay,
        Optional<TOut>? fallback = null)
    {
        Validate(mode);
        return TypedBindingExpression<TIn, TOut>.CreateForCell(source, GetPlan(mode), fallback ?? FallbackValue);
    }
    private (ValueColumn<TIn, TOut> Column, Func<TIn, object?>[] Links) GetPlan(BindingMode mode)
    {
        // Instruction storage is reused directly, without an extra plan object.
        // The tuple is a value type; it never owns a root or an observation.
        Func<TIn, object?>[] links;
        if (mode == BindingMode.OneTime)
            links = Array.Empty<Func<TIn, object?>>();
        else if (_expressionLinks is { } cached && TypedBindingPlan<TIn, TOut>.MatchesLinks(Links!, cached))
            links = cached;
        else
            links = TypedBindingPlan<TIn, TOut>.CopyLinks(Links!);
        // Validate/copy links before publishing any new instruction snapshot.
        var column = _expressionColumn ?? ValueColumn<TIn, TOut>.FromDelegate(null, Read!, setter: Write);
        if (mode != BindingMode.OneTime) _expressionLinks = links;
        _expressionColumn = column;
        return (column, links);
    }

    private void Validate(BindingMode mode)
    {
        if (mode is < BindingMode.Default or > BindingMode.OneWayToSource)
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (Read is null) throw new InvalidOperationException("Cannot bind TypedBinding: Read is uninitialized.");
        if (Links is null) throw new InvalidOperationException("Cannot bind TypedBinding: Links is uninitialized.");
        if (mode is BindingMode.TwoWay or BindingMode.OneWayToSource && Write is null)
            throw new InvalidOperationException("Cannot bind TypedBinding: Write is uninitialized.");
    }
    private sealed class ConstantRoot(TIn? value) : IObservable<TIn?>
    {
        public IDisposable Subscribe(IObserver<TIn?> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            observer.OnNext(value);
            return EmptySubscription.Instance;
        }
    }
    private sealed class EmptySubscription : IDisposable
    {
        internal static readonly EmptySubscription Instance = new();
        public void Dispose() { }
    }
}
