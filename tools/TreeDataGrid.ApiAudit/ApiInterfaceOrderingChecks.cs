using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TreeDataGrid.Tools.ApiAudit;

/// <summary>Emitted-PE regressions for canonical interface ordering, not interface equivalence waivers.</summary>
internal static class ApiInterfaceOrderingChecks
{
    internal static int Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tdg-interface-order-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var checks = 0;
        try
        {
            var baseline = Read("Left", "Avalonia");
            var target = Read("Right", "Uno");
            var reordered = Read("Reordered", "Uno", reverse: true);
            Check(Type(baseline, "Widget").Normalized == Type(target, "Widget").Normalized,
                "mapped class interface lists compare independently of raw namespace sort order");
            Check(Type(baseline, "IProbe").Normalized == Type(target, "IProbe").Normalized,
                "mapped interface inheritance lists compare independently of raw namespace sort order");
            Check(ApiDifferences.Classify(baseline.Entries, target.Entries).Length == 0,
                "equivalent mapped declared inventories have no manufactured differences");
            Check(Type(target, "Widget").Raw == Type(reordered, "Widget").Raw &&
                Type(target, "IProbe").Raw == Type(reordered, "IProbe").Raw,
                "declaration order does not change the existing unordered interface-list contract");
            Check(Type(baseline, "Widget").Raw != Type(target, "Widget").Raw &&
                Type(baseline, "Widget").Raw.Contains("Avalonia.Controls.ILocal", StringComparison.Ordinal) &&
                Type(target, "Widget").Raw.Contains("Uno.Controls.ILocal", StringComparison.Ordinal),
                "raw inventory retains each original framework type spelling");
            Check(Type(target, "Widget").Raw.Contains(" | bases=class Common.Parent,", StringComparison.Ordinal),
                "base class remains separate and first");
            Check(target.Entries.All(entry => ApiNameNormalizer.Normalize(entry.Raw) == entry.Normalized),
                "candidate integrity still recomputes normalized values from complete raw records");
            Check(baseline.UnresolvedTypes.Length == 0 && target.UnresolvedTypes.Length == 0,
                "both emitted fixture dependency sets resolve");
            Check(ApiAuditAccounting.FindCollisions("fixture", target.Entries).Length == 0,
                "canonical ordering does not introduce ambiguous declaration identities");

            var missing = Read("Missing", "Uno", omitLocal: true);
            var added = Read("Added", "Uno", extra: true);
            var swapped = Read("SwappedArguments", "Uno", swapArguments: true);
            var otherBase = Read("OtherBase", "Uno", parent: "Other");
            Check(Type(target, "Widget").Normalized != Type(missing, "Widget").Normalized,
                "missing interface is not waived");
            Check(Type(target, "Widget").Normalized != Type(added, "Widget").Normalized,
                "additional interface is not erased");
            Check(Type(target, "Widget").Normalized != Type(swapped, "Widget").Normalized,
                "ordered nested generic arguments are not sorted as interfaces");
            Check(Type(target, "Widget").Normalized != Type(otherBase, "Widget").Normalized,
                "different base class still differs");
            Check(target.Entries.Single(entry => entry.MetadataName == "Literal").Normalized.Contains(
                "\"Avalonia.Controls.ILocal,Uno.Controls.ILocal\"", StringComparison.Ordinal),
                "namespace-looking and comma-containing constant remains literal data");
            Check(target.Entries.Single(entry => entry.MetadataName == "Ordered").Normalized.Contains(
                "System.Int32 first, System.String second", StringComparison.Ordinal),
                "parameter sequence and names remain ordered");
            var again = Surface.Read([Path.Combine(directory, "Right.dll")], null);
            Check(JsonSerializer.Serialize(target) == JsonSerializer.Serialize(again),
                "production reader remains deterministic on identical bytes");
            Console.WriteLine("UNO_API_INTERFACE_ORDERING_CHECKS_PASSED=" + checks);
            return checks;
        }
        finally { Directory.Delete(directory, recursive: true); }

        void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("API interface ordering regression: " + name);
            ++checks;
            Console.WriteLine("UNO_API_INTERFACE_ORDERING_CASE_PASSED=" + name);
        }
        Surface Read(string assembly, string framework, bool reverse = false, bool omitLocal = false,
            bool extra = false, bool swapArguments = false, string parent = "Parent")
        {
            var contracts = new List<string> { "System.IDisposable", swapArguments
                ? "Common.IComplex<System.Tuple<string, int>, int>"
                : "Common.IComplex<System.Tuple<int, string>, int>" };
            if (!omitLocal) contracts.Add("ILocal<int>");
            if (extra) contracts.Add("Common.IExtra");
            if (reverse) contracts.Reverse();
            var source = $$"""
                namespace Common
                {
                    public interface IComplex<TFirst, TSecond> { }
                    public interface IExtra { }
                    public class Parent { }
                    public class Other { }
                }
                namespace {{framework}}.Controls
                {
                    public interface ILocal<T> { }
                    public interface IProbe : {{string.Join(", ", contracts)}} { }
                    public abstract class Widget : Common.{{parent}}, {{string.Join(", ", contracts)}}
                    {
                        public const string Literal = "Avalonia.Controls.ILocal,Uno.Controls.ILocal";
                        public abstract void Dispose();
                        public void Ordered(int first, string second) { }
                    }
                }
                """;
            var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(assembly, [CSharpSyntaxTree.ParseText(source)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));
            var output = Path.Combine(directory, assembly + ".dll");
            using (var stream = File.Create(output))
            {
                var result = compilation.Emit(stream);
                if (!result.Success) throw new InvalidDataException(string.Join("\n", result.Diagnostics));
            }
            return Surface.Read([output], null);
        }
    }

    private static ApiEntry Type(Surface surface, string name) =>
        surface.Entries.Single(entry => entry.Kind == "NamedType" && entry.MetadataName == name);
}
