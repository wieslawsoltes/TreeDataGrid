using System;
using Uno.Controls.Presentation;
using Uno.Data;
using TreeDataGridCore.Models;

namespace Uno.Experimental.Data.Core;

/// <summary>An instantiated typed binding over the existing native cell-binding engine.</summary>
/// <remarks>
/// Use on the source's owning thread. The expression observes INPC/collection
/// links without reflection. Disposing it releases root and link subscriptions;
/// neither source models nor caller-owned source observables are disposed.
/// </remarks>
public class TypedBindingExpression<TIn, TOut> : LightweightObservableBase<BindingValue<TOut>>,
    IObserver<BindingValue<TOut>>, IDisposable where TIn : class
{
    private IObservable<TIn?>? _rootSource;
    private readonly ValueColumn<TIn, TOut> _column;
    private readonly Func<TIn, object?>[] _links;
    private readonly Optional<TOut> _fallback;
    private readonly Action _changed;
    private CellBinding<TIn, TOut>? _binding;
    private IDisposable? _rootSubscription;
    private WeakReference<TIn>? _root;
    private BindingValue<TOut> _current;
    private bool _hasCurrent;
    private bool _active;
    private bool _disposed;
    private int _rootRevision;

    public TypedBindingExpression(IObservable<TIn?> root, Func<TIn, TOut> read,
        Action<TIn, TOut>? write, Func<TIn, object>[] links, Optional<TOut> fallbackValue)
        : this(read, write, links, fallbackValue)
    { _rootSource = root ?? throw new ArgumentNullException(nameof(root)); }

    private TypedBindingExpression(Func<TIn, TOut> read, Action<TIn, TOut>? write,
        Func<TIn, object>[] links, Optional<TOut> fallbackValue)
    {
        // Direct public construction still owns its own instructions. Do not add
        // an otherwise unused plan allocation to the uncached constructor path.
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(links);
        _links = new Func<TIn, object?>[links.Length];
        for (var i = 0; i < links.Length; ++i)
            _links[i] = links[i] ?? throw new ArgumentException("A binding link cannot be null.", nameof(links));
        _column = ValueColumn<TIn, TOut>.FromDelegate(null, read, setter: write);
        _fallback = fallbackValue;
        _changed = ValueChanged;
    }

    internal TypedBindingExpression(IObservable<TIn?> root, (ValueColumn<TIn, TOut> Column, Func<TIn, object?>[] Links) plan,
        Optional<TOut> fallbackValue) : this(plan, fallbackValue)
    { _rootSource = root ?? throw new ArgumentNullException(nameof(root)); }

    private TypedBindingExpression((ValueColumn<TIn, TOut> Column, Func<TIn, object?>[] Links) plan, Optional<TOut> fallbackValue)
    {
        _links = plan.Links;
        _column = plan.Column;
        _fallback = fallbackValue;
        _changed = ValueChanged;
    }

    public string Description => $"TypedBinding<{typeof(TIn).Name}, {typeof(TOut).Name}>";
    internal bool IsActive => _active && !_disposed;
    internal static TypedBindingExpression<TIn, TOut> CreateForCell(TIn? root,
        (ValueColumn<TIn, TOut> Column, Func<TIn, object?>[] Links) plan, Optional<TOut> fallback) =>
        new(plan, fallback) { _root = root is null ? null : new(root) };

    /// <summary>Receives target values. Source errors are followed by a source refresh, as in the reference subject contract.</summary>
    public void OnNext(BindingValue<TOut> value)
    {
        if (!value.HasValue || !IsActive ||
            _binding is not { } binding || !binding.CanWrite)
            return;
        var revision = _rootRevision;
        try { binding.Write(value.Value); }
        catch
        {
            // Preserve the subject rejection policy, but never let an old write
            // refresh a replacement activation or a subsequently selected root.
            if (IsActive && ReferenceEquals(_binding, binding) && revision == _rootRevision)
                binding.RefreshCurrent();
        }
    }
    void IObserver<BindingValue<TOut>>.OnCompleted() { }
    void IObserver<BindingValue<TOut>>.OnError(Exception error) { }

    // Native controls use the explicit throwing path so a rejected edit cannot
    // be reported as a successful commit. OnNext keeps the legacy subject policy.
    internal void WriteValue(TOut value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_active || _binding is null) throw new InvalidOperationException("The binding has no active observers.");
        _binding.Write(value);
    }
    internal void SetRoot(TIn? root)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_rootSource is not null) throw new InvalidOperationException("An observable-root binding cannot be retargeted directly.");
        _root = root is null ? null : new(root);
        if (_active) RootChanged(root);
    }
    internal void SuspendRoot()
    {
        _root = null;
        ++_rootRevision;
        _hasCurrent = false;
        _current = default;
        _binding?.Suspend();
    }
    internal void RefreshCurrent() => _binding?.RefreshCurrent();

    protected override void Initialize()
    {
        if (_disposed) return;
        _active = true;
        var binding = new CellBinding<TIn, TOut>(_column, _changed, _links, publishEveryRefresh: true);
        _binding = binding;
        try
        {
            if (_rootSource is { } source)
            {
                // Subscribe can synchronously emit, dispose or replace ownership
                // before returning its token. Retired tokens are never retained.
                var subscription = source.Subscribe(new RootObserver(this, binding)) ??
                    throw new InvalidOperationException("The root observable returned no subscription.");
                if (IsActive && ReferenceEquals(_binding, binding)) _rootSubscription = subscription;
                else subscription.Dispose();
            }
            else
            {
                TIn? root = null;
                _root?.TryGetTarget(out root);
                RootChanged(root);
            }
        }
        catch (Exception error)
        {
            try { Deinitialize(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }

    protected override void Deinitialize()
    {
        _active = false;
        ++_rootRevision;
        _hasCurrent = false;
        _current = default;
        var binding = _binding;
        var subscription = _rootSubscription;
        _binding = null;
        _rootSubscription = null;
        Exception? failure = null;
        try { binding?.Dispose(); }
        catch (Exception error) { failure = error; }
        try { subscription?.Dispose(); }
        catch (Exception error)
        {
            if (failure is not null) throw new AggregateException(failure, error);
            throw;
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    protected override void Subscribed(IObserver<BindingValue<TOut>> observer, bool first)
    {
        if (!first && _hasCurrent && IsActive) observer.OnNext(_current);
    }

    private void RootChanged(TIn? root)
    {
        if (!IsActive || _binding is not { } binding) return;
        var revision = ++_rootRevision;
        if (root is not null) binding.Retarget(root);
        else
        {
            binding.Suspend();
            if (revision == _rootRevision && IsActive && ReferenceEquals(_binding, binding))
                Publish(BindingValue<TOut>.BindingError(new NullReferenceException("The binding root is null."), _fallback));
        }
    }
    private void ValueChanged()
    {
        if (!IsActive || _binding is not { } binding) return;
        Publish(binding.Error is { } error
            ? BindingValue<TOut>.BindingError(error, _fallback)
            : new BindingValue<TOut>(binding.Value!));
    }
    private void Publish(BindingValue<TOut> value)
    {
        _current = value;
        _hasCurrent = true;
        PublishNext(value);
    }
    private void RootFailed(Exception error)
    {
        Exception? publicationFailure = null;
        try { PublishError(error); }
        catch (Exception failure)
        {
            publicationFailure = failure;
            throw;
        }
        finally
        {
            try { Deinitialize(); }
            catch (Exception cleanup) when (publicationFailure is not null)
            {
                throw new AggregateException(publicationFailure, cleanup);
            }
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rootSource = null;
        _root = null;
        Exception? failure = null;
        try { Deinitialize(); }
        catch (Exception error) { failure = error; }
        try { PublishCompleted(); }
        catch (Exception error)
        {
            if (failure is not null) throw new AggregateException(failure, error);
            throw;
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class RootObserver(TypedBindingExpression<TIn, TOut> owner,
        CellBinding<TIn, TOut> binding) : IObserver<TIn?>
    {
        // An activation owns a distinct binding. Owner activity alone cannot
        // distinguish a callback already captured by a retired subscription.
        private bool IsCurrent => owner.IsActive && ReferenceEquals(owner._binding, binding);
        public void OnNext(TIn? value) { if (IsCurrent) owner.RootChanged(value); }
        public void OnError(Exception error) { if (IsCurrent) owner.RootFailed(error); }
        public void OnCompleted() { } // Completion of a root stream does not freeze its final model.
    }
}
