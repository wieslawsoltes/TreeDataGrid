using Microsoft.UI.Xaml;
using Xunit;
using global::Uno.Controls;
using global::Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnDefinitionOptionsTests
{
    [Fact]
    public void Derived_columns_can_create_reference_default_options()
    {
        var definition = new Definition();
        var options = definition.Capture();
        Assert.Null(options.CanUserResizeColumn);
        Assert.Null(options.CanUserSortColumn);
        Assert.Equal(new GridLength(30), options.MinWidth);
        Assert.Null(options.MaxWidth);
        Assert.Equal(BeginEditGestures.Default, options.BeginEditGestures);
        Assert.Null(options.CompareAscending);
        Assert.Null(options.CompareDescending);
    }

    [Fact]
    public void Scalar_policy_captures_are_independent_and_preserve_Core_units()
    {
        var definition = new Definition
        {
            CanUserResize = true,
            CanUserSortColumn = false,
            MinWidth = GridLength.Auto,
            MaxWidth = new GridLength(170),
            BeginEditGestures = BeginEditGestures.None,
        };
        var first = definition.Capture();
        definition.MinWidth = new GridLength(40);
        definition.MaxWidth = null;
        definition.CanUserResize = false;
        var second = definition.Capture();
        Assert.NotSame(first, second);
        Assert.Equal(true, first.CanUserResizeColumn);
        Assert.Equal(false, first.CanUserSortColumn);
        Assert.Equal(BeginEditGestures.None, first.BeginEditGestures);
        Assert.Equal(GridLength.Auto, first.MinWidth);
        Assert.Equal(new GridLength(170), first.MaxWidth);
        Assert.Equal(new GridLength(40), second.MinWidth);
        Assert.Null(second.MaxWidth);
        TreeDataGridCore.Models.ColumnOptions<object> core = first;
        Assert.True(core.MinWidth.IsAuto);
        Assert.Equal(170d, core.MaxWidth!.Value.Value);
    }

    [Fact]
    public void Configured_comparisons_follow_the_reference_live_definition_policy()
    {
        var left = new object();
        var right = new object();
        object? observedLeft = null, observedRight = null;
        var definition = new Definition
        {
            CompareAscending = (a, b) => { observedLeft = a; observedRight = b; return 17; },
            CompareDescending = (_, _) => -17,
        };
        var options = definition.Capture();
        Assert.Equal(17, options.CompareAscending!(left, right));
        Assert.Same(left, observedLeft);
        Assert.Same(right, observedRight);
        Assert.Equal(-17, options.CompareDescending!(left, right));
        definition.CompareAscending = (_, _) => 23;
        definition.CompareDescending = (_, _) => -23;
        Assert.Equal(23, options.CompareAscending(left, right));
        Assert.Equal(-23, options.CompareDescending(left, right));
    }

    [Fact]
    public void Initially_unconfigured_comparisons_remain_unconfigured_in_the_capture()
    {
        var definition = new Definition();
        var options = definition.Capture();
        definition.CompareAscending = (_, _) => 1;
        definition.CompareDescending = (_, _) => -1;
        Assert.Null(options.CompareAscending);
        Assert.Null(options.CompareDescending);
        Assert.NotNull(definition.Capture().CompareAscending);
    }

    private sealed class Definition : TreeDataGridTextColumn
    {
        public ColumnOptions<object> Capture() => CreateCommonOptions();
    }
}
