using System;
using Uno.Data;
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
    public Func<TIn, TOut>? Read { get; set; }
    public Action<TIn, TOut>? Write { get; set; }
    public Func<TIn, object>[]? Links { get; set; }
    public BindingMode Mode { get; set; }
    public Optional<TOut> FallbackValue { get; set; }
    public Optional<TIn> Source { get; set; }

    public TypedBindingExpression<TIn, TOut> Instance(TIn? source, BindingMode mode = BindingMode.OneWay)
    {
        Validate(mode);
        return new(new ConstantRoot(source), Read!, Write,
            mode == BindingMode.OneTime ? Array.Empty<Func<TIn, object>>() : Links!, FallbackValue);
    }

    internal TypedBindingExpression<TIn, TOut> InstanceForCell(TIn? source, BindingMode mode = BindingMode.OneWay,
        Optional<TOut>? fallback = null)
    {
        Validate(mode);
        return TypedBindingExpression<TIn, TOut>.CreateForCell(source, Read!, Write,
            mode == BindingMode.OneTime ? Array.Empty<Func<TIn, object>>() : Links!, fallback ?? FallbackValue);
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
