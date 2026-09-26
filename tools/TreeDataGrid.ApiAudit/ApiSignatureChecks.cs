using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TreeDataGrid.Tools.ApiAudit;

/// <summary>Read real PE signatures that C# syntax cannot faithfully express.</summary>
internal static class ApiSignatureChecks
{
    internal static int Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdg-api-signature-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var checks = 0;
        try
        {
            foreach (var site in new[] { "return", "return-ref", "parameter", "parameter-ref", "field", "array", "pointer", "function-return", "function-parameter", "generic-argument", "containing-argument", "nested-argument" })
            {
                var first = Read(site, [1]);
                var second = Read(site, [2]);
                Check(ApiDifferences.Classify(first.Entries, second.Entries).Length == 0,
                    site + ": identical declared C# surface does not expose optional modifier identity");
                Check(!EqualSemantics(first, second), site + ": supplemental signature distinguishes modifier type");
                Check(first.SemanticEntries.Any(x => x.Raw.Contains("modopt(SignatureFixtures.Marker1)", StringComparison.Ordinal)),
                    site + ": actual modifier is recorded at its signature site");
                Check(first.UnresolvedTypes.Length == 0, site + ": modifier dependency resolves");
            }
            var ordered = Read("parameter", [1, 2, 1]);
            var reversed = Read("parameter", [2, 1, 1]);
            var deduplicated = Read("parameter", [1, 2]);
            Check(!EqualSemantics(ordered, reversed), "custom modifier order is preserved");
            Check(!EqualSemantics(ordered, deduplicated), "duplicate custom modifiers are preserved");
            Check(!EqualSemantics(Read("parameter", [1]), Read("parameter", [1], required: true)), "modreq and modopt are distinct");
            Check(EqualSemantics(Read("parameter", [1]), Read("parameter", [1])), "signature reads are deterministic");
            Check(!EqualSemantics(Read("return", [1]), Read("return-ref", [1])), "return-type and by-reference sites remain distinct");
            Check(!EqualSemantics(Read("function-return", [1], convention: 1), Read("function-return", [1], convention: 2)),
                "function-pointer cdecl and stdcall are distinct");

            var dependency = Emit("parameter", [1], missingModifier: true);
            var unresolved = Surface.Read([dependency], null);
            Check(unresolved.UnresolvedTypes.Any(x => x.Contains("ExternalMarker", StringComparison.Ordinal)),
                "an unresolved modifier type cannot silently count as a resolved signature");

            var csharp = EmitCSharp();
            var surface = Surface.Read([csharp], null);
            Check(surface.SemanticEntries.Any(x => x.Raw.Contains("custom-modifier:set:signature:return:type", StringComparison.Ordinal) &&
                x.Raw.Contains("IsExternalInit", StringComparison.Ordinal)), "init accessor return modifier is recorded");
            Check(surface.SemanticEntries.Any(x => x.Raw.Contains("custom-modifier:field:type", StringComparison.Ordinal) &&
                x.Raw.Contains("IsVolatile", StringComparison.Ordinal)), "volatile field modifier is recorded");
            Check(surface.SemanticEntries.Any(x => x.Raw.Contains("custom-modifier:signature:return:ref", StringComparison.Ordinal) &&
                x.Raw.Contains("InAttribute", StringComparison.Ordinal)), "readonly by-reference return modifier is recorded");
            Check(surface.SemanticEntries.Any(x => x.Raw.Contains("unmanaged-convention:", StringComparison.Ordinal) &&
                x.Raw.Contains("CallConvSuppressGCTransition", StringComparison.Ordinal)), "function pointer unmanaged convention modifiers are recorded");
            Check(surface.SemanticEntries.Any(x => x.Relation == "inherited-member-candidate" &&
                x.Raw.Contains("custom-modifier:signature:return:ref", StringComparison.Ordinal)), "inherited constructed signatures include modifiers");
            Check(surface.SemanticEntries.Any(x => x.Relation == "interface-member-contract" &&
                x.Raw.Contains("custom-modifier:", StringComparison.Ordinal)), "interface contracts include signature modifiers");
            Check(surface.UnresolvedTypes.Length == 0, "C# compiler-emitted signatures resolve");
            Console.WriteLine("UNO_API_SIGNATURE_CHECKS_PASSED=" + checks + "; production Surface.Read; raw PE and C# fixtures; no fixture methods executed");
            return checks;
        }
        finally { Directory.Delete(directory, recursive: true); }

        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("API signature regression: " + message);
            ++checks;
            Console.WriteLine("UNO_API_SIGNATURE_CASE_PASSED=" + message);
        }
        Surface Read(string site, int[] modifiers, bool required = false, byte convention = 1) =>
            Surface.Read([Emit(site, modifiers, required, convention)], null);
        static bool EqualSemantics(Surface first, Surface second) =>
            first.SemanticEntries.Select(x => x.Normalized).SequenceEqual(second.SemanticEntries.Select(x => x.Normalized));

        string EmitCSharp()
        {
            var syntax = CSharpSyntaxTree.ParseText("""
                public interface IRead { ref readonly int Read(); }
                public class Parent<T> : IRead
                {
                    protected int value;
                    public ref readonly int Read() => ref value;
                    public int Number { get; init; }
                    public volatile int Volatile;
                }
                public unsafe class Child : Parent<string>
                {
                    public delegate* unmanaged[Cdecl, SuppressGCTransition]<int, int> Callback;
                    public delegate*<int, int>[] Callbacks;
                }
                """);
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator).Select(x => MetadataReference.CreateFromFile(x));
            var compilation = CSharpCompilation.Create("CompilerSignatureFixture", [syntax], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            var path = Path.Combine(directory, "CompilerSignatureFixture.dll");
            using var stream = File.Create(path);
            var result = compilation.Emit(stream);
            if (!result.Success) throw new InvalidDataException(string.Join("\n", result.Diagnostics));
            return path;
        }

        string Emit(string site, int[] modifiers, bool required = false, byte convention = 1, bool missingModifier = false)
        {
            var metadata = new MetadataBuilder();
            metadata.AddModule(0, metadata.GetOrAddString("SignatureFixture.dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
            metadata.AddAssembly(metadata.GetOrAddString("SignatureFixture"), new Version(1, 0), default, default,
                (AssemblyFlags)0, AssemblyHashAlgorithm.None);
            var core = typeof(object).Assembly.GetName();
            var coreRef = metadata.AddAssemblyReference(metadata.GetOrAddString(core.Name!), core.Version!, default,
                metadata.GetOrAddBlob(core.GetPublicKeyToken()!), (AssemblyFlags)0, default);
            var objectRef = metadata.AddTypeReference(coreRef, metadata.GetOrAddString("System"), metadata.GetOrAddString("Object"));
            var fields = MetadataTokens.FieldDefinitionHandle(1);
            var methods = MetadataTokens.MethodDefinitionHandle(1);
            metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default, fields, methods);
            var marker1 = metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("SignatureFixtures"),
                metadata.GetOrAddString("Marker1"), objectRef, fields, methods);
            var marker2 = metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("SignatureFixtures"),
                metadata.GetOrAddString("Marker2"), objectRef, fields, methods);
            var generic = metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("SignatureFixtures"),
                metadata.GetOrAddString("Generic`1"), objectRef, fields, methods);
            metadata.AddGenericParameter(generic, GenericParameterAttributes.None, metadata.GetOrAddString("T"), 0);
            var nested = metadata.AddTypeDefinition(TypeAttributes.NestedPublic, default,
                metadata.GetOrAddString("Inner`1"), objectRef, fields, methods);
            metadata.AddNestedType(nested, generic);
            metadata.AddGenericParameter(nested, GenericParameterAttributes.None, metadata.GetOrAddString("T"), 0);
            metadata.AddGenericParameter(nested, GenericParameterAttributes.None, metadata.GetOrAddString("U"), 1);
            metadata.AddTypeDefinition(TypeAttributes.Public | TypeAttributes.Abstract, metadata.GetOrAddString("SignatureFixtures"),
                metadata.GetOrAddString("Holder"), objectRef, fields, methods);
            EntityHandle external = default;
            if (missingModifier)
            {
                var missing = metadata.AddAssemblyReference(metadata.GetOrAddString("Missing.Modifier.Dependency"), new Version(1, 0),
                    default, default, (AssemblyFlags)0, default);
                external = metadata.AddTypeReference(missing, metadata.GetOrAddString("SignatureFixtures"), metadata.GetOrAddString("ExternalMarker"));
            }
            var signature = new BlobBuilder();
            var genericArgument = site.EndsWith("-argument", StringComparison.Ordinal);
            var field = genericArgument || site is "field" or "array" or "pointer" or "function-return" or "function-parameter";
            if (field)
            {
                signature.WriteByte(0x06); // FIELD
                if (genericArgument)
                {
                    signature.WriteByte(0x15); // GENERICINST
                    signature.WriteByte(0x12); // CLASS
                    signature.WriteCompressedInteger(CodedIndex.TypeDefOrRef(site == "generic-argument" ? generic : nested));
                    signature.WriteCompressedInteger(site == "generic-argument" ? 1 : 2);
                    if (site is "generic-argument" or "containing-argument") WriteModifiers();
                    if (site != "generic-argument")
                    {
                        signature.WriteByte(0x08); // enclosing argument
                        if (site == "nested-argument") WriteModifiers();
                    }
                }
                else if (site.StartsWith("function-", StringComparison.Ordinal))
                {
                    signature.WriteByte(0x1b); // FNPTR
                    signature.WriteByte(convention);
                    signature.WriteCompressedInteger(1);
                    if (site == "function-return") WriteModifiers();
                    signature.WriteByte(0x08); // I4 return
                    if (site == "function-parameter") WriteModifiers();
                }
                else if (site == "array") signature.WriteByte(0x1d); // SZARRAY
                else if (site == "pointer") signature.WriteByte(0x0f); // PTR
                if (!genericArgument && !site.StartsWith("function-", StringComparison.Ordinal)) WriteModifiers();
                signature.WriteByte(0x08);
                metadata.AddFieldDefinition(FieldAttributes.Public, metadata.GetOrAddString("Value"), metadata.GetOrAddBlob(signature));
            }
            else
            {
                signature.WriteByte(0x20); // DEFAULT | HASTHIS
                signature.WriteCompressedInteger(1);
                if (site.StartsWith("return", StringComparison.Ordinal)) WriteModifiers();
                if (site == "return-ref") signature.WriteByte(0x10); // BYREF
                signature.WriteByte(0x08);
                if (site.StartsWith("parameter", StringComparison.Ordinal)) WriteModifiers();
                if (site == "parameter-ref") signature.WriteByte(0x10);
                signature.WriteByte(0x08);
                var parameter = metadata.AddParameter(ParameterAttributes.None, metadata.GetOrAddString("value"), 1);
                metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual |
                    MethodAttributes.NewSlot | MethodAttributes.HideBySig, MethodImplAttributes.IL, metadata.GetOrAddString("Read"),
                    metadata.GetOrAddBlob(signature), 0, parameter);
            }
            var image = new BlobBuilder();
            new ManagedPEBuilder(new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll), new MetadataRootBuilder(metadata),
                new BlobBuilder(), flags: CorFlags.ILOnly).Serialize(image);
            var fixtureDirectory = Path.Combine(directory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixtureDirectory);
            var path = Path.Combine(fixtureDirectory, "SignatureFixture.dll");
            using (var stream = File.Create(path)) image.WriteContentTo(stream);
            return path;

            void WriteModifiers()
            {
                foreach (var index in modifiers)
                {
                    signature.WriteByte(required ? (byte)0x1f : (byte)0x20);
                    signature.WriteCompressedInteger(CodedIndex.TypeDefOrRef(missingModifier ? external : index == 1 ? marker1 : marker2));
                }
            }
        }
    }
}
