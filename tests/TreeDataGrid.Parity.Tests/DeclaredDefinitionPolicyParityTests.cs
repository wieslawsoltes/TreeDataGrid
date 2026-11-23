using System;
using System.Reflection;
using Xunit;
using A = Avalonia.Controls;
using U = Uno.Controls;

namespace TreeDataGrid.Parity.Tests;

public sealed class DeclaredDefinitionPolicyParityTests
{
    private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Theory]
    [InlineData("CanUserResize")]
    [InlineData("CanUserSortColumn")]
    [InlineData("AllowTriStateSorting")]
    [InlineData("CompareAscending")]
    [InlineData("CompareDescending")]
    [InlineData("BeginEditGestures")]
    public void Portable_policy_is_declared_on_the_reference_owner_without_a_second_field(string name)
    {
        var expected = typeof(A.TreeDataGridColumn).GetProperty(name, Declared)!;
        var actual = typeof(U.TreeDataGridColumn).GetProperty(name, Declared);
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.PropertyType.Name, actual!.PropertyType.Name);
        Assert.Equal(expected.GetMethod!.Attributes, actual.GetMethod!.Attributes);
        Assert.Equal(expected.SetMethod!.Attributes, actual.SetMethod!.Attributes);
        var nullability = new NullabilityInfoContext();
        Assert.Equal(nullability.Create(expected).ReadState, nullability.Create(actual).ReadState);
        Assert.Equal(nullability.Create(expected).WriteState, nullability.Create(actual).WriteState);
        Assert.Null(typeof(U.TreeDataGridColumn).GetField($"<{name}>k__BackingField", Declared));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_and_base_configuration_share_nullable_policies_and_callback_identity(bool? policy)
    {
        var expected = new ReferenceDefinition();
        var actual = new NativeDefinition();
        U.ColumnCreateOptions common = actual;
        expected.CanUserResize = actual.CanUserResize = policy;
        expected.CanUserSortColumn = common.CanUserSortColumn = policy;
        expected.AllowTriStateSorting = common.AllowTriStateSorting = true;
        Comparison<object?> comparison = static (_, _) => 17;
        expected.CompareAscending = actual.CompareAscending = comparison;
        expected.CompareDescending = common.CompareDescending = comparison;
        Assert.Equal(expected.CanUserResize, common.CanUserResize);
        Assert.Equal(expected.CanUserSortColumn, actual.CanUserSortColumn);
        Assert.Equal(expected.AllowTriStateSorting, actual.AllowTriStateSorting);
        Assert.Same(comparison, common.CompareAscending);
        Assert.Same(comparison, actual.CompareDescending);
        common.CanUserResize = actual.CanUserSortColumn = null;
        common.CompareAscending = actual.CompareDescending = null;
        Assert.Null(actual.CanUserResize);
        Assert.Null(common.CanUserSortColumn);
        Assert.Null(actual.CompareAscending);
        Assert.Null(common.CompareDescending);
        common.BeginEditGestures = U.Models.TreeDataGrid.BeginEditGestures.None;
        Assert.Equal(U.Models.TreeDataGrid.BeginEditGestures.None, actual.BeginEditGestures);
        actual.BeginEditGestures = U.Models.TreeDataGrid.BeginEditGestures.Default;
        Assert.Equal(U.Models.TreeDataGrid.BeginEditGestures.Default, common.BeginEditGestures);
    }

    [Fact]
    public void Common_options_keep_captured_scalar_policies_and_live_comparison_callbacks()
    {
        var expected = new ReferenceDefinition { CanUserResize = false, CanUserSortColumn = true };
        var actual = new NativeDefinition { CanUserResize = false, CanUserSortColumn = true };
        expected.CompareAscending = actual.CompareAscending = static (_, _) => 7;
        expected.CompareDescending = actual.CompareDescending = static (_, _) => -7;
        var a = expected.Snapshot();
        var u = actual.Snapshot();
        ((U.ColumnCreateOptions)actual).CanUserResize = expected.CanUserResize = true;
        ((U.ColumnCreateOptions)actual).CanUserSortColumn = expected.CanUserSortColumn = false;
        ((U.ColumnCreateOptions)actual).CompareAscending = expected.CompareAscending = static (_, _) => 29;
        ((U.ColumnCreateOptions)actual).CompareDescending = expected.CompareDescending = static (_, _) => -29;
        Assert.Equal(a.CanUserResizeColumn, u.CanUserResizeColumn);
        Assert.Equal(a.CanUserSortColumn, u.CanUserSortColumn);
        Assert.False(u.CanUserResizeColumn);
        Assert.True(u.CanUserSortColumn);
        Assert.Equal(a.CompareAscending!(null, null), u.CompareAscending!(null, null));
        Assert.Equal(29, u.CompareAscending!(null, null));
        Assert.Equal(a.CompareDescending!(null, null), u.CompareDescending!(null, null));
        var failure = new InvalidOperationException("caller comparison");
        expected.CompareAscending = actual.CompareAscending = (_, _) => throw failure;
        Assert.Same(failure, Record.Exception(() => a.CompareAscending!(null, null)));
        Assert.Same(failure, Record.Exception(() => u.CompareAscending!(null, null)));
        expected.CompareDescending = actual.CompareDescending = null;
        Assert.IsType<NullReferenceException>(Record.Exception(() => a.CompareDescending!(null, null)));
        Assert.IsType<NullReferenceException>(Record.Exception(() => u.CompareDescending!(null, null)));
    }

    [Fact]
    public void Unconfigured_comparisons_remain_unconfigured_in_an_existing_snapshot()
    {
        var expected = new ReferenceDefinition();
        var actual = new NativeDefinition();
        var a = expected.Snapshot(); var u = actual.Snapshot();
        expected.CompareAscending = actual.CompareAscending = static (_, _) => 31;
        Assert.Null(a.CompareAscending); Assert.Null(u.CompareAscending);
        // Native Auto has a different unused numeric payload (1 versus 0).
        // Assert the actual unit contract; no GridLength normalization is added.
        Assert.True(expected.Width.IsAuto);
        Assert.True(actual.Width.IsAuto);
        Assert.Equal(expected.MinWidth.Value, actual.MinWidth.Value);
        Assert.Null(actual.MaxWidth);
        Assert.Equal((int)expected.BeginEditGestures, (int)actual.BeginEditGestures);
        Assert.False(actual.AllowTriStateSorting);
    }

    private sealed class ReferenceDefinition : A.TreeDataGridTextColumn
    {
        public A.Models.TreeDataGrid.ColumnOptions<object> Snapshot() => CreateCommonOptions();
    }
    private sealed class NativeDefinition : U.TreeDataGridTextColumn
    {
        public U.Models.TreeDataGrid.ColumnOptions<object> Snapshot() => CreateCommonOptions();
    }
}
