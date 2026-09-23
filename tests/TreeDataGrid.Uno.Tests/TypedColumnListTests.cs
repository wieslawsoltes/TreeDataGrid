using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Xunit;
using global::Uno.Controls.Models.TreeDataGrid;
using global::Uno.Controls.Presentation;
using Core = TreeDataGridCore.Models;

namespace TreeDataGrid.Uno.Tests;

public sealed class TypedColumnListTests
{
    [Fact]
    public void Native_columns_and_custom_columns_share_typed_collection_and_layout_contracts()
    {
        var text = new TextColumn<Model, string>("Name", model => model.Name, width: new GridLength(90));
        var check = new CheckBoxColumn<Model>("Checked", model => model.Checked, width: new GridLength(40));
        var custom = new CustomColumn("Custom", new GridLength(70));
        var columns = new ColumnList<Model> { text, check, custom };
        Assert.IsAssignableFrom<IColumns>(columns);
        Assert.IsAssignableFrom<IReadOnlyList<ICellColumn<Model>>>(columns);
        Assert.Same(text, columns[0]);
        Assert.Same(check, columns[1]);
        Assert.Same(custom, columns[2]);
        columns.ViewportChanged(new Rect(0, 0, 200, 100));
        columns.CommitActualWidths();
        Assert.Equal(90d, columns[0].ActualWidth);
        Assert.Equal(40d, columns[1].ActualWidth);
        Assert.Equal(70d, columns[2].ActualWidth);
        columns.Clear();
        Assert.Equal(0, custom.Disposals);
        text.Dispose(); check.Dispose(); custom.Dispose();
    }

    [Fact]
    public void Pixel_auto_and_star_columns_share_the_existing_measurement_solver()
    {
        var pixel = new CustomColumn("Pixel", new GridLength(60));
        var one = new CustomColumn("One", new GridLength(1, GridUnitType.Star));
        var two = new CustomColumn("Two", new GridLength(2, GridUnitType.Star));
        var columns = new ColumnList<Model> { pixel, one, two };
        try
        {
            columns.ViewportChanged(new Rect(0, 0, 300, 100));
            columns.CommitActualWidths();
            Assert.Equal(60d, pixel.ActualWidth);
            Assert.Equal(80d, one.ActualWidth);
            Assert.Equal(160d, two.ActualWidth);
            Assert.Equal((1, 60d), columns.GetColumnAt(60));
            Assert.Equal((2, 140d), columns.GetColumnAt(140));
            columns.SetColumnWidth(0, GridLength.Auto);
            columns.CellMeasured(0, 0, new Size(90, 28));
            columns.CommitActualWidths();
            Assert.Equal(90d, pixel.ActualWidth);
            Assert.Equal(70d, one.ActualWidth);
            Assert.Equal(140d, two.ActualWidth);
        }
        finally { columns.Clear(); }
    }

    [Fact]
    public void Collection_changes_keep_precise_notifications_and_column_identity()
    {
        var first = new CustomColumn("First", new GridLength(80));
        var second = new CustomColumn("Second", new GridLength(90));
        var replacement = new CustomColumn("Replacement", new GridLength(100));
        var columns = new ColumnList<Model>();
        var actions = new List<NotifyCollectionChangedAction>();
        columns.CollectionChanged += (_, args) => actions.Add(args.Action);
        columns.Add(first);
        columns.Add(second);
        columns[0] = replacement;
        columns.RemoveAt(1);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add,
            NotifyCollectionChangedAction.Replace, NotifyCollectionChangedAction.Remove }, actions);
        Assert.Same(replacement, Assert.Single(columns));
        Assert.Equal(0, first.Disposals);
        Assert.Equal(0, second.Disposals);
        columns.Clear();
    }

    [Fact]
    public void Typed_cell_creation_uses_the_supplied_Core_row_without_copying_it()
    {
        var custom = new CustomColumn("Custom", new GridLength(100));
        var columns = new ColumnList<Model> { custom };
        var model = new Model { Name = "Shared", Checked = true };
        var row = new Row(model);
        var created = columns[0].CreateCell(row);
        Assert.Same(row, custom.LastRow);
        Assert.Equal("Shared", Assert.IsType<CustomCell>(created).Value);
        columns.Clear();
        Assert.Equal(0, custom.Disposals);
        ((IDisposable)created).Dispose();
        custom.Dispose();
        Assert.Equal(1, custom.Disposals);
    }

    [Fact]
    public void Removed_columns_no_longer_invalidate_the_collection()
    {
        var custom = new CustomColumn("Custom", new GridLength(80));
        var columns = new ColumnList<Model> { custom };
        columns.ViewportChanged(new Rect(0, 0, 200, 100));
        columns.CommitActualWidths();
        columns.Clear();
        var changes = 0;
        columns.LayoutInvalidated += (_, _) => ++changes;
        custom.Header = "Detached";
        ((IUpdateColumnLayout)custom).SetWidth(new GridLength(120));
        Assert.Equal(0, changes);
        Assert.Empty(columns);
    }

    [Fact]
    public void Warm_typed_geometry_queries_do_not_allocate()
    {
        var columns = new ColumnList<Model>();
        for (var i = 0; i < 128; ++i) columns.Add(new CustomColumn(i, new GridLength(10)));
        try
        {
            columns.ViewportChanged(new Rect(0, 0, 1280, 100));
            columns.CommitActualWidths();
            for (var i = 0; i < 1024; ++i) columns.GetColumnAt(637);
            var sum = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 4096; ++i) sum += columns.GetColumnAt(637).index;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(4096 * 63, sum);
            Assert.Equal(0L, allocated);
        }
        finally { columns.Clear(); }
    }

    private sealed class Model { public string Name { get; init; } = "Name"; public bool Checked { get; init; } }
    private sealed class Row(Model model) : Core.IRow<Model>
    {
        public Model Model { get; } = model;
        object? Core.IRow.Model => Model;
        public object? Header => null;
        public TreeDataGridCore.GridLength Height { get; set; } = TreeDataGridCore.GridLength.Auto;
    }
    private sealed class CustomCell(string text) : CellValue
    {
        public override object? Value => text;
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new InvalidOperationException("Read only");
    }
    private sealed class CustomColumn(object? header, GridLength width)
        : CellColumnBase<Model>(header, width, new CellColumnOptions { MinWidth = new GridLength(0) }), IDisposable
    {
        internal Core.IRow<Model>? LastRow;
        internal int Disposals;
        public override ICell CreateCell(Core.IRow<Model> row) { LastRow = row; return new CustomCell(row.Model.Name); }
        public void Dispose() => ++Disposals;
    }
}
