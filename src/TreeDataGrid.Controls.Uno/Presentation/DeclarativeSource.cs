using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TreeDataGridCore;

namespace Uno.Controls.Presentation;

/// <summary>Owns only a generated Core source and its UI accessor resources.</summary>
internal sealed class DeclarativeSource : IDisposable
{
    private readonly DeclarativeSourceContext _context;
    private bool _disposed;
    private DeclarativeSource(ITreeDataGridSource source, DeclarativeSourceContext context)
    { Source = source; _context = context; }
    internal ITreeDataGridSource Source { get; }

    internal static DeclarativeSource Create(IEnumerable items, IReadOnlyList<TreeDataGridColumn> definitions)
    {
        var projected = new DeclarativeItemsSource(items);
        var sample = projected.FirstOrDefault(x => x is not null);
        var context = new DeclarativeSourceContext(sample, sample?.GetType() ?? GetItemType(items));
        // The generated source owns the projection relay, not the caller's
        // IList. Core selection can outlive row disposal through weak listeners;
        // explicit relay retirement must not depend on a future GC or event.
        context.Own(projected);
        ITreeDataGridSource? source = null;
        try
        {
            var columns = definitions.Select(column => column.CreateCoreColumn<object>(context: context)).ToArray();
            if (definitions.Any(column => column.IsHierarchical))
            {
                var hierarchy = new HierarchicalTreeDataGridSource<object>(projected);
                source = hierarchy;
                foreach (var column in columns) hierarchy.Columns.Add(column);
            }
            else
            {
                var flat = new FlatTreeDataGridSource<object>(projected);
                source = flat;
                foreach (var column in columns) flat.Columns.Add(column);
            }
            return new(source, context);
        }
        catch (Exception error)
        {
            Release(source as IDisposable, context, error);
            throw; // Release preserves and throws the original or combined failure.
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Release(Source as IDisposable, _context);
    }

    private static void Release(IDisposable? source, DeclarativeSourceContext context, Exception? primary = null)
    {
        List<Exception>? errors = primary is null ? null : new() { primary };
        try { source?.Dispose(); }
        catch (Exception error) { (errors ??= new()).Add(error); }
        // A failed Core teardown must never skip native accessor retirement.
        try { context.Dispose(); }
        catch (Exception error) { (errors ??= new()).Add(error); }
        if (errors is { Count: 1 })
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException(errors);
    }
    private static Type? GetItemType(IEnumerable items)
    {
        var type = items.GetType();
        if (type.IsArray) return type.GetElementType();
        if (TreeDataGridBindingRegistry.FindCollectionItemType(type) is { } registered) return registered;
        return BindingFeatures.ReflectionEnabled ? GetReflectedItemType(type) : null;
    }
    [RequiresUnreferencedCode("Empty collection item-type discovery requires preserved interfaces. Use RegisterCollection<TCollection,TModel> in trimmed hosts.")]
    private static Type? GetReflectedItemType(Type type) =>
        type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];
}

internal sealed class DeclarativeSourceContext(object? sample, Type? declaredType) : IDisposable
{
    private readonly List<IDisposable> _resources = new();
    private bool _disposed;
    internal object? Sample { get; } = sample;
    internal Type? ModelType { get; } = sample?.GetType() ?? declaredType;
    // Ownership transfers at entry. A resource created by a reentrant factory
    // after retirement is released, never appended to an already-drained list.
    internal T Own<T>(T resource) where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (_disposed)
        {
            var retired = new ObjectDisposedException(nameof(DeclarativeSourceContext));
            try { resource.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(retired, cleanup); }
            throw retired;
        }
        _resources.Add(resource);
        return resource;
    }
    public void Dispose()
    {
        if (_disposed) return;
        // Snapshot before publication: allocation failure leaves ownership
        // intact and retryable; no callback can run between these assignments.
        var resources = _resources.ToArray();
        _disposed = true;
        _resources.Clear();
        List<Exception>? errors = null;
        foreach (var resource in resources)
            try { resource.Dispose(); } catch (Exception error) { (errors ??= new()).Add(error); }
        if (errors is { Count: 1 })
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException(errors);
    }
}
