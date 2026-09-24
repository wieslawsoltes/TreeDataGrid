using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TreeDataGrid.Tools.ApiAudit;

/// <summary>
/// Maps explicit framework-owned namespace roots, never constant values or
/// substrings of another namespace. Raw inventories are stored independently.
/// </summary>
internal static class ApiNameNormalizer
{
    public static IReadOnlyDictionary<string, string> Mappings { get; } =
        new ReadOnlyDictionary<string, string>(new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["Avalonia.Controls"] = "UI.Controls",
            ["Uno.Controls"] = "UI.Controls",
            ["Avalonia.Data.Core.Parsers"] = "UI.Data.Core.Parsers",
            ["Uno.Data.Core.Parsers"] = "UI.Data.Core.Parsers",
            ["Avalonia.Experimental.Data"] = "UI.Experimental.Data",
            ["Uno.Experimental.Data"] = "UI.Experimental.Data",
        });
    private static readonly (string From, string To)[] Prefixes = Mappings
        .Select(pair => (pair.Key + ".", pair.Value + ".")).ToArray();

    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        StringBuilder? result = null;
        var copied = 0;
        for (var index = 0; index < value.Length;)
        {
            if (value[index] is '"' or '\'')
            {
                index = SkipLiteral(value, index);
                continue;
            }
            var replaced = false;
            if (value[index] is 'A' or 'U' && IsRootBoundary(value, index))
            {
                foreach (var (from, to) in Prefixes)
                {
                    if (!value.AsSpan(index).StartsWith(from.AsSpan(), StringComparison.Ordinal)) continue;
                    result ??= new StringBuilder(value.Length);
                    result.Append(value, copied, index - copied).Append(to);
                    index += from.Length;
                    copied = index;
                    replaced = true;
                    break;
                }
            }
            if (!replaced) ++index;
        }
        return result is null ? value : result.Append(value, copied, value.Length - copied).ToString();
    }

    private static bool IsRootBoundary(string text, int index)
    {
        if (index == 0) return true;
        var previous = text[index - 1];
        if (previous is '.' or '@' || previous == '_' || char.IsLetterOrDigit(previous)) return false;
        return char.GetUnicodeCategory(previous) is not (
            UnicodeCategory.LetterNumber or UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or UnicodeCategory.ConnectorPunctuation or
            UnicodeCategory.Format or UnicodeCategory.Surrogate);
    }

    private static int SkipLiteral(string text, int start)
    {
        var quote = text[start];
        var verbatim = quote == '"' && start > 0 && text[start - 1] == '@';
        // Roslyn's metadata display and JSON use ordinary escaped literals.
        // Preserve verbatim spellings too. Unclosed values are copied intact,
        // never searched for namespace candidates inside a damaged literal.
        for (var index = start + 1; index < text.Length; ++index)
        {
            if (!verbatim && text[index] == '\\') { ++index; continue; }
            if (text[index] != quote) continue;
            if (verbatim && index + 1 < text.Length && text[index + 1] == quote) { ++index; continue; }
            return index + 1;
        }
        return text.Length;
    }

    internal static int RunChecks()
    {
        var cases = new (string Input, string Expected)[]
        {
            ("Avalonia.Controls.TreeDataGrid", "UI.Controls.TreeDataGrid"),
            ("Uno.Controls.TreeDataGrid", "UI.Controls.TreeDataGrid"),
            ("T:Avalonia.Controls.Models.TreeDataGrid.ColumnList`1", "T:UI.Controls.Models.TreeDataGrid.ColumnList`1"),
            ("global::Uno.Controls.TreeDataGrid", "global::UI.Controls.TreeDataGrid"),
            ("System.Tuple<Avalonia.Controls.A, Uno.Controls.B>", "System.Tuple<UI.Controls.A, UI.Controls.B>"),
            ("Other.Avalonia.Controls.A", "Other.Avalonia.Controls.A"),
            ("MyAvalonia.Controls.A", "MyAvalonia.Controls.A"),
            ("_Uno.Controls.A", "_Uno.Controls.A"),
            ("\u03bbUno.Controls.A", "\u03bbUno.Controls.A"),
            ("\u0301Avalonia.Controls.A", "\u0301Avalonia.Controls.A"),
            ("@Avalonia.Controls.A", "@Avalonia.Controls.A"),
            ("Avalonia.ControlsExtra.A", "Avalonia.ControlsExtra.A"),
            ("public string Uno.Controls.A.Value = \"Avalonia.Controls.A\"", "public string UI.Controls.A.Value = \"Avalonia.Controls.A\""),
            ("\"Avalonia.Controls.A\\\" Uno.Controls.B\" Uno.Controls.C", "\"Avalonia.Controls.A\\\" Uno.Controls.B\" UI.Controls.C"),
            ("@\"Avalonia.Controls.A\"\"Uno.Controls.B\" Uno.Controls.C", "@\"Avalonia.Controls.A\"\"Uno.Controls.B\" UI.Controls.C"),
            ("'\\\'' Uno.Controls.A", "'\\\'' UI.Controls.A"),
            ("\"Uno.Controls.A", "\"Uno.Controls.A"),
            ("constant=\"Uno.Controls.DataFormat\"", "constant=\"Uno.Controls.DataFormat\""),
            ("typeof(Avalonia.Experimental.Data.TypedBinding<T>)", "typeof(UI.Experimental.Data.TypedBinding<T>)"),
            ("T:Uno.Data.Core.Parsers.ExpressionChainVisitor`1", "T:UI.Data.Core.Parsers.ExpressionChainVisitor`1"),
            ("TreeDataGridCore.IndexPath", "TreeDataGridCore.IndexPath"),
        };
        var checks = 0;
        foreach (var (input, expected) in cases)
        {
            Check(Normalize(input) == expected, "literal/boundary case " + checks);
            if (input == expected) Check(ReferenceEquals(input, Normalize(input)), "unchanged reference " + checks);
            Check(Normalize(Normalize(input)) == expected, "idempotence " + checks);
        }
        // Read real compiler symbols too. Matching member/type names cannot
        // disguise differing namespace-looking string constants or defaults.
        var first = CreateSymbols("Avalonia", "Avalonia.Controls.Token");
        var second = CreateSymbols("Uno", "Uno.Controls.Token");
        var identicalValue = CreateSymbols("Uno", "Avalonia.Controls.Token");
        Check(Normalize(first.Type) == Normalize(second.Type), "framework type names normalize");
        Check(Normalize(first.Field) != Normalize(second.Field), "different string constants remain different");
        Check(Normalize(first.Method) != Normalize(second.Method), "different optional defaults remain different");
        Check(Normalize(first.Field) == Normalize(identicalValue.Field), "identical string constants remain comparable");
        Check(Normalize(first.Method) == Normalize(identicalValue.Method), "identical optional defaults remain comparable");
        Console.WriteLine("UNO_API_NORMALIZATION_CHECKS_PASSED=" + checks);
        return checks;

        void Check(bool success, string name)
        {
            if (!success) throw new InvalidOperationException("API normalization regression: " + name);
            ++checks;
        }
    }

    private static (string Type, string Field, string Method) CreateSymbols(string framework, string literal)
    {
        var source = "namespace " + framework + ".Controls { public class Probe { public const string Token = \"" +
            literal + "\"; public void Run(string token = \"" + literal + "\") { } } }";
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("NormalizerFixture" + framework,
            [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors.Select(item => item.ToString())));
        var type = compilation.GetTypeByMetadataName(framework + ".Controls.Probe")!;
        var format = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeType |
                SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeConstantValue,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName |
                SymbolDisplayParameterOptions.IncludeDefaultValue);
        return (type.ToDisplayString(format), type.GetMembers("Token").Single().ToDisplayString(format),
            type.GetMembers("Run").Single().ToDisplayString(format));
    }
}
