using System.ComponentModel;
using Avalonia.Controls;
using TreeDataGridDemo.Internal.TreeDataGrid;
using Xunit;

namespace TreeDataGridDemo.Tests;

public class TreeDataGridSourceExtensionsTests
{
    [Fact]
    public void FindDisplayedRowIndex_UsesReferenceIdentityAndHandlesNull()
    {
        var displayed = new Item("Same value");
        var equalButDifferent = new Item("Same value");
        var source = new FlatTreeDataGridSource<Item>(new[] { displayed });

        Assert.Equal(0, source.FindDisplayedRowIndex(displayed));
        Assert.Equal(-1, source.FindDisplayedRowIndex(equalButDifferent));
        Assert.Equal(-1, source.FindDisplayedRowIndex(null));
    }

    [Fact]
    public void FindDisplayedRowIndex_TracksFilteringAndSorting()
    {
        var gamma = new Item("Gamma");
        var alpha = new Item("Alpha");
        var beta = new Item("Beta");
        var source = new FlatTreeDataGridSource<Item>(new[] { gamma, alpha, beta })
            .WithTextColumn("Name", x => x.Name);

        Assert.Equal(1, source.FindDisplayedRowIndex(alpha));

        source.Filter(x => !ReferenceEquals(x, gamma));

        Assert.Equal(-1, source.FindDisplayedRowIndex(gamma));
        Assert.Equal(0, source.FindDisplayedRowIndex(alpha));
        Assert.Equal(1, source.FindDisplayedRowIndex(beta));

        ((ITreeDataGridSource)source).SortBy(source.Columns[0], ListSortDirection.Descending);

        Assert.Equal(0, source.FindDisplayedRowIndex(beta));
        Assert.Equal(1, source.FindDisplayedRowIndex(alpha));
    }

    private sealed record Item(string Name);
}
