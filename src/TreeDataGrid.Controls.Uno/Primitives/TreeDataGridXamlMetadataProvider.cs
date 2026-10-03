#if WINDOWS
using System;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace Uno.Controls.Primitives;

/// <summary>
/// XAML type information for classes derived in code from TreeDataGrid controls, such as the
/// cells a custom <see cref="TreeDataGridElementFactory"/> creates.
/// </summary>
/// <remarks>
/// WinUI describes an object through the application's generated XAML metadata. A class that
/// is never used in XAML has no entry there, so WinUI treats it as its nearest WinRT base
/// (<c>Control</c>) and rejects the TreeDataGrid theme style for it ("Cannot apply a Style with
/// TargetType ... to an object of type Control"). Applications include this provider
/// automatically: their generated metadata consults the providers of referenced assemblies. It
/// describes such a class through its base types and adds no members of its own.
/// </remarks>
public sealed class TreeDataGridXamlMetadataProvider : IXamlMetadataProvider
{
    private static readonly Assembly s_assembly = typeof(TreeDataGridXamlMetadataProvider).Assembly;
    private static readonly ConcurrentDictionary<Type, IXamlType> s_byType = new();
    private static readonly ConcurrentDictionary<string, IXamlType> s_byName = new(StringComparer.Ordinal);

    public IXamlType? GetXamlType(Type type)
    {
        if (type is null || type.Assembly == s_assembly || type.ContainsGenericParameters ||
            !typeof(DependencyObject).IsAssignableFrom(type) || !DerivesFromLibraryType(type))
            return null;
        if (s_byType.TryGetValue(type, out var known)) return known;
        // The base is either a library type (known to the generated metadata) or another such
        // derived class, which comes back here through the application's provider.
        if (type.BaseType is not { } baseClass || Application.Current is not IXamlMetadataProvider application ||
            application.GetXamlType(baseClass) is not { } baseType)
            return null;
        var xamlType = s_byType.GetOrAdd(type, _ => new DerivedXamlType(type, baseType));
        s_byName.TryAdd(xamlType.FullName, xamlType);
        return xamlType;
    }

    public IXamlType? GetXamlType(string fullName) =>
        fullName is not null && s_byName.TryGetValue(fullName, out var type) ? type : null;

    public XmlnsDefinition[] GetXmlnsDefinitions() => [];

    private static bool DerivesFromLibraryType(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current.Assembly == s_assembly) return true;
        return false;
    }

    private sealed class DerivedXamlType(Type type, IXamlType baseType) : IXamlType
    {
        public IXamlType BaseType => baseType;
        public IXamlType BoxedType => baseType.BoxedType;
        public IXamlMember ContentProperty => baseType.ContentProperty;
        public string FullName => type.FullName ?? type.Name;
        public bool IsArray => false;
        public bool IsBindable => baseType.IsBindable;
        public bool IsCollection => baseType.IsCollection;
        public bool IsConstructible => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) is not null;
        public bool IsDictionary => baseType.IsDictionary;
        public bool IsMarkupExtension => baseType.IsMarkupExtension;
        public IXamlType ItemType => baseType.ItemType;
        public IXamlType KeyType => baseType.KeyType;
        public Type UnderlyingType => type;
        public object ActivateInstance() => Activator.CreateInstance(type)!;
        public void AddToMap(object instance, object key, object value) => baseType.AddToMap(instance, key, value);
        public void AddToVector(object instance, object value) => baseType.AddToVector(instance, value);
        public object CreateFromString(string value) => baseType.CreateFromString(value);
        public IXamlMember GetMember(string name) => baseType.GetMember(name);
        public void RunInitializer() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
    }
}
#endif
