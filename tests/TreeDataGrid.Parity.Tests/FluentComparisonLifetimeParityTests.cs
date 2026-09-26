using System;
using System.ComponentModel;
using Xunit;
using A = Avalonia.Controls;
using U = Uno.Controls;

namespace TreeDataGrid.Parity.Tests;

public sealed class FluentComparisonLifetimeParityTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    [InlineData(3, false)] [InlineData(3, true)]
    public void Public_factories_keep_reference_callback_lifetime(int kind, bool descending)
    {
        var a = new Item(1); var b = new Item(2);
        using var reference = new A.FlatTreeDataGridSource<Item>(new[] { a, b });
        using var native = new TreeDataGridCore.FlatTreeDataGridSource<Item>(new[] { a, b });
        A.ColumnCreateOptions? ao = null;
        U.ColumnCreateOptions? uo = null;
        Add(reference, native, kind,
            x => { ao = x; x.CompareAscending = x.CompareDescending = static (_, _) => 7; },
            x => { uo = x; x.CompareAscending = x.CompareDescending = static (_, _) => 7; });
        var direction = descending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        var expected = reference.Columns[0].GetComparison(direction)!;
        var actual = native.Columns[0].GetComparison(direction)!;
        Assert.Equal(7, expected(a, b)); Assert.Equal(7, actual(a, b));
        ao!.CompareAscending = ao.CompareDescending = static (_, _) => 29;
        uo!.CompareAscending = uo.CompareDescending = static (_, _) => 29;
        Assert.Equal(29, expected(a, b)); Assert.Equal(expected(a, b), actual(a, b));
        var failure = new InvalidOperationException("current comparer");
        ao.CompareAscending = ao.CompareDescending = (_, _) => throw failure;
        uo.CompareAscending = uo.CompareDescending = (_, _) => throw failure;
        Assert.Same(failure, Record.Exception(() => expected(a, b)));
        Assert.Same(failure, Record.Exception(() => actual(a, b)));
        ao.CompareAscending = ao.CompareDescending = null;
        uo.CompareAscending = uo.CompareDescending = null;
        Assert.IsType<NullReferenceException>(Record.Exception(() => expected(a, b)));
        Assert.IsType<NullReferenceException>(Record.Exception(() => actual(a, b)));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Initially_absent_callbacks_do_not_join_an_existing_factory_snapshot(int kind)
    {
        var a = new Item(1); var b = new Item(2);
        using var reference = new A.FlatTreeDataGridSource<Item>(new[] { a, b });
        using var native = new TreeDataGridCore.FlatTreeDataGridSource<Item>(new[] { a, b });
        A.ColumnCreateOptions? ao = null; U.ColumnCreateOptions? uo = null;
        Add(reference, native, kind, x => ao = x, x => uo = x);
        var expected = reference.Columns[0].GetComparison(ListSortDirection.Ascending);
        var actual = native.Columns[0].GetComparison(ListSortDirection.Ascending);
        ao!.CompareAscending = static (_, _) => 99;
        uo!.CompareAscending = static (_, _) => 99;
        Assert.Equal(expected is null, actual is null);
        if (expected is not null) Assert.Equal(Math.Sign(expected(a, b)), Math.Sign(actual!(a, b)));
        Assert.NotEqual(99, actual?.Invoke(a, b));
    }

    private static void Add(A.FlatTreeDataGridSource<Item> a, TreeDataGridCore.FlatTreeDataGridSource<Item> u,
        int kind, Action<A.ColumnCreateOptions> ac, Action<U.ColumnCreateOptions> uc)
    {
        switch (kind)
        {
            case 0:
                A.TreeDataGridSourceExtensions.WithTextColumn<Item, int>(a, "Value", x => x.Value, x => { x.IsReadOnly = true; ac(x); });
                U.TreeDataGridSourceExtensions.WithTextColumn<Item, int>(u, "Value", x => x.Value, x => { x.IsReadOnly = true; uc(x); });
                break;
            case 1:
                A.TreeDataGridSourceExtensions.WithCheckBoxColumn(a, "Flag", x => x.Flag, x => { x.IsReadOnly = true; ac(x); });
                U.TreeDataGridSourceExtensions.WithCheckBoxColumn(u, "Flag", x => x.Flag, x => { x.IsReadOnly = true; uc(x); });
                break;
            case 2:
                A.TreeDataGridSourceExtensions.WithThreeStateCheckBoxColumn(a, "Flag", x => x.NullableFlag, x => { x.IsReadOnly = true; ac(x); });
                U.TreeDataGridSourceExtensions.WithThreeStateCheckBoxColumn(u, "Flag", x => x.NullableFlag, x => { x.IsReadOnly = true; uc(x); });
                break;
            default:
                A.TreeDataGridSourceExtensions.WithTemplateColumnFromResourceKeys(a, "Template", "Display", configure: x => ac(x));
                U.TreeDataGridSourceExtensions.WithTemplateColumnFromResourceKeys(u, "Template", "Display", configure: x => uc(x));
                break;
        }
    }
    private sealed record Item(int Value)
    {
        public bool Flag => Value > 1;
        public bool? NullableFlag => Flag;
    }
}
