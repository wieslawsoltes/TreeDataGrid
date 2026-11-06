using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Uno.Controls;

/// <summary>
/// Supplies statically referenced binding endpoints for trimmed and AOT hosts.
/// Native binding still owns observation and target evaluation. Registrations
/// describe types, never individual rows; prefer static accessor lambdas.
/// </summary>
public static class TreeDataGridBindingRegistry
{
    private static readonly ConditionalWeakTable<Type, ModelMetadata> s_models = new();
    private static readonly ConditionalWeakTable<Type, CollectionMetadata> s_collections = new();
    private static readonly ConditionalWeakTable<Type, ConversionMetadata> s_conversions = new();

    /// <summary>Registers a property or field without runtime member discovery.</summary>
    /// <remarks>A null setter preserves the read-only contract. Re-registration replaces this named endpoint.</remarks>
    public static void RegisterProperty<TModel, TValue>(string name,
        Func<TModel, TValue>? getter, Action<TModel, TValue>? setter = null) where TModel : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (getter is null && setter is null)
            throw new ArgumentException("At least one accessor must be provided.", nameof(getter));
        s_models.GetValue(typeof(TModel), static _ => new()).Properties[name] = new(
            typeof(TValue), getter is null ? null : (owner, _) => getter((TModel)owner),
            setter is null ? null : (owner, _, value) => setter((TModel)owner, (TValue)value!));
    }

    /// <summary>Registers a single-argument indexer with its declared key and value types.</summary>
    public static void RegisterIndexer<TModel, TKey, TValue>(
        Func<TModel, TKey, TValue>? getter, Action<TModel, TKey, TValue>? setter = null) where TModel : class
    {
        if (getter is null && setter is null)
            throw new ArgumentException("At least one accessor must be provided.", nameof(getter));
        s_models.GetValue(typeof(TModel), static _ => new()).Indexers[typeof(TKey)] = new(
            typeof(TValue), getter is null ? null : (owner, key) => getter((TModel)owner, (TKey)key!),
            setter is null ? null : (owner, key, value) => setter((TModel)owner, (TKey)key!, (TValue)value!));
    }

    /// <summary>Preserves item-type discovery for an empty declarative collection.</summary>
    public static void RegisterCollection<TCollection, TModel>() where TCollection : IEnumerable<TModel> =>
        s_collections.GetValue(typeof(TCollection), static _ => new(typeof(TModel)));

    /// <summary>Registers conversion for a declared endpoint type without TypeDescriptor discovery.</summary>
    public static void RegisterConversion<TValue>(Func<object?, CultureInfo, TValue> convert)
    {
        ArgumentNullException.ThrowIfNull(convert);
        var holder = s_conversions.GetValue(typeof(TValue), static _ => new());
        System.Threading.Volatile.Write(ref holder.Convert, (value, culture) => convert(value, culture));
    }

    internal static Accessor? FindProperty(Type type, string name)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
            if (s_models.TryGetValue(current, out var metadata) && metadata.Properties.TryGetValue(name, out var accessor))
                return accessor;
        return null;
    }

    internal static (Type Key, Accessor Accessor)? FindIndexer(Type type, Type? keyType)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (!s_models.TryGetValue(current, out var metadata)) continue;
            if (keyType is not null)
            {
                if (metadata.Indexers.TryGetValue(keyType, out var exact)) return (keyType, exact);
            }
            else
            {
                (Type, Accessor)? result = null;
                foreach (var entry in metadata.Indexers)
                {
                    if (result is not null)
                        throw new AmbiguousMatchException($"Binding indexer on '{type.Name}' is ambiguous.");
                    result = (entry.Key, entry.Value);
                }
                if (result is not null) return result;
            }
        }
        return null;
    }

    internal static Type? FindCollectionItemType(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
            if (s_collections.TryGetValue(current, out var metadata)) return metadata.ItemType;
        return null;
    }

    internal static bool TryConvert(Type type, object? value, CultureInfo culture, out object? result)
    {
        if (s_conversions.TryGetValue(type, out var holder) && System.Threading.Volatile.Read(ref holder.Convert) is { } convert)
        {
            result = convert(value, culture);
            return true;
        }
        result = null;
        return false;
    }

    internal sealed record Accessor(Type ValueType, Func<object, object?, object?>? Get, Action<object, object?, object?>? Set);
    private sealed class ModelMetadata
    {
        internal readonly ConcurrentDictionary<string, Accessor> Properties = new(StringComparer.Ordinal);
        internal readonly ConcurrentDictionary<Type, Accessor> Indexers = new();
    }
    private sealed record CollectionMetadata(Type ItemType);
    private sealed class ConversionMetadata
    {
        internal Func<object?, CultureInfo, object?>? Convert;
    }
}
