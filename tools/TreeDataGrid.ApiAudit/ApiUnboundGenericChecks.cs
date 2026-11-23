using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TreeDataGrid.Tools.ApiAudit;

/// <summary>Open generic metadata must resolve without waiving actual missing types.</summary>
internal static class ApiUnboundGenericChecks
{
    public static int Run()
    {
        var runtime = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var dependency = Compile("AuditGenericDependency", """
            namespace External
            {
                public class Generic<T> { }
                public class T { }
            }
            """, runtime);
        var fixture = Compile("AuditGenericConsumer", """
            #nullable enable
            using System;
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
            public sealed class TypeReferenceAttribute(Type type) : Attribute
            {
                public Type[]? Types { get; set; }
            }
            public class Generic<T> { }
            public class Generic<TFirst, TSecond> { }
            public class Outer<T> { public class Inner<TValue> { } }
            [TypeReference(typeof(Generic<>), Types = new[] {
                typeof(Generic<,>), typeof(Outer<>.Inner<>),
                typeof(Generic<string>), typeof(string[]) })]
            public class OpenHost { }
            [TypeReference(typeof(External.Generic<>))]
            public class ExternalOpenHost { }
            [TypeReference(typeof(External.Generic<string>))]
            public class ExternalClosedHost { }
            [TypeReference(typeof(External.T))]
            public class ExternalNamedTHost { }
            """, runtime.Append(dependency));
        var resolved = ReadAssembly(runtime.Append(dependency).Append(fixture), fixture);
        var open = resolved.GetTypeByMetadataName("OpenHost")!;
        var argument = (INamedTypeSymbol)open.GetAttributes().Single().ConstructorArguments[0].Value!;
        var checks = 0;
        Check(argument.IsUnboundGenericType && argument.TypeArguments.Any(x => x.TypeKind == TypeKind.Error),
            "fixture reproduces Roslyn's unbound placeholder representation");
        var errors = new List<string>();
        var entries = Inventory(open, errors);
        Check(errors.Count == 0, "open generic attribute dependencies resolve");
        var raw = entries.Single(x => x.Relation == "declared-type-metadata").Raw;
        Check(raw.Contains("typeof(Generic<>)", StringComparison.Ordinal), "unbound constructor argument remains unbound in inventory");
        Check(raw.Contains("typeof(Generic<,>)", StringComparison.Ordinal), "multiple omitted type arguments are preserved");
        Check(raw.Contains("typeof(Outer<>.Inner<>)", StringComparison.Ordinal), "nested unbound generic is preserved");
        Check(raw.Contains("typeof(Generic<System.String>)", StringComparison.Ordinal) &&
              raw.Contains("typeof(System.String[])", StringComparison.Ordinal), "constructed and array type arguments are preserved");
        Check(entries.SequenceEqual(Inventory(open, errors)) && errors.Count == 0, "open generic inventory is deterministic");
        foreach (var name in new[] { "ExternalOpenHost", "ExternalClosedHost", "ExternalNamedTHost" })
        {
            errors.Clear();
            Inventory(resolved.GetTypeByMetadataName(name)!, errors);
            Check(errors.Count == 0, "referenced dependency resolves: " + name);
        }
        // Re-read the same emitted assembly without its referenced dependency.
        // This tests real missing metadata, not fabricated error symbols or a
        // name-based exception for the placeholder spelling 'T'.
        var missing = ReadAssembly(runtime.Append(fixture), fixture);
        foreach (var name in new[] { "ExternalOpenHost", "ExternalClosedHost", "ExternalNamedTHost" })
        {
            errors.Clear();
            Inventory(missing.GetTypeByMetadataName(name)!, errors);
            Check(errors.Count > 0, "missing dependency still fails: " + name);
        }
        return checks;

        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("API generic metadata regression: " + name);
            ++checks;
            Console.WriteLine("UNO_API_SEMANTIC_CASE_PASSED=" + name);
        }
    }

    private static ApiSemanticEntry[] Inventory(INamedTypeSymbol root, List<string> errors)
    {
        return ApiSemantics.Read(root, static text => text, Check, errors.Add);
        void Check(ITypeSymbol? type)
        {
            if (type?.TypeKind == TypeKind.Error) errors.Add(type.ToDisplayString());
            if (type is INamedTypeSymbol named)
                foreach (var argument in named.TypeArguments) Check(argument);
            if (type is IArrayTypeSymbol array) Check(array.ElementType);
        }
    }

    private static IAssemblySymbol ReadAssembly(IEnumerable<MetadataReference> references, MetadataReference fixture)
    {
        var compilation = CSharpCompilation.Create("AuditGenericReader", references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All));
        return (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(fixture)!;
    }

    private static PortableExecutableReference Compile(string name, string source, IEnumerable<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        if (!emitted.Success) throw new InvalidOperationException(string.Join("\n", emitted.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
