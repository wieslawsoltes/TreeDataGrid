using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Uno.Data;
using Uno.Experimental.Data.Core;

namespace Uno.Experimental.Data;

public partial class TypedBinding<TIn, TOut> where TIn : class
{
    /// <summary>Attaches to a native property and returns the observation lifetime.</summary>
    /// <remarks>
    /// Default maps to OneWay. TwoWay uses native property callbacks;
    /// OneWayToSource also sends the initial target value. Without Source, the
    /// root is DataContext; binding DataContext itself reads the visual parent.
    /// Disposal stops observation and leaves the last native local value in place.
    /// This API does not emulate Avalonia's styled/direct-property priority stack.
    /// </remarks>
    public IDisposable Bind(DependencyObject target, DependencyProperty property)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(property);
        var mode = Mode == BindingMode.Default ? BindingMode.OneWay : Mode;
        Validate(mode);
        if (!Source.HasValue && target is not FrameworkElement)
            throw new ArgumentException("A binding without an explicit Source requires a FrameworkElement target.", nameof(target));
        var slot = NativeTypedBindings.GetSlot(target, property);
        var fallback = FallbackValue;
        if (property == FrameworkElement.DataContextProperty && !fallback.HasValue)
            fallback = new(default!);
        var expression = InstanceForCell(null, mode, fallback);
        var attachment = new NativeAttachment(target, property, slot, expression, Source, mode);
        var previous = slot.Current;
        slot.Current = attachment;
        try
        {
            previous?.Dispose();
            if (attachment.IsCurrent) attachment.Start();
            return attachment;
        }
        catch (Exception error)
        {
            try { attachment.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }

    private sealed class NativeAttachment : IDisposable, IObserver<BindingValue<TOut>>
    {
        private readonly WeakReference<DependencyObject> _target;
        private readonly DependencyProperty _property;
        private readonly NativeTypedBindings.Slot _slot;
        private readonly TypedBindingExpression<TIn, TOut> _expression;
        private readonly BindingMode _mode;
        private Optional<TIn> _source;
        private IDisposable? _subscription;
        private long? _targetToken;
        private long? _contextToken;
        private WeakReference<FrameworkElement>? _parent;
        private long? _parentToken;
        private bool _parentEvents;
        private bool _disposed;
        private bool _writingTarget;
        private bool _hasRoot;

        internal NativeAttachment(DependencyObject target, DependencyProperty property, NativeTypedBindings.Slot slot,
            TypedBindingExpression<TIn, TOut> expression, Optional<TIn> source, BindingMode mode)
        {
            _target = new(target);
            _property = property;
            _slot = slot;
            _expression = expression;
            _source = source;
            _mode = mode;
        }
        internal bool IsCurrent => !_disposed && ReferenceEquals(_slot.Current, this);

        internal void Start()
        {
            if (!_target.TryGetTarget(out var target)) { Dispose(); return; }
            if (_mode is BindingMode.TwoWay or BindingMode.OneWayToSource)
                _targetToken = target.RegisterPropertyChangedCallback(_property, TargetChanged);
            if (!_source.HasValue)
            {
                if (_property == FrameworkElement.DataContextProperty)
                {
                    var element = (FrameworkElement)target;
                    _parentEvents = true;
                    element.Loaded += ParentChanged;
                    element.Unloaded += ParentDetached;
                    ObserveParent(element);
                }
                else _contextToken = target.RegisterPropertyChangedCallback(FrameworkElement.DataContextProperty, ContextChanged);
            }
            SetCurrentRoot(target);
            var subscription = _expression.Subscribe(this);
            if (IsCurrent) _subscription = subscription;
            else { subscription.Dispose(); return; }
            if (_mode == BindingMode.OneWayToSource && _hasRoot) TargetChanged(target, _property);
        }

        private void ParentChanged(object sender, RoutedEventArgs args)
        {
            if (!IsCurrent || sender is not FrameworkElement element) return;
            ObserveParent(element);
            SetCurrentRoot(element);
        }
        private void ParentDetached(object sender, RoutedEventArgs args)
        {
            if (!IsCurrent) return;
            ForgetParent();
            _hasRoot = false;
            _expression.SuspendRoot();
        }
        private void ObserveParent(FrameworkElement target)
        {
            ForgetParent();
            var parent = VisualTreeHelper.GetParent(target) as FrameworkElement;
            if (parent is null) return;
            _parent = new(parent);
            _parentToken = parent.RegisterPropertyChangedCallback(FrameworkElement.DataContextProperty, ContextChanged);
        }
        private void ForgetParent()
        {
            var reference = _parent;
            var token = _parentToken;
            _parent = null;
            _parentToken = null;
            if (token is { } value && reference?.TryGetTarget(out var parent) == true)
                parent.UnregisterPropertyChangedCallback(FrameworkElement.DataContextProperty, value);
        }
        private void ContextChanged(DependencyObject sender, DependencyProperty property)
        {
            if (!IsCurrent) return;
            if (_target.TryGetTarget(out var target)) SetCurrentRoot(target);
            else Dispose();
        }
        private void SetCurrentRoot(DependencyObject target)
        {
            if (!IsCurrent) return;
            TIn? root;
            if (_source.HasValue) root = _source.Value;
            else if (_property == FrameworkElement.DataContextProperty)
                root = _parent?.TryGetTarget(out var parent) == true ? parent.DataContext as TIn : null;
            else root = ((FrameworkElement)target).DataContext as TIn;
            _hasRoot = root is not null;
            _expression.SetRoot(root);
            // A replacement DataContext in source-only mode receives the current
            // target value, rather than the retired root's last observed value.
            if (IsCurrent && _subscription is not null && _hasRoot && _mode == BindingMode.OneWayToSource)
                TargetChanged(target, _property);
        }
        private void TargetChanged(DependencyObject sender, DependencyProperty property)
        {
            if (!IsCurrent || _writingTarget || !_hasRoot) return;
            var raw = sender.GetValue(property);
            if (!IsCurrent) return;
            TOut value;
            if (raw is TOut typed) value = typed;
            else if (raw is null && default(TOut) is null) value = default!;
            else throw new InvalidCastException($"Native property value cannot be assigned to {typeof(TOut)}.");
            _expression.WriteValue(value);
        }
        public void OnNext(BindingValue<TOut> value)
        {
            if (!IsCurrent || _mode == BindingMode.OneWayToSource || !value.HasValue) return;
            if (!_target.TryGetTarget(out var target)) { Dispose(); return; }
            var previous = _writingTarget;
            _writingTarget = true;
            try { target.SetValue(_property, value.Value); }
            finally { _writingTarget = previous; }
        }
        public void OnError(Exception error) => System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        public void OnCompleted() { }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _hasRoot = false;
            _source = default;
            if (ReferenceEquals(_slot.Current, this)) _slot.Current = null;
            var subscription = _subscription;
            _subscription = null;
            List<Exception>? errors = null;
            if (_target.TryGetTarget(out var target))
            {
                if (_targetToken is { } targetToken)
                    try { target.UnregisterPropertyChangedCallback(_property, targetToken); }
                    catch (Exception error) { (errors ??= new()).Add(error); }
                if (_contextToken is { } contextToken)
                    try { target.UnregisterPropertyChangedCallback(FrameworkElement.DataContextProperty, contextToken); }
                    catch (Exception error) { (errors ??= new()).Add(error); }
                if (_parentEvents && target is FrameworkElement element)
                {
                    try { element.Loaded -= ParentChanged; }
                    catch (Exception error) { (errors ??= new()).Add(error); }
                    try { element.Unloaded -= ParentDetached; }
                    catch (Exception error) { (errors ??= new()).Add(error); }
                }
            }
            _targetToken = _contextToken = null;
            try { ForgetParent(); }
            catch (Exception error) { (errors ??= new()).Add(error); }
            try { subscription?.Dispose(); }
            catch (Exception error) { (errors ??= new()).Add(error); }
            try { _expression.Dispose(); }
            catch (Exception error) { (errors ??= new()).Add(error); }
            if (errors is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors is not null) throw new AggregateException(errors);
        }
    }
}

// One registry across all TIn/TOut combinations. A later typed binding of a
// different generic type must still retire the previous owner of the same DP.
internal static class NativeTypedBindings
{
    private static readonly ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, Slot>> Targets = new();
    internal sealed class Slot { internal IDisposable? Current; }
    internal static Slot GetSlot(DependencyObject target, DependencyProperty property)
    {
        var slots = Targets.GetOrCreateValue(target);
        if (!slots.TryGetValue(property, out var slot)) slots.Add(property, slot = new());
        return slot;
    }
}
