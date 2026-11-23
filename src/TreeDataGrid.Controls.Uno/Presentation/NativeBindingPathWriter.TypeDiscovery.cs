using System;
using System.Collections.Generic;

namespace Uno.Controls.Presentation;

internal static partial class NativeBindingPathWriter
{
    internal static Type? GetPathValueType(Type? type, string path)
    {
        if (type is null || path.Contains('(') || path.Contains(')')) return null;
        List<Segment> segments;
        try { segments = Parse(path); }
        catch (ArgumentException) { return null; }
        foreach (var segment in segments)
        {
            if (type is null) return null;
            if (segment.Index)
            {
                if (type.IsArray) { type = type.GetElementType(); continue; }
                var registered = FindRegisteredIndexer(type, segment.Name);
                if (registered is { } indexer) { type = indexer.Accessor.ValueType; continue; }
                type = BindingFeatures.ReflectionEnabled ? GetReflectedValueType(type, segment) : null;
            }
            else
            {
                var registered = TreeDataGridBindingRegistry.FindProperty(type, segment.Name);
                if (registered is not null) { type = registered.ValueType; continue; }
                if (GetMetadataPropertyType(type, segment.Name) is { } metadataType) { type = metadataType; continue; }
                type = BindingFeatures.ReflectionEnabled ? GetReflectedValueType(type, segment) : null;
            }
        }
        return type;
    }
}
