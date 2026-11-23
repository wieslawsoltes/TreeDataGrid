using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnGeometrySnapshotTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(300)]
    public void Commit_uses_one_validated_snapshot(int count)
    {
        var geometry = new ColumnGeometry();
        var widths = new ObservedWidths(count, static (_, reads) => reads == 1 ? 10d : double.NaN);
        Assert.True(geometry.Commit(widths));
        Assert.Equal(count, geometry.Count);
        Assert.Equal(count * 10d, geometry.TotalWidth);
        Assert.Equal(1, widths.CountReads);
        Assert.All(widths.ElementReads, reads => Assert.Equal(1, reads));
        for (var index = 0; index < count; ++index)
        {
            Assert.Equal(index * 10d, geometry.Start(index));
            Assert.Equal(10d, geometry.Width(index));
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1d)]
    public void Invalid_snapshot_preserves_committed_geometry(double invalid)
    {
        var geometry = CreateGeometry();
        var widths = new ObservedWidths(300, (index, _) => index == 17 ? invalid : 10d);
        Assert.Throws<ArgumentOutOfRangeException>(() => geometry.Commit(widths));
        AssertOriginalGeometry(geometry);
    }

    [Fact]
    public void Throwing_indexer_preserves_committed_geometry()
    {
        var geometry = CreateGeometry();
        var failure = new InvalidOperationException("Fixture indexer failure.");
        var widths = new ObservedWidths(300, (index, _) => index == 17 ? throw failure : 10d);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => geometry.Commit(widths)));
        AssertOriginalGeometry(geometry);
    }

    [Fact]
    public void Overflowing_snapshot_preserves_committed_geometry()
    {
        var geometry = CreateGeometry();
        var widths = new ObservedWidths(2, static (_, _) => double.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => geometry.Commit(widths));
        AssertOriginalGeometry(geometry);
    }

    [Fact]
    public void Nested_geometry_commits_do_not_share_snapshot_storage()
    {
        var outer = new ColumnGeometry();
        var inner = new ColumnGeometry();
        var widths = new ObservedWidths(300, (index, _) =>
        {
            if (index == 151) inner.Commit(new ObservedWidths(300, static (_, _) => 7d));
            return 3d;
        });
        outer.Commit(widths);
        Assert.Equal(900d, outer.TotalWidth);
        Assert.Equal(2100d, inner.TotalWidth);
        Assert.Equal(3d, outer.Width(299));
        Assert.Equal(7d, inner.Width(299));
    }

    [Fact]
    public void Caller_storage_is_not_retained()
    {
        var geometry = new ColumnGeometry();
        var widths = new[] { 10d, 20d };
        geometry.Commit(widths);
        Array.Fill(widths, double.NaN);
        Assert.Equal(30d, geometry.TotalWidth);
        Assert.Equal(20d, geometry.Width(1));
        Assert.False(geometry.Commit(new ObservedWidths(2, static (index, _) => (index + 1) * 10d)));
    }

    [Fact]
    public void Warm_array_commits_allocate_no_managed_storage()
    {
        var geometry = new ColumnGeometry();
        var widths = new[] { 10d, 20d, 30d };
        geometry.Commit(widths);
        for (var iteration = 0; iteration < 1024; ++iteration)
        {
            widths[1] = (iteration & 1) == 0 ? 20d : 21d;
            geometry.Commit(widths);
        }
        var changes = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 4096; ++iteration)
        {
            widths[1] = (iteration & 1) == 0 ? 20d : 21d;
            if (geometry.Commit(widths)) ++changes;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(4096, changes);
        Assert.Equal(61d, geometry.TotalWidth);
    }

    private static ColumnGeometry CreateGeometry()
    {
        var geometry = new ColumnGeometry();
        geometry.Commit(new[] { 20d, 30d });
        return geometry;
    }
    private static void AssertOriginalGeometry(ColumnGeometry geometry)
    {
        Assert.Equal(2, geometry.Count);
        Assert.Equal(50d, geometry.TotalWidth);
        Assert.Equal(0d, geometry.Start(0));
        Assert.Equal(20d, geometry.Start(1));
        Assert.Equal(20d, geometry.Width(0));
        Assert.Equal(30d, geometry.Width(1));
    }
    private sealed class ObservedWidths(int count, Func<int, int, double> read) : IReadOnlyList<double>
    {
        public int CountReads { get; private set; }
        public int[] ElementReads { get; } = new int[count];
        public int Count { get { ++CountReads; return count; } }
        public double this[int index] => read(index, ++ElementReads[index]);
        public IEnumerator<double> GetEnumerator()
        {
            for (var index = 0; index < count; ++index) yield return this[index];
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
