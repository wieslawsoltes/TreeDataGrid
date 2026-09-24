using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace TreeDataGrid.Tools.ApiAudit;

/// <summary>Supplemental metadata, never an automatic compatibility waiver.</summary>
internal sealed record ApiSemanticEntry(
    string Assembly, string Owner, string Relation, int BaseDepth,
    string DeclaringAssembly, string Declaration, string Raw, string Normalized);

internal sealed record ApiSemanticSummary(
    int BaselineEntries, int TargetEntries, int ExactMatches,
    int MissingOrDifferent, int AdditionalOrDifferent);

internal static class ApiSemantics
{
    private static readonly SymbolDisplayFormat TypeDisplay = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
    private static readonly SymbolDisplayFormat Display = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters |
            SymbolDisplayGenericsOptions.IncludeTypeConstraints | SymbolDisplayGenericsOptions.IncludeVariance,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeModifiers |
            SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeContainingType |
            SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface,
        delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName |
            SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeDefaultValue |
            SymbolDisplayParameterOptions.IncludeOptionalBrackets,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        kindOptions: SymbolDisplayKindOptions.IncludeTypeKeyword | SymbolDisplayKindOptions.IncludeMemberKeyword,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static ApiSemanticEntry[] Read(INamedTypeSymbol root, Func<string, string> normalize,
        Action<ITypeSymbol?> checkType, Action<string> unresolved)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(normalize);
        ArgumentNullException.ThrowIfNull(checkType);
        ArgumentNullException.ThrowIfNull(unresolved);
        var entries = new List<ApiSemanticEntry>();
        var owner = root.GetDocumentationCommentId() ?? root.ToDisplayString(Display);
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var depth = 0;
        for (var type = root; type is not null; type = type.BaseType, ++depth)
        {
            if (!visited.Add(type)) { unresolved("Cyclic base hierarchy: " + owner); break; }
            checkType(type);
            Add(type, depth == 0 ? "declared-type-metadata" : "base-type-metadata", depth);
            foreach (var member in type.GetMembers())
            {
                if (member is INamedTypeSymbol || !Visible(member)) continue;
                if (member is IMethodSymbol method &&
                    (method.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove ||
                     (depth > 0 && method.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor))) continue;
                Add(member, depth == 0 ? "declared-member-metadata" : "inherited-member-candidate", depth);
            }
        }
        foreach (var contract in root.AllInterfaces.OrderBy(x => x.ToDisplayString(TypeDisplay), StringComparer.Ordinal))
        {
            checkType(contract);
            Add(contract, "interface-type-metadata", -1);
            foreach (var member in contract.GetMembers())
            {
                if (member is INamedTypeSymbol || !Visible(member)) continue;
                if (member is IMethodSymbol accessor && accessor.MethodKind is
                    MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove) continue;
                var implementation = root.FindImplementationForInterfaceMember(member);
                Add(member, "interface-member-contract", -1,
                    implementation is null ? "implementation=<none>" :
                    "implementation=" + implementation.ToDisplayString(Display) + ";assembly=" + implementation.ContainingAssembly?.Identity.Name);
            }
        }
        return Order(entries);

        void Add(ISymbol symbol, string relation, int baseDepth, string? extra = null)
        {
            var declaration = symbol.GetDocumentationCommentId() ?? symbol.ToDisplayString(Display);
            var details = new List<string>();
            Attributes(symbol.GetAttributes(), "declaration", details);
            switch (symbol)
            {
                case INamedTypeSymbol type:
                    details.Add($"type-kind={type.TypeKind};record={type.IsRecord};readonly={type.IsReadOnly};ref-like={type.IsRefLikeType}");
                    TypeParameters(type.TypeParameters, details);
                    break;
                case IMethodSymbol method:
                    checkType(method.ReturnType);
                    details.Add($"return-ref={method.RefKind};return-nullability={method.ReturnNullableAnnotation};method-kind={method.MethodKind}");
                    Attributes(method.GetReturnTypeAttributes(), "return", details);
                    Parameters(method.Parameters, details);
                    TypeParameters(method.TypeParameters, details);
                    break;
                case IPropertySymbol property:
                    checkType(property.Type);
                    details.Add($"required={property.IsRequired};property-ref={property.RefKind};nullability={property.NullableAnnotation}");
                    Parameters(property.Parameters, details);
                    Accessor(property.GetMethod, "get", details);
                    Accessor(property.SetMethod, "set", details);
                    break;
                case IFieldSymbol field:
                    checkType(field.Type);
                    details.Add($"required={field.IsRequired};volatile={field.IsVolatile};readonly={field.IsReadOnly};nullability={field.NullableAnnotation}");
                    if (field.HasConstantValue) details.Add("constant=" + ScalarText(field.ConstantValue));
                    break;
                case IEventSymbol evt:
                    checkType(evt.Type);
                    details.Add("nullability=" + evt.NullableAnnotation);
                    Accessor(evt.AddMethod, "add", details);
                    Accessor(evt.RemoveMethod, "remove", details);
                    Accessor(evt.RaiseMethod, "raise", details);
                    break;
            }
            if (extra is not null) details.Add(extra);
            var raw = owner + " | " + relation + " | depth=" + baseDepth.ToString(CultureInfo.InvariantCulture) +
                " | " + symbol.ToDisplayString(Display) + " | " + string.Join(" | ", details);
            entries.Add(new(root.ContainingAssembly.Identity.Name, owner, relation, baseDepth,
                symbol.ContainingAssembly?.Identity.Name ?? "", declaration, raw, normalize(raw)));
        }
        void Accessor(IMethodSymbol? accessor, string site, List<string> details)
        {
            if (accessor is null) { details.Add(site + "=<absent>"); return; }
            details.Add($"{site}:accessibility={accessor.DeclaredAccessibility};init-only={accessor.IsInitOnly};abstract={accessor.IsAbstract};virtual={accessor.IsVirtual};override={accessor.IsOverride};sealed={accessor.IsSealed}");
            Attributes(accessor.GetAttributes(), site, details);
            Attributes(accessor.GetReturnTypeAttributes(), site + ":return", details);
            Parameters(accessor.Parameters, details, site + ":");
        }
        void Parameters(ImmutableArray<IParameterSymbol> parameters, List<string> details, string prefix = "")
        {
            foreach (var parameter in parameters)
            {
                checkType(parameter.Type);
                var site = prefix + "parameter:" + parameter.Ordinal.ToString(CultureInfo.InvariantCulture);
                details.Add($"{site}:ref={parameter.RefKind};nullable={parameter.NullableAnnotation};params={parameter.IsParams};optional={parameter.IsOptional}");
                if (parameter.HasExplicitDefaultValue) details.Add(site + ":default=" + ScalarText(parameter.ExplicitDefaultValue));
                Attributes(parameter.GetAttributes(), site, details);
            }
        }
        void TypeParameters(ImmutableArray<ITypeParameterSymbol> parameters, List<string> details)
        {
            foreach (var parameter in parameters)
            {
                var site = "type-parameter:" + parameter.Ordinal.ToString(CultureInfo.InvariantCulture);
                details.Add($"{site}:variance={parameter.Variance};reference={parameter.HasReferenceTypeConstraint};value={parameter.HasValueTypeConstraint};unmanaged={parameter.HasUnmanagedTypeConstraint};notnull={parameter.HasNotNullConstraint};new={parameter.HasConstructorConstraint};nullable={parameter.ReferenceTypeConstraintNullableAnnotation}");
                foreach (var constraint in parameter.ConstraintTypes.OrderBy(x => x.ToDisplayString(TypeDisplay), StringComparer.Ordinal))
                {
                    checkType(constraint);
                    details.Add(site + ":constraint=" + constraint.ToDisplayString(TypeDisplay));
                }
                Attributes(parameter.GetAttributes(), site, details);
            }
        }
        void Attributes(ImmutableArray<AttributeData> attributes, string site, List<string> details)
        {
            foreach (var attribute in attributes.OrderBy(AttributeText, StringComparer.Ordinal))
            {
                checkType(attribute.AttributeClass);
                if (attribute.AttributeConstructor is null)
                    unresolved("Unresolved attribute constructor: " + owner + ":" + site + ":" + attribute.AttributeClass?.ToDisplayString(TypeDisplay));
                foreach (var argument in attribute.ConstructorArguments) CheckConstant(argument);
                foreach (var argument in attribute.NamedArguments) CheckConstant(argument.Value);
                details.Add("attribute:" + site + "=" + AttributeText(attribute));
            }
        }
        void CheckConstant(TypedConstant value)
        {
            checkType(value.Type);
            if (value.Kind == TypedConstantKind.Error) unresolved("Unresolved attribute argument: " + owner);
            else if (value.Kind == TypedConstantKind.Array && !value.IsNull)
                foreach (var child in value.Values) CheckConstant(child);
            else if (value.Kind == TypedConstantKind.Type && value.Value is ITypeSymbol type)
            {
                // Roslyn intentionally represents the omitted arguments in
                // typeof(Generic<>) with error symbols. They are placeholders,
                // not missing assembly dependencies. Check the referenced generic
                // definition while retaining the ORIGINAL unbound typeof in the
                // metadata inventory below. A genuinely missing definition still
                // reaches checkType as an error and remains an audit failure.
                checkType(type is INamedTypeSymbol { IsUnboundGenericType: true } unbound
                    ? unbound.OriginalDefinition : type);
            }
        }
    }

    public static ApiSemanticEntry[] Order(IEnumerable<ApiSemanticEntry> entries) => entries
        .OrderBy(x => x.Assembly, StringComparer.Ordinal).ThenBy(x => x.Owner, StringComparer.Ordinal)
        .ThenBy(x => x.Relation, StringComparer.Ordinal).ThenBy(x => x.BaseDepth)
        .ThenBy(x => x.Declaration, StringComparer.Ordinal).ThenBy(x => x.Raw, StringComparer.Ordinal).ToArray();

    public static ApiSemanticSummary WriteComparison(ApiSemanticEntry[] baseline, ApiSemanticEntry[] target,
        string output, JsonSerializerOptions options)
    {
        var left = baseline.Select(x => x.Normalized).ToHashSet(StringComparer.Ordinal);
        var right = target.Select(x => x.Normalized).ToHashSet(StringComparer.Ordinal);
        var missing = left.Except(right).Order(StringComparer.Ordinal).ToArray();
        var additional = right.Except(left).Order(StringComparer.Ordinal).ToArray();
        var result = new ApiSemanticSummary(left.Count, right.Count, left.Intersect(right).Count(), missing.Length, additional.Length);
        var directory = Path.Combine(output, "supplemental-metadata");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "avalonia.json"), JsonSerializer.Serialize(baseline, options) + "\n");
        File.WriteAllText(Path.Combine(directory, "uno.json"), JsonSerializer.Serialize(target, options) + "\n");
        File.WriteAllLines(Path.Combine(directory, "missing-or-different.txt"), missing);
        File.WriteAllLines(Path.Combine(directory, "additional-or-different.txt"), additional);
        File.WriteAllText(Path.Combine(directory, "summary.json"), JsonSerializer.Serialize(new
        {
            counts = result,
            metadataOnly = true,
            rawDeclaredDifferencesRemoved = 0,
            inheritedMembersAreLookupCandidatesNotBindingProof = true,
            nonFiniteAndNegativeZeroConstants = "Type-tagged IEEE bits, never discarded or coerced to null",
            limitations = new[]
            {
                "No method body or native framework code is executed.",
                "Inherited declarations retain their actual declaring types and constructed generic arguments; hiding/overload applicability still needs consumer compilation.",
                "Attributes are declared metadata at their original sites, not reflection-instantiated attributes or AttributeUsage inheritance resolution.",
                "Assembly/module attributes, custom modifiers, explicit interface accessor bodies, dependency-property defaults and native layout/input behavior remain separate acceptance surfaces."
            }
        }, options) + "\n");
        return result;
    }

    internal static string ScalarText(object? value)
    {
        // JSON numbers cannot encode infinities/NaN. Preserve the actual metadata
        // bits, including signed zero, rather than omit an attribute or conflate
        // a floating-point sentinel with an ordinary string or null constant.
        if (value is double d)
        {
            var bits = BitConverter.DoubleToInt64Bits(d);
            if (!double.IsFinite(d) || d == 0 && bits < 0)
                return "ieee754:System.Double:0x" + unchecked((ulong)bits).ToString("x16", CultureInfo.InvariantCulture);
        }
        if (value is float f)
        {
            var bits = BitConverter.SingleToInt32Bits(f);
            if (!float.IsFinite(f) || f == 0 && bits < 0)
                return "ieee754:System.Single:0x" + unchecked((uint)bits).ToString("x8", CultureInfo.InvariantCulture);
        }
        return JsonSerializer.Serialize(value);
    }

    private static bool Visible(ISymbol symbol) => symbol.DeclaredAccessibility is
        Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;
    private static string AttributeText(AttributeData attribute) =>
        (attribute.AttributeClass?.ToDisplayString(TypeDisplay) ?? "<missing>") + "(" +
        string.Join(",", attribute.ConstructorArguments.Select(ConstantText)) + "){" +
        string.Join(",", attribute.NamedArguments.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => x.Key + "=" + ConstantText(x.Value))) + "}";
    private static string ConstantText(TypedConstant value)
    {
        var type = value.Type?.ToDisplayString(TypeDisplay) ?? "<missing>";
        if (value.IsNull) return type + ":null";
        return value.Kind switch
        {
            TypedConstantKind.Array => type + ":[" + string.Join(",", value.Values.Select(ConstantText)) + "]",
            TypedConstantKind.Type => type + ":typeof(" + ((ITypeSymbol)value.Value!).ToDisplayString(TypeDisplay) + ")",
            TypedConstantKind.Error => type + ":<error>",
            _ => type + ":" + ScalarText(value.Value),
        };
    }
}
