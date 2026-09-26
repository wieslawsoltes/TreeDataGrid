using System.Collections.Generic;
using System.Linq;
using TreeDataGridUnoSample;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class PublicExpanderConstructionTests
{
    public static IEnumerable<object[]> Cases =>
        PublicExpanderConstructionChecks.Cases.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(Cases))]
    public void Public_expander_construction_owns_only_completed_observations(string name) =>
        PublicExpanderConstructionChecks.Run(name);
}
