using System;
using System.Globalization;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Uno.Controls.Presentation;

/// <summary>Native binding lifetime for a realized declarative cell or a transient Core accessor.</summary>
internal sealed partial class NativeColumnBinding : IDisposable
{
    private readonly Binding _binding;
    private Binding? _activeBinding;
    private readonly Action? _changed;
    private readonly CultureInfo _culture;
    private Probe? _probe;
    private object? _model;
    private int _updating;
    private bool _disposed;
    private int _revision;
    internal NativeColumnBinding(Binding binding, Action? changed = null, CultureInfo? culture = null)
    {
        // Editing is committed explicitly by CellEditSession, never on a probe
        // value assignment. Do not mutate the caller's reusable Binding object.
        _binding = CopyBinding(binding);
        InitializeNamedSource();
        _changed = changed;
        _culture = culture ?? CultureInfo.CurrentCulture;
    }
    private static Binding CopyBinding(Binding binding) => new()
        {
            Path = binding.Path, Mode = binding.Mode, Source = binding.Source,
            RelativeSource = binding.RelativeSource, ElementName = binding.ElementName,
            Converter = binding.Converter, ConverterParameter = binding.ConverterParameter,
            ConverterLanguage = binding.ConverterLanguage, FallbackValue = binding.FallbackValue,
            TargetNullValue = binding.TargetNullValue, UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
        };
    internal bool CanWrite => _binding.Mode == BindingMode.TwoWay;
    internal static IDisposable Observe(Binding binding, object model, Action changed)
    {
        var ready = false;
        var observer = new NativeColumnBinding(binding, () => { if (ready) changed(); });
        try
        {
            observer.Retarget(model);
            if (observer.Error is { } error) throw error;
            ready = true;
            return observer;
        }
        catch { observer.Dispose(); throw; }
    }
    internal object? Value
    {
        get
        {
            // Uno does not signal a subject being set to null. Do not expose its
            // retired data, or start a new realization from a snapshot getter.
            if (_activeBinding is { } binding && !IsNamedSourceCurrent(binding)) return null;
            return _model is not null && _probe?.GetValue(Probe.ValueProperty) is { } value &&
                !ReferenceEquals(value, DependencyProperty.UnsetValue) ? value : null;
        }
    }
    internal Exception? Error { get; private set; }
    internal void Retarget(object model)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var revision = ++_revision;
        _model = model;
        Probe? probe = null;
        ++_updating;
        try
        {
            var binding = ResolveNamedSource();
            if (!ReferenceEquals(_activeBinding, binding) && _probe is { } previous)
            {
                _probe = null;
                _activeBinding = null;
                try { previous.DataContext = null; }
                finally { previous.ClearValue(Probe.ValueProperty); }
                if (revision != _revision) return;
            }
            _activeBinding = binding;
            probe = _probe ??= new Probe(OnChanged);
            // Native row-context bindings keep their expression while pooled;
            // explicit-source bindings are disconnected completely on Suspend.
            if (probe.GetBindingExpression(Probe.ValueProperty) is null) probe.SetBinding(Probe.ValueProperty, binding);
            if (revision != _revision) return;
            if (ReferenceEquals(probe.DataContext, model)) probe.DataContext = null;
            if (revision != _revision) return;
            probe.DataContext = HasNamedSource ? null : model;
            if (revision == _revision) Error = null;
        }
        catch (Exception error) { if (revision == _revision) Error = error; }
        finally
        {
            try
            {
                // A converter can dispose this binding while SetBinding is
                // still installing its expression. Detach again after it returns.
                if (probe is not null && (_disposed || !ReferenceEquals(_probe, probe)))
                {
                    try { probe.DataContext = null; }
                    finally { probe.ClearValue(Probe.ValueProperty); }
                }
            }
            finally { --_updating; }
        }
        if (revision == _revision) _changed?.Invoke();
    }
    internal object? ReadSnapshot(object model)
    {
        var revision = _revision + 1;
        try
        {
            Retarget(model);
            if (revision != _revision) throw new OperationCanceledException("The binding changed while reading its snapshot.");
            if (Error is { } error) throw error;
            return Value;
        }
        finally { if (revision == _revision) Suspend(); }
    }
    internal void Write(object? value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanWrite || _model is null || _probe is null) throw new InvalidOperationException("The binding is read-only or suspended.");
        var revision = _revision;
        var binding = _activeBinding ?? _binding;
        var expression = _probe.GetBindingExpression(Probe.ValueProperty)
            ?? throw new InvalidOperationException("The native binding is detached.");
        var endpoint = expression.DataItem;
        ExceptionDispatchInfo? failure = null;
        ++_updating;
        try
        {
            if (!IsNamedSourceCurrent(binding)) throw new OperationCanceledException("The named binding source changed before writeback.");
            if (HasNamedSource && binding.Source is null) throw new InvalidOperationException("The named binding source is not available.");
            if (!NativeBindingPathWriter.TryWrite(binding, _model, value, _culture, IsCurrent))
            {
                var written = false;
#if !WINDOWS
                // Uno exposes the actual native source root publicly. Keep its
                // named/relative resolution, but preserve ordinary setter errors.
                var source = expression.DataContext;
                written = NativeBindingPathWriter.TryWriteResolvedSource(binding, source, value, _culture,
                    () => IsCurrent() && ReferenceEquals(expression.DataContext, source));
#endif
                if (!written)
                {
                    _probe.SetValue(Probe.ValueProperty, value);
                    if (!IsCurrent()) throw new OperationCanceledException("The binding changed before native source update.");
                    expression.UpdateSource();
                }
            }
            if (revision == _revision) Error = null;
        }
        catch (Exception error)
        {
            failure = ExceptionDispatchInfo.Capture(error);
            if (revision == _revision) Error = error;
        }
        finally
        {
            // Refresh even without INPC. A setter may have recycled the cell;
            // never restore that old model after a reentrant suspend/retarget.
            try
            {
                if (revision == _revision && _model is { } model && _probe is { } probe)
                {
                    if (!IsNamedSourceCurrent(binding)) Retarget(model);
                    else if (binding.Source is not null || HasNamedSource)
                    {
                        // DataContext changes do not refresh an explicit Source.
                        // Re-evaluate after writes even when it does not raise INPC.
                        try
                        {
                            probe.ClearValue(Probe.ValueProperty);
                            if (IsRealizationCurrent()) probe.SetBinding(Probe.ValueProperty, binding);
                        }
                        finally
                        {
                            if (!ReferenceEquals(_probe, probe)) probe.ClearValue(Probe.ValueProperty);
                        }
                    }
                    else
                    {
                        probe.DataContext = null;
                        if (IsRealizationCurrent()) probe.DataContext = model;
                    }
                }
            }
            catch (Exception error)
            {
                failure ??= ExceptionDispatchInfo.Capture(error);
                if (revision == _revision) Error = failure.SourceException;
            }
            finally { --_updating; }
            if (revision == _revision)
            {
                try { _changed?.Invoke(); }
                catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
            }
        }
        failure?.Throw();
        bool IsRealizationCurrent() => revision == _revision && !_disposed && IsNamedSourceCurrent(binding);
        bool IsCurrent() => IsRealizationCurrent() && ReferenceEquals(expression.DataItem, endpoint);
    }
    internal void WriteSnapshot(object model, object? value)
    {
        var revision = _revision + 1;
        try
        {
            Retarget(model);
            if (revision != _revision) throw new OperationCanceledException("The binding changed while preparing its write snapshot.");
            if (Error is { } error) throw error;
            Write(value);
        }
        finally { if (revision == _revision) Suspend(); }
    }
    internal void Suspend()
    {
        var revision = ++_revision;
        _model = null;
        DetachNamedSource();
        if (HasNamedSource) _activeBinding = null;
        ++_updating;
        try
        {
            if (_probe is { } probe)
            {
                try { probe.DataContext = null; }
                catch
                {
                    // Failed detachment must not leave a live expression in a
                    // model pool. Never clear a newer reentrant realization.
                    if (revision == _revision) probe.ClearValue(Probe.ValueProperty);
                    throw;
                }
                if (revision == _revision && (_binding.Source is not null || _binding.ElementName is not null || _binding.RelativeSource is not null))
                    probe.ClearValue(Probe.ValueProperty);
            }
            if (revision == _revision) Error = null;
        }
        finally { --_updating; }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ++_revision;
        _model = null;
        DetachNamedSource();
        _activeBinding = null;
        var probe = _probe;
        _probe = null;
        Error = null;
        ++_updating;
        try
        {
            try { if (probe is not null) probe.DataContext = null; }
            finally { probe?.ClearValue(Probe.ValueProperty); }
        }
        finally { --_updating; }
    }
    private void OnChanged() { if (_updating == 0 && !_disposed) _changed?.Invoke(); }

    private sealed class Probe(Action changed) : FrameworkElement
    {
        internal static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value", typeof(object), typeof(Probe), new PropertyMetadata(null, (sender, _) => ((Probe)sender).Changed()));
        private void Changed() => changed();
    }
}
