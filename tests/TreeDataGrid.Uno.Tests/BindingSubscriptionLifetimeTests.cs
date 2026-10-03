using TreeDataGridUnoSample;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class BindingSubscriptionLifetimeTests
{
    [Theory]
    [InlineData("independent-removals")]
    [InlineData("nested-removals")]
    [InlineData("property-add-failure")]
    [InlineData("collection-add-failure")]
    [InlineData("add-and-cleanup-errors")]
    [InlineData("constructor-and-cleanup-errors")]
    [InlineData("reentrant-add-retirement")]
    [InlineData("recursive-dispose")]
    [InlineData("duplicate-owner")]
    [InlineData("failed-retarget-recovery")]
    public void Public_cell_binding_cleanup_preserves_ownership_and_failures(string name) =>
        BindingSubscriptionLifetimeChecks.RunCase(name);
}
