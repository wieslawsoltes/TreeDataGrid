using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TreeDataGrid.Tools.ApiAudit;

/// <summary>End-to-end fixtures use the same emitted-PE Surface.Read path as production audits.</summary>
internal static class ApiSurfaceChecks
{
    internal static int Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdg-api-surface-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var checks = 0;
        try
        {
            var path = Emit("SurfaceFixture", """
                #nullable enable
                namespace AuditFixtures;
                public class Parent<T>
                {
                    public T Echo(T value) => value;
                    protected virtual void Hook() { }
                    private protected void AssemblyOnly() { }
                    internal void Internal() { }
                    private void Private() { }
                }
                public class Child : Parent<string>
                {
                    public new int Echo(int value) => value;
                    public const double NotANumber = double.NaN;
                    public const double PositiveInfinity = double.PositiveInfinity;
                    public const float NegativeInfinity = float.NegativeInfinity;
                    public const double NegativeZero = -0.0;
                    public const double PositiveZero = 0.0;
                    public const string Literal = "Avalonia.Controls.NotAType";
                    public string? Name { get; private set; }
                    public void ByRef(ref int value) { }
                }
                internal class Hidden { public class Nested { } }
                public interface IContract { void Execute(); }
                public class Explicit : IContract { void IContract.Execute() { } }
                """);
            var surface = Surface.Read([path], null);
            Check(surface.Inputs.Length == 1 && surface.Inputs[0].Name == "SurfaceFixture", "actual emitted assembly identity");
            Check(surface.UnresolvedTypes.Length == 0, "compiled fixture dependencies resolve");
            Check(Has("T:AuditFixtures.Child"), "public type recorded");
            Check(Has("M:AuditFixtures.Parent`1.Hook"), "protected extension point recorded");
            Check(!surface.Entries.Any(x => x.MetadataName is "AssemblyOnly" or "Internal" or "Private"), "nonexternal members excluded");
            Check(!surface.Entries.Any(x => x.DeclaringType.Contains("Hidden", StringComparison.Ordinal)), "nested public type under internal owner excluded");
            Check(Field("NotANumber").Raw.Contains("ieee754:System.Double:0x", StringComparison.Ordinal), "declared NaN survives full reader");
            Check(Field("PositiveInfinity").Raw.EndsWith("0x7ff0000000000000", StringComparison.Ordinal), "declared positive infinity bits");
            Check(Field("NegativeInfinity").Raw.EndsWith("0xff800000", StringComparison.Ordinal), "declared single negative infinity bits");
            Check(Field("NegativeZero").Raw.EndsWith("0x8000000000000000", StringComparison.Ordinal), "declared negative zero bits");
            Check(Field("PositiveZero").Normalized != Field("NegativeZero").Normalized, "signed zero cannot match");
            Check(Field("Literal").Normalized.Contains("\"Avalonia.Controls.NotAType\"", StringComparison.Ordinal), "namespace-like constant not rewritten");
            Check(surface.Entries.Any(x => x.MetadataName == "Name" && x.Raw.Contains("private set", StringComparison.Ordinal)), "private setter metadata retained");
            Check(surface.Entries.Any(x => x.MetadataName == "ByRef" && x.Raw.Contains("ref", StringComparison.Ordinal)), "ref parameter retained");
            Check(!surface.Entries.Any(x => x.DeclaringType == "T:AuditFixtures.Child" && x.MetadataName == "Hook"), "declared surface does not invent inherited declarations");
            Check(surface.SemanticEntries.Any(x => x.Owner == "T:AuditFixtures.Child" && x.Relation == "inherited-member-candidate" &&
                x.Raw.Contains("Echo", StringComparison.Ordinal) && x.Raw.Contains("System.String", StringComparison.Ordinal)), "constructed inherited signature retained separately");
            Check(surface.SemanticEntries.Any(x => x.Owner == "T:AuditFixtures.Explicit" && x.Relation == "interface-member-contract" &&
                x.Raw.Contains("implementation=", StringComparison.Ordinal) && !x.Raw.Contains("implementation=<none>", StringComparison.Ordinal)), "explicit interface implementation evidence retained");
            var again = Surface.Read([path], null);
            Check(JsonSerializer.Serialize(surface) == JsonSerializer.Serialize(again), "full declared inventory deterministic");
            Check(ApiDifferences.Classify(surface.Entries, again.Entries).Length == 0, "full pipeline self comparison");
            Check(ApiAuditAccounting.FindCollisions("fixture", surface.Entries).Length == 0, "ordinary declarations do not collide");

            var corePath = Emit("Fixture.Core", "namespace Shared { public class Model { public int Value { get; set; } } }");
            var leftPath = Emit("Fixture.Left", "namespace Avalonia.Controls { public class Widget { public int Value { get; set; } public void Lost() { } } }");
            var rightPath = Emit("Fixture.Right", "namespace Uno.Controls { public class Widget { public int Value { get; set; } } }");
            var left = Surface.Read([leftPath, corePath], null);
            var right = Surface.Read([rightPath, corePath], null);
            var accounting = ApiAuditAccounting.Create(left, right, "Fixture.Core");
            Check(accounting.SharedCoreBytesIdentical, "identical dependency bytes distinguished from UI evidence");
            Check(accounting.SharedCore.Exact > 0 && accounting.SharedCore.MissingOrDifferent == 0, "shared dependency self matches");
            Check(accounting.UiAssemblies.MissingOrDifferent == 1, "actual missing UI member not hidden by shared dependency");
            Check(accounting.ScopeCountsAreAdditive, "scoped counters reconcile with historical raw counters");
            Check(accounting.RawDifferencesRemoved == 0 && !accounting.InheritedCandidatesAreCompatibilityProof, "accounting cannot waive differences");
            var known = surface.Entries.First();
            var collision = known with { Assembly = "AnotherAssembly" };
            Check(ApiAuditAccounting.FindCollisions("fixture", [known, collision]).Length == 1, "cross-assembly normalized collision reported");
            Check(ApiAuditAccounting.FindCollisions("fixture", [known, known]).Length == 0, "identical duplicate record is not an identity collision");

            Console.WriteLine("UNO_API_SURFACE_CHECKS_PASSED=" + checks + "; emitted PE files read by production Surface.Read; no fixture methods executed");
            return checks;

            bool Has(string id) => surface.Entries.Any(x => x.Identity == id);
            ApiEntry Field(string name) => surface.Entries.Single(x => x.Kind == "Field" && x.MetadataName == name);
        }
        finally { Directory.Delete(directory, recursive: true); }

        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("API full-reader regression: " + name);
            ++checks;
            Console.WriteLine("UNO_API_SURFACE_CASE_PASSED=" + name);
        }
        string Emit(string name, string text)
        {
            var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(x => MetadataReference.CreateFromFile(x));
            var compilation = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(text)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));
            var output = Path.Combine(directory, name + ".dll");
            using var stream = File.Create(output);
            var emitted = compilation.Emit(stream);
            if (!emitted.Success) throw new InvalidDataException(string.Join("\n", emitted.Diagnostics));
            return output;
        }
    }
}
