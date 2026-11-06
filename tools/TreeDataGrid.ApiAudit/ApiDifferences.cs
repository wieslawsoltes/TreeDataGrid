using System;
using System.Collections.Generic;
using System.Linq;

namespace TreeDataGrid.Tools.ApiAudit;

internal sealed record ApiEntry(string Assembly, string Kind, string Raw, string Normalized,
    string Identity, string DeclaringType, string MetadataName);

internal sealed record ApiDifference(string Category, ApiEntry Baseline, ApiEntry[] Candidates);

/// <summary>
/// Triage of declared metadata, not an equivalence rule. Documentation identities
/// distinguish overloads while deliberately retaining native parameter types.
/// Candidates require review; no category removes a difference from the raw audit.
/// </summary>
internal static class ApiDifferences
{
    public static ApiDifference[] Classify(IEnumerable<ApiEntry> baseline, IEnumerable<ApiEntry> target)
    {
        var right = target.OrderBy(entry => entry.Normalized, StringComparer.Ordinal)
            .ThenBy(entry => entry.Assembly, StringComparer.Ordinal).ToArray();
        var shapes = right.Select(entry => entry.Normalized).ToHashSet(StringComparer.Ordinal);
        var identities = right.ToLookup(entry => entry.Identity, StringComparer.Ordinal);
        var types = right.Where(IsType).Select(entry => entry.Identity).ToHashSet(StringComparer.Ordinal);
        var typeNames = right.Where(IsType).ToLookup(entry => entry.MetadataName, StringComparer.Ordinal);
        var members = right.Where(entry => !IsType(entry)).ToLookup(entry =>
            (entry.DeclaringType, entry.Kind, entry.MetadataName));
        return baseline.Where(entry => !shapes.Contains(entry.Normalized))
            .OrderBy(entry => entry.Normalized, StringComparer.Ordinal)
            .ThenBy(entry => entry.Assembly, StringComparer.Ordinal)
            .DistinctBy(entry => entry.Normalized, StringComparer.Ordinal)
            .Select(ClassifyEntry).ToArray();

        ApiDifference ClassifyEntry(ApiEntry entry)
        {
            if (identities.Contains(entry.Identity))
                return new("declaration-changed-at-same-identity", entry, identities[entry.Identity].ToArray());
            if (IsType(entry))
                return new("exported-type-not-found-at-identity", entry, typeNames[entry.MetadataName].ToArray());
            if (!types.Contains(entry.DeclaringType))
                return new("member-of-absent-exported-type", entry, []);
            var candidates = members[(entry.DeclaringType, entry.Kind, entry.MetadataName)].ToArray();
            return candidates.Length > 0
                ? new("overload-or-parameter-identity-difference", entry, candidates)
                : new("member-not-declared-on-matched-type", entry, []);
        }
    }

    public static bool IsType(ApiEntry entry) => entry.Identity.StartsWith("T:", StringComparison.Ordinal);
}
