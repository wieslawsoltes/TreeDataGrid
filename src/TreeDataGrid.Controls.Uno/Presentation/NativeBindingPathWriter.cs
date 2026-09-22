using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Uno.Controls.Presentation;

/// <summary>
/// Writes the endpoint explicitly because Uno's native BindingPath setter can
/// swallow application exceptions. Native bindings still own read/observation.
/// </summary>
internal static partial class NativeBindingPathWriter
{
    internal static bool TryWrite(Binding binding, object model, object? value, CultureInfo culture, Func<bool>? isCurrent = null)
    {
        if (binding.ElementName is not null || binding.RelativeSource is not null) return false;
        return TryWriteResolvedSource(binding, binding.Source ?? model, value, culture, isCurrent);
    }

    internal static bool TryWriteResolvedSource(Binding binding, object? source, object? value, CultureInfo culture, Func<bool>? isCurrent = null)
    {
        var path = binding.Path?.Path ?? string.Empty;
        if (path.Contains('(') || path.Contains(')')) return false;
        if (source is null) throw new InvalidOperationException("The native binding source is not available in this scope.");
        return WritePath(path, source, value, culture, binding.Converter,
            binding.ConverterParameter, binding.ConverterLanguage, isCurrent);
    }

    internal static bool WritePath(string path, object model, object? value, CultureInfo culture,
        IValueConverter? converter = null, object? converterParameter = null, string? converterLanguage = null, Func<bool>? isCurrent = null)
    {
        var segments = Parse(path);
        if (segments.Count == 0) throw new InvalidOperationException("A row itself is not a writable binding endpoint.");
        object? owner = model;
        try
        {
            for (var i = 0; i < segments.Count - 1; ++i)
            {
                owner = Resolve(owner ?? throw new InvalidOperationException("A binding path owner is null."), segments[i]).Read();
                EnsureCurrent();
                if (owner?.GetType().IsValueType == true)
                    throw new InvalidOperationException("A binding path cannot write through a copied value-type owner.");
            }
            var endpoint = Resolve(owner ?? throw new InvalidOperationException("A binding path owner is null."), segments[^1]);
            EnsureCurrent();
            if (converter is not null)
                value = converter.ConvertBack(value!, endpoint.Type, converterParameter!,
                    string.IsNullOrEmpty(converterLanguage) ? culture.Name : converterLanguage);
            EnsureCurrent();
            if (ReferenceEquals(value, DependencyProperty.UnsetValue))
                throw new InvalidOperationException("The binding converter rejected the value.");
            var converted = ConvertValue(value, endpoint.Type, culture);
            EnsureCurrent();
            if (segments.Count > 1)
            {
                // Conversion can replace a nested owner without replacing the
                // row. Resolve again; never commit into a retired endpoint.
                object? currentOwner = model;
                for (var i = 0; i < segments.Count - 1; ++i)
                {
                    if (currentOwner is null) throw new OperationCanceledException("The binding path owner changed during conversion.");
                    currentOwner = Resolve(currentOwner, segments[i]).Read();
                    EnsureCurrent();
                }
                if (!ReferenceEquals(currentOwner, owner))
                    throw new OperationCanceledException("The binding path owner changed during conversion.");
            }
            endpoint.Write(converted);
            return true;
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
        void EnsureCurrent()
        {
            if (isCurrent?.Invoke() == false) throw new OperationCanceledException("The cell binding changed while resolving its write endpoint.");
        }
    }
}
