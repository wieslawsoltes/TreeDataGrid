using System;
using System.Collections.Specialized;
using System.Linq;
using TreeDataGridCore.Models;
using Xunit;

namespace TreeDataGridCore.Tests;

public class SortAllocationTests
{
    [Fact]
    public void Clearing_sort_does_not_allocate_a_snapshot_of_materialized_child_rows()
    {
        const int count = 4096;
        var models = Enumerable.Range(0, count).Select(_ => new Node()).ToArray();
        var model = new Node { Children = models };
        var column = new HierarchicalExpanderColumn<Node>(
            new TextColumn<Node, string>("Name", _ => "row"), node => node.Children);
        using var row = new HierarchicalRow<Node>(new Controller(), column, new IndexPath(0), model, null);
        row.IsExpanded = true;
        var rows = Assert.IsAssignableFrom<SortableRowsBase<Node, HierarchicalRow<Node>>>(row.Children);
        Assert.Equal(count, rows.Count);
        Assert.Same(models[0], rows[0].Model);
        // Warm JIT and observers before measuring thread-local allocation.
        for (var i = 0; i < 16; ++i) rows.Sort(null);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; ++i) rows.Sort(null);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 4096, $"Sort reset allocated {allocated:N0} bytes.");
        Assert.Same(models[0], rows[0].Model);
        Assert.Same(models[^1], rows[count - 1].Model);
    }
    private sealed class Node
    {
        internal Node[] Children { get; init; } = Array.Empty<Node>();
    }
    private sealed class Controller : IExpanderRowController<Node>
    {
        public void OnBeginExpandCollapse(IExpanderRow<Node> row) { }
        public void OnEndExpandCollapse(IExpanderRow<Node> row) { }
        public void OnChildCollectionChanged(IExpanderRow<Node> row, NotifyCollectionChangedEventArgs e) { }
    }
}
