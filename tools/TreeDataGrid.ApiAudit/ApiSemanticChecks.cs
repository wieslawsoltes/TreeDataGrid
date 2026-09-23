using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TreeDataGrid.Tools.ApiAudit;

/// <summary>Compiled-metadata regression cases for the supplemental inventory.</summary>
internal static class ApiSemanticChecks
{
    public static int Run()
    {
        const string source = """
            #nullable enable
            using System;
            [AttributeUsage(AttributeTargets.All, AllowMultiple=true)]
            public sealed class MarkerAttribute : Attribute
            {
                public MarkerAttribute(string name, Type type, int[] values) { }
                public string? Note { get; set; }
            }
            public interface IContract<T> { T Convert(T value); }
            public class Parent<T>
            {
                public Parent() { }
                public Parent(int number) { }
                public T? Value { get; protected set; }
                public void Overload(int value) { }
                public void Overload(string value) { }
                protected virtual T Echo(T value) => value;
                private void Hidden() { }
            }
            [Marker("root", typeof(string), new[]{1,2}, Note="named")]
            public class Child : Parent<string>, IContract<int>
            {
                public required string Required { get; init; }
                public string? Optional { get; private set; }
                public const int Number = 17;
                // Metadata inspection MUST NOT execute this initializer.
                static Child() => throw new InvalidOperationException("DO NOT EXECUTE");
                int IContract<int>.Convert(int value) => value;
                [return: Marker("result", typeof(long), new[]{3})]
                public string? Read([Marker("argument", typeof(int), new[]{4})] int value = 7) => null;
                public void Generic<[Marker("type", typeof(object), new[]{5})] T>(T value) where T : class, IDisposable, new() { }
                public new void Overload(int value) { }
            }
            """;
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var compilation = CSharpCompilation.Create("ApiAuditFixture", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        if (!emitted.Success) throw new InvalidOperationException(string.Join("\n", emitted.Diagnostics));
        var fixture = MetadataReference.CreateFromImage(stream.ToArray());
        var metadata = CSharpCompilation.Create("MetadataReader", references: references.Append(fixture),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All));
        var type = ((IAssemblySymbol)metadata.GetAssemblyOrModuleSymbol(fixture)!).GetTypeByMetadataName("Child")!;
        var unresolved = new List<string>();
        var entries = ApiSemantics.Read(type, static value => value, CheckType, unresolved.Add);
        var checks = 0;
        Check(unresolved.Count == 0, "all fixture dependencies resolve");
        Check(entries.Any(x => x.Relation == "inherited-member-candidate" && x.Raw.Contains("Parent<System.String>.Value", StringComparison.Ordinal)), "constructed generic base arguments");
        Check(entries.Count(x => x.Relation == "inherited-member-candidate" && x.Declaration.Contains("Overload", StringComparison.Ordinal)) == 2, "all inherited overload candidates retained");
        Check(entries.Any(x => x.Relation == "declared-member-metadata" && x.Declaration.Contains("Overload", StringComparison.Ordinal)), "hiding declaration retained separately");
        Check(entries.Any(x => x.Raw.Contains("Parent<System.String>.Echo", StringComparison.Ordinal)), "protected inheritance");
        Check(!entries.Any(x => x.Declaration.Contains("Hidden", StringComparison.Ordinal)), "private base member exclusion");
        Check(!entries.Any(x => x.BaseDepth > 0 && x.Declaration.Contains("#ctor", StringComparison.Ordinal)), "constructors not inherited");
        Check(entries.Any(x => x.Raw.Contains("attribute:declaration=MarkerAttribute", StringComparison.Ordinal) && x.Raw.Contains("named", StringComparison.Ordinal)), "type attributes and named arguments");
        Check(entries.Any(x => x.Raw.Contains("typeof(System.String)", StringComparison.Ordinal) && x.Raw.Contains(":1", StringComparison.Ordinal) && x.Raw.Contains(":2", StringComparison.Ordinal)), "type and array constants");
        Check(entries.Any(x => x.Raw.Contains("attribute:return=MarkerAttribute", StringComparison.Ordinal)), "return attributes");
        Check(entries.Any(x => x.Raw.Contains("attribute:parameter:0=MarkerAttribute", StringComparison.Ordinal)), "parameter attributes");
        Check(entries.Any(x => x.Raw.Contains("parameter:0:default=7", StringComparison.Ordinal)), "optional parameter constants");
        Check(entries.Any(x => x.Raw.Contains("required=True", StringComparison.Ordinal) && x.Raw.Contains("init-only=True", StringComparison.Ordinal)), "required and init-only properties");
        Check(entries.Any(x => x.Raw.Contains("set:accessibility=Private", StringComparison.Ordinal)), "accessor accessibility");
        Check(entries.Any(x => x.Raw.Contains("constant=17", StringComparison.Ordinal)), "literal field values without executing initializers");
        Check(entries.Any(x => x.Relation == "interface-member-contract" && x.Raw.Contains("implementation=", StringComparison.Ordinal) && x.Raw.Contains("IContract<System.Int32>", StringComparison.Ordinal)), "explicit interface implementation");
        Check(entries.Any(x => x.Raw.Contains("type-parameter:0:constraint=System.IDisposable", StringComparison.Ordinal)), "generic constraints");
        Check(entries.Any(x => x.Raw.Contains("attribute:type-parameter:0=MarkerAttribute", StringComparison.Ordinal)), "generic parameter attributes");
        Check(entries.Any(x => x.Raw.Contains("return-nullability=Annotated", StringComparison.Ordinal)), "return nullability");
        var repeated = ApiSemantics.Read(type, static value => value, CheckType, unresolved.Add);
        Check(entries.SequenceEqual(repeated), "deterministic repeated inventory");
        Console.WriteLine($"UNO_API_SEMANTIC_CHECKS_PASSED={checks}; compiled metadata only; no fixture execution");
        return checks;

        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("API metadata regression: " + name);
            ++checks;
            Console.WriteLine("UNO_API_SEMANTIC_CASE_PASSED=" + name);
        }
        void CheckType(ITypeSymbol? symbol)
        {
            if (symbol?.TypeKind == TypeKind.Error) unresolved.Add(symbol.ToDisplayString());
            if (symbol is INamedTypeSymbol named)
                foreach (var argument in named.TypeArguments) CheckType(argument);
            if (symbol is IArrayTypeSymbol array) CheckType(array.ElementType);
        }
    }
}
