using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using Windows.Foundation;
using Xunit;
using GridLength = Microsoft.UI.Xaml.GridLength;
using GridUnitType = Microsoft.UI.Xaml.GridUnitType;

namespace TreeDataGrid.Uno.Tests;

public class ColumnListCompatibilityTests
{
    [Fact]
    public void Public_presentation_and_row_column_contract_uses_actual_view_columns()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("Name")]);
        source.Columns.Add(new TreeDataGridCore.Models.TextColumn<Item, string>("Name", x => x.Name, width: new(100)));
        source.Columns.Add(new TreeDataGridCore.Models.TextColumn<Item, string>("Hidden", x => x.Name) { IsVisible = false });
        using var presentation = TreeDataGridPresentation.Create(source);
        IColumns columns = presentation.Columns;
        Assert.Same(presentation.NativeColumns[0], columns[0]);
        Assert.Single(columns);
        columns.ViewportChanged(new Rect(0, 0, 500, 200));
        columns.CommitActualWidths();
        Assert.Equal(100, columns[0].ActualWidth);
        columns.SetColumnWidth(0, new GridLength(180));
        Assert.Equal(180, source.Columns[0].Width.Value);
        Assert.Equal((0, 0d), columns.GetColumnAt(179));
        Assert.Equal((-1, -1d), columns.GetColumnAt(180));
        var notifications = 0;
        columns.CollectionChanged += (_, e) =>
        {
            Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
            ++notifications;
            Assert.Equal(columns.Count, presentation.NativeColumns.Count);
        };
        source.Columns[1].IsVisible = true;
        Assert.Equal(1, notifications);
        Assert.Equal(2, columns.Count);
        source.Columns.Move(1, 0);
        Assert.Equal(2, notifications);
        Assert.Same(source.Columns[1], ((CellColumn)columns[1]).Model);
    }

    [Fact]
    public void Standalone_columns_measure_commit_stars_and_update_position_cache()
    {
        var fixedColumn = new Column(new GridLength(100));
        var auto = new Column(GridLength.Auto);
        var star = new Column(new GridLength(1, GridUnitType.Star));
        var columns = new ColumnListBase<Column>();
        columns.AddRange([fixedColumn, auto, star]);
        IColumns layout = columns;
        layout.ViewportChanged(new Rect(0, 0, 500, 100));
        Assert.Equal(new Size(80, 30), layout.CellMeasured(1, 0, new Size(80, 30)));
        layout.CommitActualWidths();
        Assert.Equal(100, fixedColumn.ActualWidth);
        Assert.Equal(80, auto.ActualWidth);
        Assert.Equal(320, star.ActualWidth);
        Assert.Equal((1, 100d), layout.GetColumnAt(100));
        Assert.Equal((2, 180d), layout.GetColumnAt(180));
        Assert.Equal((-1, -1d), layout.GetColumnAt(500));
        Assert.Equal(500, layout.GetEstimatedWidth(500));
        layout.SetColumnWidth(0, new GridLength(120));
        Assert.Equal((1, 120d), layout.GetColumnAt(120));
        Assert.Equal(300, star.ActualWidth);
        layout.ViewportChanged(new Rect(0, 0, 600, 100));
        Assert.Equal(400, star.ActualWidth);
        columns.Clear();
        Assert.Equal(0, fixedColumn.Subscribers);
        Assert.Equal(0, auto.Subscribers);
        Assert.Equal(0, star.Subscribers);
    }

    [Fact]
    public void Shared_column_entries_keep_one_owned_subscription_until_final_removal()
    {
        var column = new Column(new GridLength(40));
        var columns = new ColumnListBase<Column>();
        columns.AddRange([column, column]);
        Assert.Equal(1, column.Subscribers);
        columns.CommitActualWidths();
        Assert.Equal((1, 40d), columns.GetColumnAt(40));
        columns.RemoveAt(0);
        Assert.Equal(1, column.Subscribers);
        columns.SetColumnWidth(0, new GridLength(60));
        Assert.Equal((-1, -1d), columns.GetColumnAt(60));
        columns.RemoveAt(0);
        Assert.Equal(0, column.Subscribers);
    }

    [Fact]
    public void Width_batch_defers_commits_and_preserves_final_measure_request()
    {
        var column = new Column(GridLength.Auto);
        var columns = new ColumnListBase<Column> { column };
        var batch = (IColumnLayoutBatch)columns;
        batch.BeginActualWidthBatch();
        batch.BeginActualWidthBatch();
        columns.CellMeasured(0, 0, new Size(120, 20));
        columns.CommitActualWidths();
        Assert.True(double.IsNaN(column.ActualWidth));
        batch.RequestFinalMeasure();
        Assert.False(batch.EndActualWidthBatch());
        Assert.True(batch.EndActualWidthBatch());
        Assert.Equal(120, column.ActualWidth);
        Assert.False(batch.IsActualWidthCommitDeferred);
        Assert.Throws<InvalidOperationException>(() => batch.EndActualWidthBatch());
        columns.Clear();
    }

    [Fact]
    public void Rejected_reentrant_changes_do_not_mutate_subscription_ownership()
    {
        var column = new Column(new GridLength(40));
        var rejected = new Column(new GridLength(80));
        var columns = new ColumnListBase<Column>();
        void Changing(object? sender, NotifyCollectionChangedEventArgs args)
        {
            Assert.Throws<InvalidOperationException>(() => columns.Add(rejected));
            Assert.Throws<InvalidOperationException>(() => columns.Clear());
        }
        void Second(object? sender, NotifyCollectionChangedEventArgs args) { }
        columns.CollectionChanged += Changing;
        columns.CollectionChanged += Second;
        columns.Add(column);
        Assert.Single(columns);
        Assert.Equal(1, column.Subscribers);
        Assert.Equal(0, rejected.Subscribers);
        columns.CollectionChanged -= Changing;
        columns.CollectionChanged -= Second;
        columns.Clear();
        Assert.Equal(0, column.Subscribers);
    }

    private sealed record Item(string Name);
    private sealed class Column(GridLength width) : IUpdateColumnLayout
    {
        private PropertyChangedEventHandler? _changed;
        private double _measured;
        private double _star;
        public int Subscribers { get; private set; }
        public double ActualWidth { get; private set; } = double.NaN;
        public bool? CanUserResize => true;
        public object? Header => null;
        public GridLength Width { get; private set; } = width;
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public double CellMeasured(double measured, int rowIndex) { _measured = Math.Max(_measured, measured); return _measured; }
        public void CalculateStarWidth(double availableWidth, double totalStars) => _star = availableWidth * Width.Value / totalStars;
        public bool CommitActualWidth()
        {
            var next = Width.IsAuto ? _measured : Width.IsStar ? _star : Width.Value;
            if (next.Equals(ActualWidth)) return false;
            ActualWidth = next;
            _changed?.Invoke(this, new(nameof(ActualWidth)));
            return true;
        }
        public void SetWidth(GridLength value) => Width = value;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
}
