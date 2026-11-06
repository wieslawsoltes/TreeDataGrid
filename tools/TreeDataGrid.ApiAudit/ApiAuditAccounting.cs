using System.Text.Json;

namespace TreeDataGrid.Tools.ApiAudit;

internal sealed record ApiShapeCounts(int Baseline, int Target, int Exact, int MissingOrDifferent, int AdditionalOrDifferent);
internal sealed record ApiNormalizationCollision(string Side, string Shape, ApiEntry[] Declarations);
internal sealed record ApiInheritedReview(ApiEntry Baseline, ApiSemanticEntry[] Candidates);
internal sealed record ApiAccountingReport(
    ApiShapeCounts AllInputs, ApiShapeCounts SharedCore, ApiShapeCounts UiAssemblies,
    bool SharedCoreBytesIdentical, bool ScopeCountsAreAdditive,
    ApiNormalizationCollision[] NormalizationCollisions, ApiInheritedReview[] InheritedMemberReviews,
    int RawDifferencesRemoved, bool InheritedCandidatesAreCompatibilityProof);

/// <summary>Separate dependency self-matches from UI-port evidence without waiving raw differences.</summary>
internal static class ApiAuditAccounting
{
    internal static ApiShapeCounts Count(IEnumerable<ApiEntry> baseline, IEnumerable<ApiEntry> target)
    {
        var left = baseline.Select(x => x.Normalized).ToHashSet(StringComparer.Ordinal);
        var right = target.Select(x => x.Normalized).ToHashSet(StringComparer.Ordinal);
        return new(left.Count, right.Count, left.Intersect(right).Count(),
            left.Except(right).Count(), right.Except(left).Count());
    }

    internal static ApiAccountingReport Create(Surface baseline, Surface target, string coreAssembly)
    {
        var all = Count(baseline.Entries, target.Entries);
        var core = Count(baseline.Entries.Where(x => x.Assembly == coreAssembly), target.Entries.Where(x => x.Assembly == coreAssembly));
        var ui = Count(baseline.Entries.Where(x => x.Assembly != coreAssembly), target.Entries.Where(x => x.Assembly != coreAssembly));
        var leftCore = baseline.Inputs.SingleOrDefault(x => x.Name == coreAssembly);
        var rightCore = target.Inputs.SingleOrDefault(x => x.Name == coreAssembly);
        var collisions = FindCollisions("baseline", baseline.Entries).Concat(FindCollisions("target", target.Entries)).ToArray();
        var inherited = target.SemanticEntries.Where(x => x.Relation == "inherited-member-candidate")
            .ToLookup(x => (ApiNameNormalizer.Normalize(x.Owner), MemberKind(x.Declaration), MemberName(x.Declaration)));
        var reviews = ApiDifferences.Classify(baseline.Entries, target.Entries)
            .Where(x => !ApiDifferences.IsType(x.Baseline))
            .Select(x => new ApiInheritedReview(x.Baseline,
                inherited[(x.Baseline.DeclaringType, MemberKind(x.Baseline.Identity), x.Baseline.MetadataName)]
                    .OrderBy(x => x.BaseDepth).ThenBy(x => x.Raw, StringComparer.Ordinal).ToArray()))
            .Where(x => x.Candidates.Length > 0).ToArray();
        return new(all, core, ui, leftCore is not null && rightCore is not null && leftCore.Sha256 == rightCore.Sha256,
            all == new ApiShapeCounts(core.Baseline + ui.Baseline, core.Target + ui.Target, core.Exact + ui.Exact,
                core.MissingOrDifferent + ui.MissingOrDifferent, core.AdditionalOrDifferent + ui.AdditionalOrDifferent),
            collisions, reviews, 0, false);
    }

    internal static ApiNormalizationCollision[] FindCollisions(string side, IEnumerable<ApiEntry> entries) => entries
        .GroupBy(x => x.Normalized, StringComparer.Ordinal)
        .Where(group => group.Select(x => (x.Assembly, x.Raw)).Distinct().Skip(1).Any())
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => new ApiNormalizationCollision(side, group.Key,
            group.OrderBy(x => x.Assembly, StringComparer.Ordinal).ThenBy(x => x.Raw, StringComparer.Ordinal).ToArray())).ToArray();

    internal static ApiAccountingReport Write(Surface baseline, Surface target, string coreAssembly, string output, JsonSerializerOptions options)
    {
        var report = Create(baseline, target, coreAssembly);
        File.WriteAllText(Path.Combine(output, "scope-accounting.json"), JsonSerializer.Serialize(report, options) + "\n");
        var rows = new List<string>
        {
            "# API audit scope and inherited candidates", "",
            "The raw audit compares declared metadata, not implemented features or C# lookup equivalence.",
            "Shared Core is supplied on both sides. Its self-matches must not be presented as Uno UI-port coverage.", "",
            "| Scope | Baseline | Target | Exact | Missing/different | Additional/different |",
            "| --- | ---: | ---: | ---: | ---: | ---: |",
            Row("All inputs (historical raw metric)", report.AllInputs),
            Row("Shared Core only", report.SharedCore),
            Row("UI assemblies only", report.UiAssemblies), "",
            $"Shared Core bytes identical: **{report.SharedCoreBytesIdentical}**. Scope counts additive: **{report.ScopeCountsAreAdditive}**.",
            $"Normalization collision groups: **{report.NormalizationCollisions.Length}**. Differences with same-name inherited candidates: **{report.InheritedMemberReviews.Length}**.", "",
            "## Inherited candidates requiring consumer review", "",
            "Candidates keep their declaring assembly/type and constructed signature. Same name does not establish overload applicability, hiding, accessibility, metadata or behavioral equivalence. No raw difference is removed.", ""
        };
        foreach (var review in report.InheritedMemberReviews)
        {
            rows.Add("### " + Escape(review.Baseline.Identity));
            rows.Add("Baseline: `" + Escape(review.Baseline.Normalized) + "`");
            foreach (var candidate in review.Candidates)
                rows.Add("- `" + Escape(candidate.DeclaringAssembly + ": " + candidate.Declaration) + "` (base depth " + candidate.BaseDepth + ")");
            rows.Add("");
        }
        File.WriteAllLines(Path.Combine(output, "scope-accounting.md"), rows);
        Console.WriteLine("UNO_API_SCOPE_ACCOUNTING=" + JsonSerializer.Serialize(new
        {
            report.AllInputs, report.SharedCore, report.UiAssemblies, report.SharedCoreBytesIdentical, report.ScopeCountsAreAdditive,
            normalizationCollisions = report.NormalizationCollisions.Length,
            inheritedCandidateDifferences = report.InheritedMemberReviews.Length,
            report.RawDifferencesRemoved, report.InheritedCandidatesAreCompatibilityProof
        }));
        Console.WriteLine("UNO_API_INHERITED_REVIEW_IDENTITIES=" + JsonSerializer.Serialize(report.InheritedMemberReviews.Select(x => x.Baseline.Identity)));
        return report;

        static string Row(string label, ApiShapeCounts x) => $"| {label} | {x.Baseline} | {x.Target} | {x.Exact} | {x.MissingOrDifferent} | {x.AdditionalOrDifferent} |";
        static string Escape(string text) => text.Replace('`', '\'').Replace("|", "\\|");
    }

    private static char MemberKind(string identity) => identity.Length > 1 && identity[1] == ':' ? identity[0] : '?';
    private static string MemberName(string identity)
    {
        var end = identity.IndexOf('(');
        if (end < 0) end = identity.Length;
        var head = identity[..end];
        var name = head[(head.LastIndexOf('.') + 1)..];
        var arity = name.IndexOf("``", StringComparison.Ordinal);
        return arity < 0 ? name : name[..arity];
    }
}
