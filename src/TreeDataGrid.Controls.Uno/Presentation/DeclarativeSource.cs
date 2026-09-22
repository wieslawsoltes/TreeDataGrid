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
        catch { (source as IDisposable)?.Dispose(); context.Dispose(); throw; }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { (Source as IDisposable)?.Dispose(); }
        finally { _context.Dispose(); }
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
    internal object? Sample { get; } = sample;
    internal Type? ModelType { get; } = sample?.GetType() ?? declaredType;
    internal T Own<T>(T resource) where T : IDisposable { _resources.Add(resource); return resource; }
    public void Dispose()
    {
        List<Exception>? errors = null;
        foreach (var resource in _resources)
            try { resource.Dispose(); } catch (Exception error) { (errors ??= new()).Add(error); }
        _resources.Clear();
        if (errors is not null) throw new AggregateException(errors);
    }
}
