using System;
using System.Linq;
using TreeDataGrid.Tools.ApiAudit;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class ApiDifferenceClassificationTests
{
    [Fact]
    public void Identical_metadata_has_no_classified_differences()
    {
        ApiEntry[] surface = [Type("T:UI.Row"), Member("M:UI.Row.Realize(System.Int32)", "Realize", "original")];
        Assert.Empty(ApiDifferences.Classify(surface, surface.Reverse()));
    }

    [Fact]
    public void Absent_type_and_its_members_are_distinct_from_missing_members_on_present_types()
    {
        ApiEntry[] baseline = [Type("T:UI.Row"), Member("M:UI.Row.Realize", "Realize", "original")];
        var result = ApiDifferences.Classify(baseline, []);
        Assert.Equal(2, result.Length);
        Assert.Contains(result, item => item.Category == "exported-type-not-found-at-identity");
        Assert.Contains(result, item => item.Category == "member-of-absent-exported-type");
        Assert.All(result, item => Assert.Empty(item.Candidates));
        var matchedType = Assert.Single(ApiDifferences.Classify(baseline, [baseline[0]]));
        Assert.Equal("member-not-declared-on-matched-type", matchedType.Category);
    }

    [Fact]
    public void Return_accessibility_or_default_changes_do_not_become_absent_overloads()
    {
        var before = Member("M:UI.Row.Realize(System.Int32)", "Realize", "public void Realize(int index = 1)");
        var after = before with { Normalized = "protected bool Realize(int index = 2)" };
        var result = Assert.Single(ApiDifferences.Classify([before], [after]));
        Assert.Equal("declaration-changed-at-same-identity", result.Category);
        Assert.Same(after, Assert.Single(result.Candidates));
    }

    [Fact]
    public void Native_parameter_differences_are_candidates_not_automatically_accepted()
    {
        var before = Member("M:UI.Row.Realize(Avalonia.Controls.Control)", "Realize", "Avalonia signature");
        var after = Member("M:UI.Row.Realize(Microsoft.UI.Xaml.Controls.Control)", "Realize", "native signature");
        var result = Assert.Single(ApiDifferences.Classify([Type("T:UI.Row"), before], [Type("T:UI.Row"), after]));
        Assert.Equal("overload-or-parameter-identity-difference", result.Category);
        Assert.Same(after, Assert.Single(result.Candidates));
    }

    [Fact]
    public void Relocated_type_candidates_do_not_hide_absent_original_identity()
    {
        var before = Type("T:UI.Row");
        var relocated = Type("T:Core.Row");
        var result = Assert.Single(ApiDifferences.Classify([before], [relocated]));
        Assert.Equal("exported-type-not-found-at-identity", result.Category);
        Assert.Same(relocated, Assert.Single(result.Candidates));
    }

    [Fact]
    public void Candidate_order_and_difference_order_do_not_depend_on_input_enumeration()
    {
        var a = Member("M:UI.Row.Realize(A)", "Realize", "a");
        var b = Member("M:UI.Row.Realize(B)", "Realize", "b");
        var original = Member("M:UI.Row.Realize(C)", "Realize", "c");
        var target = new[] { Type("T:UI.Row"), b, a };
        var first = ApiDifferences.Classify([original, original], target);
        var second = ApiDifferences.Classify([original], target.Reverse());
        Assert.Single(first);
        Assert.Equal(first.Select(item => item.Baseline), second.Select(item => item.Baseline));
        Assert.Equal(new[] { a, b }, first[0].Candidates);
        Assert.Equal(first[0].Candidates, second[0].Candidates);
    }

    private static ApiEntry Type(string identity) => new("Fixture", "NamedType", identity, identity,
        identity, identity, identity[(identity.LastIndexOf('.') + 1)..]);
    private static ApiEntry Member(string identity, string name, string shape) => new("Fixture", "Method", shape,
        shape, identity, "T:UI.Row", name);
}
