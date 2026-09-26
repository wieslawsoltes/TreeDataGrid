using System;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;
using UI = global::Uno.Controls.Models.TreeDataGrid;
using NativeLength = Microsoft.UI.Xaml.GridLength;

namespace TreeDataGrid.Uno.Tests;

public sealed class CustomColumnDiscoveryTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Native_adapter_discovers_Auto_constraints_without_changing_the_public_unmeasured_contract(bool minimumAuto, bool maximumAuto)
    {
        var column = new Probe(new CellColumnOptions
        {
            MinWidth = minimumAuto ? NativeLength.Auto : new NativeLength(30),
            MaxWidth = maximumAuto ? (NativeLength?)NativeLength.Auto : new NativeLength(200),
        });
        var layout = (UI.IUpdateColumnLayout)column;
        Assert.True(double.IsNaN(layout.MinActualWidth));
        Assert.True(double.IsNaN(layout.MaxActualWidth));
        using var source = new FlatTreeDataGridSource<Item>(new[] { new Item() });
        source.Columns.Add(new TextColumn<Item, string>("Name", _ => "") { PresentationKey = "Probe" });
        var options = new TreeDataGridPresentationOptions<Item>();
        options.Columns.Add("Probe", _ => column);
        using var presentation = TreeDataGridPresentation.Create(source, options);
        var view = Assert.Single(presentation.NativeColumns);
        var widths = ColumnWidths.Calculate(presentation.NativeColumns, 300);
        Assert.True(widths[0] > 0 && double.IsFinite(widths[0]));
        Assert.True(double.IsNaN(layout.MinActualWidth));
        view.RecordWidth(80, 0);
        widths = ColumnWidths.Calculate(presentation.NativeColumns, 300);
        Assert.Equal(80d, widths[0]);
        view.SetActualWidth(widths[0]);
        Assert.Equal(80d, column.ActualWidth);
        Assert.Equal(minimumAuto ? 80d : 30d, view.MinimumWidth);
        Assert.Equal(maximumAuto ? 80d : 200d, view.MaximumWidth);
    }

    private sealed class Item;
    private sealed class Probe(CellColumnOptions options) : CellColumnBase<Item>("Name", null, options)
    {
        public override UI.ICell CreateCell(IRow<Item> row) => throw new InvalidOperationException("Geometry does not create cells.");
    }
}
