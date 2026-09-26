using System;
using System.Collections.Generic;
using System.ComponentModel;
using TreeDataGridCore;
using Xunit;
#if TREEDATAGRID_AVALONIA_CONTRACT_TESTS
using View = Avalonia.Controls.Presentation;
using UI = Avalonia.Controls.Models.TreeDataGrid;
using GridLength = Avalonia.Controls.GridLength;
using GridUnitType = Avalonia.Controls.GridUnitType;
#else
using View = Uno.Controls.Presentation;
using UI = Uno.Controls.Models.TreeDataGrid;
using GridLength = Microsoft.UI.Xaml.GridLength;
using GridUnitType = Microsoft.UI.Xaml.GridUnitType;
#endif

namespace TreeDataGrid.SharedContract.Tests;

// This exact file is compiled against the original Avalonia and Uno classes.
// No golden inventory or hand-normalized API comparison substitutes for execution.
public sealed class CellColumnBaseCompatibilityTests
{
    [Fact]
    public void Default_options_and_unmeasured_state_match()
    {
        var options = new View.CellColumnOptions();
        var column = new Probe("Name", null, options);
        Assert.Same(options, column.Options);
        Assert.Null(((UI.IColumn)column).CanUserResize);
        Assert.True(column.Width.IsAuto);
        Assert.True(double.IsNaN(column.ActualWidth));
        Assert.Equal(new GridLength(30), options.MinWidth);
        Assert.Null(options.MaxWidth);
        Assert.True(((UI.IColumnMeasurementOptions)column).RequiresUnconstrainedWidthMeasurement);
    }

    [Fact]
    public void Auto_width_initializes_from_minimum_and_grows_monotonically()
    {
        var column = new Probe();
        var layout = (UI.IUpdateColumnLayout)column;
        Assert.True(layout.CommitActualWidth());
        Assert.Equal(30d, column.ActualWidth);
        Assert.False(layout.CommitActualWidth());
        Assert.Equal(80d, layout.CellMeasured(80, 0));
        Assert.True(layout.CommitActualWidth());
        Assert.Equal(80d, layout.CellMeasured(10, 1));
        Assert.False(layout.CommitActualWidth());
        Assert.Equal(80d, column.ActualWidth);
    }

    [Fact]
    public void Pixel_width_is_immediate_but_coerced_when_committed()
    {
        var column = new Probe(width: new GridLength(10));
        var layout = (UI.IUpdateColumnLayout)column;
        Assert.Equal(10d, column.ActualWidth);
        Assert.Equal(10d, layout.CellMeasured(80, 0));
        Assert.True(layout.CommitActualWidth());
        Assert.Equal(30d, column.ActualWidth);
        layout.SetWidth(new GridLength(95));
        Assert.Equal(95d, column.ActualWidth);
        Assert.False(layout.CommitActualWidth());
    }

    [Fact]
    public void Star_measurement_uses_minimum_until_distribution()
    {
        var column = new Probe(width: new GridLength(2, GridUnitType.Star));
        var layout = (UI.IUpdateColumnLayout)column;
        Assert.Equal(30d, layout.CellMeasured(100, 0));
        layout.CalculateStarWidth(240, 3);
        Assert.False(layout.StarWidthWasConstrained);
        Assert.True(layout.CommitActualWidth());
        Assert.Equal(160d, column.ActualWidth);
        Assert.Equal(160d, layout.CellMeasured(200, 1));
    }

    [Theory]
    [InlineData(30, 100, 10, 30)]
    [InlineData(30, 100, 400, 100)]
    [InlineData(100, 50, 80, 50)]
    public void Maximum_constraint_wins_and_star_constraint_flag_resets(double min, double max, double space, double expected)
    {
        var column = new Probe(width: new GridLength(1, GridUnitType.Star),
            options: new() { MinWidth = new(min), MaxWidth = new(max) });
        var layout = (UI.IUpdateColumnLayout)column;
        layout.CalculateStarWidth(space, 1);
        Assert.True(layout.StarWidthWasConstrained);
        Assert.True(layout.CommitActualWidth());
        Assert.Equal(expected, column.ActualWidth);
        Assert.False(layout.StarWidthWasConstrained);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Auto_constraints_initialize_from_natural_measurements(bool minAuto, bool maxAuto)
    {
        var options = new View.CellColumnOptions
        {
            MinWidth = minAuto ? GridLength.Auto : new(30),
            // Uno also converts string to GridLength. Without an explicit
            // nullable operand the null arm can bind to that conversion.
            MaxWidth = maxAuto ? (GridLength?)GridLength.Auto : null,
        };
        var column = new Probe(options: options);
        var layout = (UI.IUpdateColumnLayout)column;
        Assert.Equal(75d, layout.CellMeasured(75, 0));
        layout.CommitActualWidth();
        Assert.Equal(75d, column.ActualWidth);
        Assert.Equal(minAuto ? 75 : 30, layout.MinActualWidth);
        Assert.Equal(maxAuto ? 75 : double.PositiveInfinity, layout.MaxActualWidth);
    }

    [Fact]
    public void Repeated_unmeasured_star_commit_does_not_request_endless_layout()
    {
        var column = new Probe(width: new GridLength(1, GridUnitType.Star));
        var layout = (UI.IUpdateColumnLayout)column;
        Assert.False(layout.CommitActualWidth());
        Assert.False(layout.CommitActualWidth());
        Assert.True(double.IsNaN(column.ActualWidth));
    }

    [Fact]
    public void Changing_width_mode_retains_discovered_auto_measurement()
    {
        var column = new Probe();
        var layout = (UI.IUpdateColumnLayout)column;
        layout.CellMeasured(88, 0);
        layout.CommitActualWidth();
        layout.SetWidth(new GridLength(200));
        Assert.Equal(200d, column.ActualWidth);
        layout.SetWidth(GridLength.Auto);
        Assert.Equal(200d, column.ActualWidth);
        Assert.True(layout.CommitActualWidth());
        Assert.Equal(88d, column.ActualWidth);
    }

    [Fact]
    public void Header_sort_and_actual_width_publish_once_while_tag_is_plain_state()
    {
        var column = new Probe();
        var names = new List<string?>();
        column.PropertyChanged += (_, args) => names.Add(args.PropertyName);
        column.Header = "Updated";
        column.Header = "Updated";
        column.SortDirection = ListSortDirection.Descending;
        column.SortDirection = ListSortDirection.Descending;
        column.Tag = new object();
        ((UI.IUpdateColumnLayout)column).SetWidth(new GridLength(70));
        Assert.Equal(new[] { "Header", "SortDirection", "ActualWidth" }, names);
    }

    [Fact]
    public void Mutating_options_is_observed_by_the_public_layout_contract()
    {
        var column = new Probe(width: new GridLength(100));
        var layout = (UI.IUpdateColumnLayout)column;
        column.Options.CanUserResizeColumn = false;
        column.Options.MaxWidth = new GridLength(50);
        Assert.False(((UI.IColumn)column).CanUserResize);
        Assert.True(layout.CommitActualWidth());
        Assert.Equal(50d, column.ActualWidth);
        column.Options.MinWidth = GridLength.Auto;
        Assert.True(((UI.IColumnMeasurementOptions)column).RequiresUnconstrainedWidthMeasurement);
    }

    [Fact]
    public void Custom_cell_factory_receives_the_actual_shared_Core_row()
    {
        using var source = new FlatTreeDataGridSource<Item>(new[] { new Item() });
        var row = Assert.IsAssignableFrom<TreeDataGridCore.Models.IRow<Item>>(source.Rows[0]);
        var column = new Probe();
        Assert.Throws<FactoryReachedException>(() => column.CreateCell(row));
        Assert.Same(row, column.LastRow);
    }

    [Fact]
    public void Star_calculation_rejects_nonstar_columns()
    {
        var layout = (UI.IUpdateColumnLayout)new Probe();
        Assert.Throws<InvalidOperationException>(() => layout.CalculateStarWidth(100, 1));
    }

    private sealed class Item;
    private sealed class FactoryReachedException : Exception;
    private sealed class Probe : View.CellColumnBase<Item>
    {
        public Probe(object? header = null, GridLength? width = null, View.CellColumnOptions? options = null)
            : base(header, width, options ?? new()) { }
        public TreeDataGridCore.Models.IRow<Item>? LastRow { get; private set; }
        public override UI.ICell CreateCell(TreeDataGridCore.Models.IRow<Item> row)
        {
            LastRow = row;
            throw new FactoryReachedException();
        }
    }
}
