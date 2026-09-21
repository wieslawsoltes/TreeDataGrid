using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Read compiled metadata; never load/execute application assemblies or access
// static properties. Namespace normalization is explicit and recorded. This
// inventory is deliberately not a behavioral, ABI or performance-parity claim.
if (args.Length is < 4 or > 5)
{
    Console.Error.WriteLine("Usage: TreeDataGrid.ApiAudit <Avalonia.dll> <Uno.dll> <Core.dll> <output-directory> [--strict]");
    return 2;
}
try
{
    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    var baselinePath = Path.GetFullPath(args[0]);
    var targetPath = Path.GetFullPath(args[1]);
    var corePath = Path.GetFullPath(args[2]);
    var output = Path.GetFullPath(args[3]);
    var strict = args.Length == 5 && args[4] == "--strict";
    if (args.Length == 5 && !strict) throw new ArgumentException("The only optional argument is --strict.");
    Directory.CreateDirectory(output);
    var baseline = Surface.Read([baselinePath, corePath]);
    var target = Surface.Read([targetPath, corePath]);
    var left = baseline.Entries.Select(entry => entry.Normalized).ToHashSet(StringComparer.Ordinal);
    var right = target.Entries.Select(entry => entry.Normalized).ToHashSet(StringComparer.Ordinal);
    var missing = left.Except(right).Order(StringComparer.Ordinal).ToArray();
    var additional = right.Except(left).Order(StringComparer.Ordinal).ToArray();
    var matched = left.Intersect(right).Count();
    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(Path.Combine(output, "avalonia.json"), JsonSerializer.Serialize(baseline, jsonOptions) + "\n");
    File.WriteAllText(Path.Combine(output, "uno.json"), JsonSerializer.Serialize(target, jsonOptions) + "\n");
    File.WriteAllLines(Path.Combine(output, "missing-or-different.txt"), missing);
    File.WriteAllLines(Path.Combine(output, "additional-or-different.txt"), additional);
    var report = new
    {
        schemaVersion = 1,
        mode = "declared-public-and-protected-metadata-shapes",
        namespaceMappings = Surface.NamespaceMappings,
        baselineShapes = left.Count,
        targetShapes = right.Count,
        exactNormalizedMatches = matched,
        missingOrDifferent = missing.Length,
        additionalOrDifferent = additional.Length,
        unresolvedBaselineTypes = baseline.UnresolvedTypes,
        unresolvedTargetTypes = target.UnresolvedTypes,
        completeApiParityProven = false,
        limitations = new[]
        {
            "Native framework property/event types, relocated Core APIs and inheritance require explicit classification.",
            "Matching declarations do not validate method bodies, event ordering, native input or timing.",
            "Inherited external framework members and custom attributes are not included in this declared-member inventory.",
            "Unresolved metadata dependencies are reported; they are never silently treated as matching contracts."
        }
    };
    File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(report, jsonOptions) + "\n");
    Console.WriteLine("UNO_API_AUDIT=" + JsonSerializer.Serialize(report));
    return strict && (missing.Length > 0 || baseline.UnresolvedTypes.Length > 0 || target.UnresolvedTypes.Length > 0) ? 1 : 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 2;
}

internal sealed record ApiEntry(string Assembly, string Kind, string Raw, string Normalized);
internal sealed record InputAssembly(string Name, string Sha256);
internal sealed record Surface(InputAssembly[] Inputs, ApiEntry[] Entries, string[] UnresolvedTypes)
{
    public static IReadOnlyDictionary<string, string> NamespaceMappings { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal)
    {
        ["Avalonia.Controls"] = "UI.Controls",
        ["Uno.Controls"] = "UI.Controls",
    };
    private static readonly SymbolDisplayFormat Format = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints | SymbolDisplayGenericsOptions.IncludeVariance,
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

    public static Surface Read(string[] inputs)
    {
        foreach (var input in inputs)
            if (!File.Exists(input)) throw new FileNotFoundException("API input assembly is missing", input);
        var references = new Dictionary<string, PortableExecutableReference>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in inputs) AddReference(input);
        foreach (var directory in inputs.Select(Path.GetDirectoryName).Distinct())
            foreach (var file in Directory.EnumerateFiles(directory!, "*.dll").Order(StringComparer.Ordinal)) AddReference(file);
        foreach (var file in ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            AddReference(file);
        var compilation = CSharpCompilation.Create("TreeDataGrid_Metadata_Audit", references: references.Values,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All));
        var entries = new List<ApiEntry>();
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        var fingerprints = new List<InputAssembly>();
        foreach (var input in inputs)
        {
            var symbol = compilation.GetAssemblyOrModuleSymbol(references[Path.GetFileName(input)]) as IAssemblySymbol
                ?? throw new InvalidDataException($"Cannot read managed assembly {input}");
            fingerprints.Add(new(symbol.Identity.Name, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(input)))));
            VisitNamespace(symbol.GlobalNamespace, symbol.Identity.Name);
        }
        return new(fingerprints.OrderBy(value => value.Name, StringComparer.Ordinal).ToArray(),
            entries.OrderBy(value => value.Assembly, StringComparer.Ordinal).ThenBy(value => value.Raw, StringComparer.Ordinal).ToArray(), unresolved.ToArray());

        void AddReference(string file)
        {
            if (references.ContainsKey(Path.GetFileName(file))) return;
            using var stream = File.OpenRead(file);
            using var pe = new PEReader(stream);
            if (pe.HasMetadata) references.Add(Path.GetFileName(file), MetadataReference.CreateFromFile(file));
        }
        void VisitNamespace(INamespaceSymbol ns, string assembly)
        {
            foreach (var child in ns.GetNamespaceMembers()) VisitNamespace(child, assembly);
            foreach (var type in ns.GetTypeMembers()) VisitType(type, assembly);
        }
        void VisitType(INamedTypeSymbol type, string assembly)
        {
            if (!Visible(type)) return;
            Add(type, assembly);
            CheckType(type.BaseType);
            foreach (var contract in type.Interfaces) CheckType(contract);
            foreach (var member in type.GetMembers())
            {
                if (member is INamedTypeSymbol nested) { VisitType(nested, assembly); continue; }
                if (!Visible(member)) continue;
                if (member is IMethodSymbol accessor && accessor.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove) continue;
                Add(member, assembly);
                switch (member)
                {
                    case IMethodSymbol method:
                        CheckType(method.ReturnType);
                        foreach (var parameter in method.Parameters) CheckType(parameter.Type);
                        break;
                    case IPropertySymbol property:
                        CheckType(property.Type);
                        foreach (var parameter in property.Parameters) CheckType(parameter.Type);
                        break;
                    case IFieldSymbol field: CheckType(field.Type); break;
                    case IEventSymbol evt: CheckType(evt.Type); break;
                }
            }
        }
        void Add(ISymbol symbol, string assembly)
        {
            var text = symbol.ToDisplayString(Format);
            if (symbol is INamedTypeSymbol type)
            {
                var bases = new List<string>();
                if (type.BaseType is { } parent) bases.Add(parent.ToDisplayString(Format));
                bases.AddRange(type.Interfaces.Select(contract => contract.ToDisplayString(Format)).Order(StringComparer.Ordinal));
                text += " | bases=" + string.Join(",", bases);
            }
            if (symbol is IFieldSymbol { HasConstantValue: true } field)
                text += " | constant=" + JsonSerializer.Serialize(field.ConstantValue);
            var normalized = text;
            foreach (var map in NamespaceMappings) normalized = normalized.Replace(map.Key + ".", map.Value + ".", StringComparison.Ordinal);
            entries.Add(new(assembly, symbol.Kind.ToString(), text, normalized));
        }
        void CheckType(ITypeSymbol? type)
        {
            if (type is null) return;
            if (type.TypeKind == TypeKind.Error) unresolved.Add(type.ToDisplayString(Format));
            if (type is INamedTypeSymbol named)
                foreach (var argument in named.TypeArguments) CheckType(argument);
            if (type is IArrayTypeSymbol array) CheckType(array.ElementType);
            if (type is IPointerTypeSymbol pointer) CheckType(pointer.PointedAtType);
        }
    }

    private static bool Visible(ISymbol symbol) => symbol.DeclaredAccessibility is
        Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;
}
