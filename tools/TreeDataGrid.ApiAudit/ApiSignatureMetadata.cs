using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace TreeDataGrid.Tools.ApiAudit;

/// <summary>Signature facts omitted by ordinary C# symbol display, including ordered modreq/modopt.</summary>
internal static class ApiSignatureMetadata
{
    private static readonly SymbolDisplayFormat TypeDisplay = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    internal static void Append(ISymbol symbol, List<string> details, Action<ITypeSymbol?> checkType)
    {
        switch (symbol)
        {
            case IMethodSymbol method:
                Method(method, "signature", details, checkType);
                break;
            case IPropertySymbol property:
                Modifiers(property.TypeCustomModifiers, "property:type", details, checkType);
                Modifiers(property.RefCustomModifiers, "property:ref", details, checkType);
                Type(property.Type, "property:type", details, checkType);
                if (property.GetMethod is { } getter) Method(getter, "get:signature", details, checkType);
                if (property.SetMethod is { } setter) Method(setter, "set:signature", details, checkType);
                break;
            case IFieldSymbol field:
                Modifiers(field.CustomModifiers, "field:type", details, checkType);
                Type(field.Type, "field:type", details, checkType);
                break;
            case IEventSymbol evt:
                Type(evt.Type, "event:type", details, checkType);
                if (evt.AddMethod is { } add) Method(add, "add:signature", details, checkType);
                if (evt.RemoveMethod is { } remove) Method(remove, "remove:signature", details, checkType);
                if (evt.RaiseMethod is { } raise) Method(raise, "raise:signature", details, checkType);
                break;
        }
    }

    private static void Method(IMethodSymbol method, string site, List<string> details, Action<ITypeSymbol?> checkType)
    {
        details.Add($"{site}:calling-convention={method.CallingConvention};vararg={method.IsVararg};hides-by-name={method.HidesBaseMethodsByName}");
        // The sequence, including duplicates, is metadata. Sorting convention or
        // modifier types can conflate different emitted signatures.
        for (var i = 0; i < method.UnmanagedCallingConventionTypes.Length; ++i)
        {
            var type = method.UnmanagedCallingConventionTypes[i];
            checkType(type);
            details.Add($"{site}:unmanaged-convention:{Index(i)}={type.ToDisplayString(TypeDisplay)}");
        }
        Modifiers(method.ReturnTypeCustomModifiers, site + ":return:type", details, checkType);
        Modifiers(method.RefCustomModifiers, site + ":return:ref", details, checkType);
        Type(method.ReturnType, site + ":return:type", details, checkType);
        foreach (var parameter in method.Parameters)
        {
            var parameterSite = site + ":parameter:" + Index(parameter.Ordinal);
            Modifiers(parameter.CustomModifiers, parameterSite + ":type", details, checkType);
            Modifiers(parameter.RefCustomModifiers, parameterSite + ":ref", details, checkType);
            Type(parameter.Type, parameterSite + ":type", details, checkType);
        }
    }

    private static void Type(ITypeSymbol type, string site, List<string> details, Action<ITypeSymbol?> checkType)
    {
        checkType(type);
        switch (type)
        {
            case IArrayTypeSymbol array:
                details.Add($"{site}:array-rank={array.Rank};szarray={array.IsSZArray}");
                Modifiers(array.CustomModifiers, site + ":element", details, checkType);
                Type(array.ElementType, site + ":element", details, checkType);
                break;
            case IPointerTypeSymbol pointer:
                Modifiers(pointer.CustomModifiers, site + ":pointed-at", details, checkType);
                Type(pointer.PointedAtType, site + ":pointed-at", details, checkType);
                break;
            case IFunctionPointerTypeSymbol pointer:
                Method(pointer.Signature, site + ":function-pointer", details, checkType);
                break;
            case INamedTypeSymbol named:
                for (var i = 0; i < named.TypeArguments.Length; ++i)
                {
                    var argumentSite = site + ":type-argument:" + Index(i);
                    Modifiers(named.GetTypeArgumentCustomModifiers(i), argumentSite, details, checkType);
                    Type(named.TypeArguments[i], argumentSite, details, checkType);
                }
                // Type arguments of an enclosing constructed generic type are
                // not necessarily present in the nested type's TypeArguments.
                if (named.ContainingType is { } containing)
                    Type(containing, site + ":containing-type", details, checkType);
                break;
        }
    }

    private static void Modifiers(ImmutableArray<CustomModifier> modifiers, string site,
        List<string> details, Action<ITypeSymbol?> checkType)
    {
        for (var i = 0; i < modifiers.Length; ++i)
        {
            var modifier = modifiers[i];
            checkType(modifier.Modifier);
            details.Add($"custom-modifier:{site}:{Index(i)}={(modifier.IsOptional ? "modopt" : "modreq")}({modifier.Modifier.ToDisplayString(TypeDisplay)})");
        }
    }

    private static string Index(int value) => value.ToString(CultureInfo.InvariantCulture);
}
