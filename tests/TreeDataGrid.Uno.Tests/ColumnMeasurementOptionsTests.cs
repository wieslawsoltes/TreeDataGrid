using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public class ColumnMeasurementOptionsTests
{
    [Fact]
    public void BuiltIn_Columns_Expose_The_Optional_Measurement_Contract()
    {
        using var fixedColumn = new UI.TextColumn<Item, string>("Name", x => x.Name, width: new GridLength(90));
        using var autoColumn = new UI.TextColumn<Item, string>("Name", x => x.Name);
        Assert.False(((UI.IColumnMeasurementOptions)fixedColumn).RequiresUnconstrainedWidthMeasurement);
        Assert.True(((UI.IColumnMeasurementOptions)autoColumn).RequiresUnconstrainedWidthMeasurement);
    }

    [Fact]
    public void Custom_Column_Policy_Is_Forwarded_Live_Without_Copying_The_Core_Model()
    {
        var core = new ValueColumn<Item, string>("Name", x => x.Name);
        var custom = new PolicyColumn();
        using var adapter = new CellColumnAdapter<Item>(core, custom);
        Assert.Same(core, adapter.Model);
        Assert.False(adapter.RequiresUnconstrainedWidthMeasurement);
        custom.RequiresUnconstrainedWidthMeasurement = true;
        Assert.True(adapter.RequiresUnconstrainedWidthMeasurement);
        custom.RequiresUnconstrainedWidthMeasurement = false;
        Assert.False(adapter.RequiresUnconstrainedWidthMeasurement);
    }

    [Fact]
    public void Unannotated_Custom_Columns_Retain_Conservative_Natural_Measurement()
    {
        var core = new ValueColumn<Item, string>("Name", x => x.Name);
        using var adapter = new CellColumnAdapter<Item>(core, new CustomColumn());
        Assert.True(adapter.RequiresUnconstrainedWidthMeasurement);
    }

    private sealed record Item(string Name);
    private sealed class PolicyColumn : CustomColumn, UI.IColumnMeasurementOptions
    {
        public bool RequiresUnconstrainedWidthMeasurement { get; set; }
    }
    private class CustomColumn : ICellColumn<Item>
    {
        public double ActualWidth => 90;
        public bool? CanUserResize => true;
        public object? Header => "Name";
        public GridLength Width { get; private set; } = new(90);
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public double CellMeasured(double width, int rowIndex) => ActualWidth;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(GridLength width) => Width = width;
        public UI.ICell CreateCell(IRow<Item> row) => new UI.TextCell<string>(row.Model.Name);
    }
}
