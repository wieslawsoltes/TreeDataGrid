using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TreeDataGrid.Tools.ApiAudit;

// Read compiled metadata; never load/execute application assemblies or access
// static properties. Namespace normalization is explicit and recorded. This
// inventory is deliberately not a behavioral, ABI or performance-parity claim.
if (!(args.Length == 1 && args[0] == "--self-test") && args.Length is < 4 or > 5)
{
    Console.Error.WriteLine("Usage: TreeDataGrid.ApiAudit <Avalonia.dll> <Uno.dll> <Core.dll> <output-directory> [--strict], or --self-test");
    return 2;
}
try
{
    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    // The actual metadata reader is regression-tested before either normal
    // comparison or strict self-comparison can produce a successful report.
    var semanticChecks = ApiSemanticChecks.Run();
    if (args.Length == 1) return 0;
    var baselinePath = Path.GetFullPath(args[0]);
    var targetPath = Path.GetFullPath(args[1]);
    var corePath = Path.GetFullPath(args[2]);
    var output = Path.GetFullPath(args[3]);
    var strict = args.Length == 5 && args[4] == "--strict";
    if (args.Length == 5 && !strict) throw new ArgumentException("The only optional argument is --strict.");
    Directory.CreateDirectory(output);
    var baseline = Surface.Read([baselinePath, corePath], Environment.GetEnvironmentVariable("TREEDATAGRID_API_BASELINE_REFERENCES"));
    var target = Surface.Read([targetPath, corePath], Environment.GetEnvironmentVariable("TREEDATAGRID_API_TARGET_REFERENCES"));
    var left = baseline.Entries.Select(entry => entry.Normalized).ToHashSet(StringComparer.Ordinal);
    var right = target.Entries.Select(entry => entry.Normalized).ToHashSet(StringComparer.Ordinal);
    var missing = left.Except(right).Order(StringComparer.Ordinal).ToArray();
    var additional = right.Except(left).Order(StringComparer.Ordinal).ToArray();
    var matched = left.Intersect(right).Count();
    var classified = ApiDifferences.Classify(baseline.Entries, target.Entries);
    if (classified.Length != missing.Length) throw new InvalidDataException("API classification lost raw differences.");
    var categories = new SortedDictionary<string, int>(StringComparer.Ordinal);
    foreach (var group in classified.GroupBy(item => item.Category)) categories.Add(group.Key, group.Count());
    var absentTypes = classified.Where(item => item.Category == "exported-type-not-found-at-identity")
        .Select(item => item.Baseline.Identity).Order(StringComparer.Ordinal).ToArray();
    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(Path.Combine(output, "avalonia.json"), JsonSerializer.Serialize(baseline, jsonOptions) + "\n");
    File.WriteAllText(Path.Combine(output, "uno.json"), JsonSerializer.Serialize(target, jsonOptions) + "\n");
    File.WriteAllLines(Path.Combine(output, "missing-or-different.txt"), missing);
    File.WriteAllLines(Path.Combine(output, "additional-or-different.txt"), additional);
    File.WriteAllText(Path.Combine(output, "classified-differences.json"), JsonSerializer.Serialize(classified, jsonOptions) + "\n");
    File.WriteAllLines(Path.Combine(output, "absent-exported-types.txt"), absentTypes);
    var semantics = ApiSemantics.WriteComparison(baseline.SemanticEntries, target.SemanticEntries, output, jsonOptions);
    var report = new
    {
        schemaVersion = 3,
        mode = "declared-public-and-protected-metadata-shapes",
        namespaceMappings = Surface.NamespaceMappings,
        baselineShapes = left.Count,
        targetShapes = right.Count,
        exactNormalizedMatches = matched,
        missingOrDifferent = missing.Length,
        additionalOrDifferent = additional.Length,
        differenceCategories = categories,
        absentExportedTypeIdentities = absentTypes,
        unresolvedBaselineTypes = baseline.UnresolvedTypes,
        unresolvedTargetTypes = target.UnresolvedTypes,
        supplementalMetadata = semantics,
        supplementalMetadataChecks = semanticChecks,
        completeApiParityProven = false,
        limitations = new[]
        {
            "Native framework types, relocated Core APIs and inheritance require explicit compatibility review.",
            "Documentation identities classify differences; candidate matches do not establish equivalence or remove raw differences.",
            "Matching declarations do not validate method bodies, event ordering, native input or timing.",
            "Inherited candidates and declared custom attributes are recorded separately; C# lookup applicability and AttributeUsage inheritance are not automatically inferred.",
            "Assembly/module attributes, custom modifiers and native dependency-property defaults still require separate review.",
            "Unresolved metadata dependencies are reported; they are never silently treated as matching contracts."
        }
    };
    File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(report, jsonOptions) + "\n");
    Console.WriteLine("UNO_API_AUDIT=" + JsonSerializer.Serialize(new
    {
        report.baselineShapes, report.targetShapes, report.exactNormalizedMatches,
        report.missingOrDifferent, report.additionalOrDifferent,
        unresolvedBaselineTypes = baseline.UnresolvedTypes.Length,
        unresolvedTargetTypes = target.UnresolvedTypes.Length,
        report.completeApiParityProven,
    }));
    Console.WriteLine("UNO_API_SUPPLEMENTAL_METADATA=" + JsonSerializer.Serialize(semantics));
    Console.WriteLine("UNO_API_DIFFERENCE_CATEGORIES=" + JsonSerializer.Serialize(categories));
    Console.WriteLine("UNO_API_ABSENT_EXPORTED_TYPES=" + JsonSerializer.Serialize(absentTypes));
    return strict && (missing.Length > 0 || semantics.MissingOrDifferent > 0 ||
        baseline.UnresolvedTypes.Length > 0 || target.UnresolvedTypes.Length > 0) ? 1 : 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 2;
}

internal sealed record InputAssembly(string Name, string Sha256);
internal sealed record Surface(InputAssembly[] Inputs, ApiEntry[] Entries, string[] UnresolvedTypes)
{
    // Keep the established declared inventories lean and unchanged in shape.
    // Supplemental rows have their own complete, independently diffable files.
    [JsonIgnore]
    public ApiSemanticEntry[] SemanticEntries { get; init; } = [];
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

    public static Surface Read(string[] inputs, string? referenceDirectories)
    {
        foreach (var input in inputs)
            if (!File.Exists(input)) throw new FileNotFoundException("API input assembly is missing", input);
        var references = new Dictionary<string, PortableExecutableReference>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in inputs) AddReference(input);
        foreach (var directory in (referenceDirectories ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
            foreach (var file in Directory.EnumerateFiles(directory, "*.dll").Order(StringComparer.Ordinal)) AddReference(file);
        }
        foreach (var directory in inputs.Select(Path.GetDirectoryName).Distinct())
            foreach (var file in Directory.EnumerateFiles(directory!, "*.dll").Order(StringComparer.Ordinal)) AddReference(file);
        foreach (var file in ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            AddReference(file);
        var compilation = CSharpCompilation.Create("TreeDataGrid_Metadata_Audit", references: references.Values,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All));
        var entries = new List<ApiEntry>();
        var semanticEntries = new List<ApiSemanticEntry>();
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
            entries.OrderBy(value => value.Assembly, StringComparer.Ordinal).ThenBy(value => value.Raw, StringComparer.Ordinal).ToArray(), unresolved.ToArray())
        { SemanticEntries = ApiSemantics.Order(semanticEntries) };

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
            semanticEntries.AddRange(ApiSemantics.Read(type, Normalize, CheckType, value => unresolved.Add("metadata: " + value)));
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
            var identity = symbol.GetDocumentationCommentId()
                ?? throw new InvalidDataException($"No metadata documentation identity for {symbol.Kind}: {text}");
            var owner = (symbol as INamedTypeSymbol ?? symbol.ContainingType)?.GetDocumentationCommentId()
                ?? throw new InvalidDataException($"No declaring type for {identity}");
            entries.Add(new(assembly, symbol.Kind.ToString(), text, Normalize(text),
                Normalize(identity), Normalize(owner), symbol.MetadataName));
        }
        static string Normalize(string value)
        {
            foreach (var map in NamespaceMappings) value = value.Replace(map.Key + ".", map.Value + ".", StringComparison.Ordinal);
            return value;
        }
        void CheckType(ITypeSymbol? type)
        {
            if (type is null) return;
            if (type.TypeKind == TypeKind.Error) unresolved.Add(type.ToDisplayString(Format));
            if (type is INamedTypeSymbol named)
                foreach (var argument in named.TypeArguments) CheckType(argument);
            if (type is IArrayTypeSymbol array) CheckType(array.ElementType);
            if (type is IPointerTypeSymbol pointer) CheckType(pointer.PointedAtType);
            if (type is IFunctionPointerTypeSymbol function)
            {
                CheckType(function.Signature.ReturnType);
                foreach (var parameter in function.Signature.Parameters) CheckType(parameter.Type);
            }
        }
    }

    private static bool Visible(ISymbol symbol) => symbol.DeclaredAccessibility is
        Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;
}
