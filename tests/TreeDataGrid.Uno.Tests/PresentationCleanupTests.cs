using System;
using System.Collections.Generic;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class PresentationCleanupTests
{
    [Fact]
    public void Throwing_column_disposal_does_not_skip_other_columns_or_repeat_on_reentry()
    {
        using var source = Source();
        var columns = new List<TrackedColumn>();
        var options = Options(columns);
        var view = TreeDataGridPresentation.Create(source, options);
        columns[0].OnDispose = () => { view.Dispose(); throw new InvalidOperationException("Column cleanup"); };
        Assert.Throws<InvalidOperationException>(() => view.Dispose());
        Assert.All(columns, column => Assert.Equal(1, column.Disposals));
        view.Dispose();
        Assert.All(columns, column => Assert.Equal(1, column.Disposals));
        Assert.Empty(view.NativeColumns);
    }

    [Fact]
    public void Throwing_pooled_cell_does_not_skip_other_cells_or_column_cleanup()
    {
        using var source = Source();
        var columns = new List<TrackedColumn>();
        var view = TreeDataGridPresentation.Create(source, Options(columns));
        var first = (TrackedCell)view.RealizeCell(0, 0);
        var second = (TrackedCell)view.RealizeCell(1, 0);
        first.ThrowOnDispose = true;
        view.RecycleCell(view.NativeColumns[0], first);
        view.RecycleCell(view.NativeColumns[1], second);
        Assert.Throws<InvalidOperationException>(() => view.Dispose());
        Assert.Equal(1, first.Disposals);
        Assert.Equal(1, second.Disposals);
        Assert.All(columns, column => Assert.Equal(1, column.Disposals));
    }

    [Fact]
    public void Suspend_removes_a_failing_pool_before_resume()
    {
        using var source = Source();
        var columns = new List<TrackedColumn>();
        using var view = TreeDataGridPresentation.Create(source, Options(columns));
        var cell = (TrackedCell)view.RealizeCell(0, 0);
        cell.ThrowOnDispose = true;
        view.RecycleCell(view.NativeColumns[0], cell);
        Assert.Throws<InvalidOperationException>(() => view.Suspend());
        view.Resume();
        using var fresh = view.RealizeCell(0, 0);
        Assert.NotSame(cell, fresh);
        Assert.Equal(1, cell.Disposals);
    }

    [Fact]
    public void Failed_pool_cleanup_disposes_newly_staged_columns_without_replacing_existing_views()
    {
        using var source = Source();
        var columns = new List<TrackedColumn>();
        using var view = TreeDataGridPresentation.Create(source, Options(columns));
        var original = view.NativeColumns[0];
        var cell = (TrackedCell)view.RealizeCell(0, 0);
        cell.ThrowOnDispose = true;
        view.RecycleCell(original, cell);
        Assert.Throws<InvalidOperationException>(() => source.Columns.Add(Column("Third")));
        Assert.Equal(3, columns.Count);
        Assert.Equal(1, columns[2].Disposals);
        Assert.Same(original, view.NativeColumns[0]);
        Assert.Equal(0, columns[0].Disposals);
        source.Columns.RemoveAt(2);
        Assert.Equal(2, view.NativeColumns.Count);
    }

    [Fact]
    public void Failed_cell_suspension_still_disposes_the_unpooled_value()
    {
        using var source = Source();
        var columns = new List<TrackedColumn>();
        using var view = TreeDataGridPresentation.Create(source, Options(columns));
        var cell = (TrackedCell)view.RealizeCell(0, 0);
        cell.OnSuspend = () => throw new InvalidOperationException("Cell suspension");
        Assert.Throws<InvalidOperationException>(() => view.RecycleCell(view.NativeColumns[0], cell));
        Assert.Equal(1, cell.Disposals);
        view.Dispose();
        Assert.Equal(1, cell.Disposals);
    }

    [Fact]
    public void Reentrant_presentation_suspension_does_not_publish_a_stale_pool_entry()
    {
        using var source = Source();
        var columns = new List<TrackedColumn>();
        using var view = TreeDataGridPresentation.Create(source, Options(columns));
        var cell = (TrackedCell)view.RealizeCell(0, 0);
        cell.OnSuspend = view.Suspend;
        view.RecycleCell(view.NativeColumns[0], cell);
        Assert.Equal(1, cell.Disposals);
        view.Resume();
        using var next = view.RealizeCell(0, 0);
        Assert.NotSame(cell, next);
    }

    private static FlatTreeDataGridSource<Item> Source()
    {
        var source = new FlatTreeDataGridSource<Item>([new()]);
        source.Columns.Add(Column("First"));
        source.Columns.Add(Column("Second"));
        return source;
    }
    private static ValueColumn<Item, string> Column(string name) => new(name, _ => name) { PresentationKey = "Custom" };
    private static TreeDataGridPresentationOptions Options(List<TrackedColumn> columns)
    {
        var options = new TreeDataGridPresentationOptions();
        options.Columns["Custom"] = model => { var column = new TrackedColumn(model); columns.Add(column); return column; };
        return options;
    }
    private sealed class Item { }
    private sealed class TrackedColumn(IColumn model) : CellColumn(model)
    {
        internal int Disposals;
        internal Action? OnDispose;
        public override CellValue CreateCell(IRow row) => new TrackedCell();
        public override void Dispose() { ++Disposals; OnDispose?.Invoke(); }
    }
    private sealed class TrackedCell : CellValue
    {
        internal int Disposals;
        internal bool ThrowOnDispose;
        internal Action? OnSuspend;
        public override object? Value => "value";
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new NotSupportedException();
        internal override bool TrySuspend() { OnSuspend?.Invoke(); return true; }
        public override void Dispose() { ++Disposals; if (ThrowOnDispose) throw new InvalidOperationException("Cell cleanup"); }
    }
}
