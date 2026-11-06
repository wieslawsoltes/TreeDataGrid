using System;
using System.Globalization;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using UI = Uno.Controls.Models.TreeDataGrid;
using NativeLength = Microsoft.UI.Xaml.GridLength;
using NativeUnit = Microsoft.UI.Xaml.GridUnitType;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class ColumnCompatibilityTests
{
    [Fact]
    public void Typed_factory_accepts_public_UI_column_and_retains_source_metadata()
    {
        using var source = new FlatTreeDataGridSource<Item>([new() { Number = 42 }]);
        source.Columns.Add(new TextColumn<Item, int>("Core", x => x.Number, width: new(2, GridUnitType.Star)) { PresentationKey = "Number" });
        var options = new TreeDataGridPresentationOptions<Item>();
        Func<IColumn<Item>, ICellColumn<Item>> factory = _ => new UI.TextColumn<Item, int>(
            "Display", x => x.Number, (x, value) => x.Number = value, options: new()
            { StringFormat = "N={0}", Culture = CultureInfo.InvariantCulture, BeginEditGestures = UI.BeginEditGestures.Tap });
        options.Columns["Number"] = factory;
        using var view = TreeDataGridPresentation.Create(source, options);
        var column = view.NativeColumns[0];
        Assert.Same(source.Columns[0], column.Model);
        Assert.Equal("Display", column.Header);
        Assert.Equal(new NativeLength(2, NativeUnit.Star), column.Width);
        using var cell = view.RealizeCell(0, 0);
        Assert.Equal("N=42", column.FormatValue(cell.Value));
        Assert.Equal("N=42", Assert.IsAssignableFrom<UI.ITextCell>(cell).Text);
        Assert.Equal(UI.BeginEditGestures.Tap, ((UI.ICell)cell).EditGestures);
        cell.Write("51");
        Assert.Equal(51, ((Item)source.Rows[0].Model!).Number);
    }

    [Fact]
    public void UI_layout_contract_has_native_lengths_and_maximum_constraint_precedence()
    {
        using var column = new UI.TextColumn<Item, int>("Number", x => x.Number,
            width: new NativeLength(2, NativeUnit.Star), options: new() { MinWidth = new(80), MaxWidth = new(60) });
        UI.IUpdateColumnLayout layout = column;
        Assert.Equal(60, layout.MinActualWidth);
        layout.CalculateStarWidth(200, 2);
        Assert.True(layout.StarWidthWasConstrained);
        Assert.True(layout.CommitActualWidth());
        Assert.Equal(60, column.ActualWidth);
        Assert.False(layout.StarWidthWasConstrained);
        Assert.False(layout.CommitActualWidth());
    }

    [Fact]
    public void Public_reuse_contract_retargets_own_binding_but_rejects_another_column()
    {
        using var source = new FlatTreeDataGridSource<Item>([new() { Number = 10 }, new() { Number = 20 }]);
        using var column = new UI.TextColumn<Item, int>("Number", x => x.Number);
        using var other = new UI.TextColumn<Item, int>("Different", x => x.Number + 100);
        ICellColumn<Item> typed = column;
        var cell = typed.CreateCell((IRow<Item>)source.Rows[0]);
        try
        {
            Assert.False(other.TryReuseCell(cell, (IRow<Item>)source.Rows[1]));
            Assert.True(typed.TryReuseCell(cell, (IRow<Item>)source.Rows[1]));
            Assert.Equal(20, cell.Value);
        }
        finally { (cell as IDisposable)?.Dispose(); }
    }

    [Fact]
    public void CheckBox_facades_keep_two_and_three_state_Core_binding_contracts()
    {
        var item = new Item();
        using var source = new FlatTreeDataGridSource<Item>([item]);
        using var two = new UI.CheckBoxColumn<Item>("Two", x => x.Checked, (x, value) => x.Checked = value);
        using var three = new UI.CheckBoxColumn<Item>("Three", x => x.NullableChecked, (x, value) => x.NullableChecked = value);
        Assert.False(two.IsThreeState);
        Assert.True(three.IsThreeState);
        using var twoCell = two.CreateCell(source.Rows[0]);
        using var threeCell = three.CreateCell(source.Rows[0]);
        twoCell.Write(true);
        threeCell.Write(null);
        Assert.True(item.Checked);
        Assert.Null(item.NullableChecked);
    }

    [Fact]
    public void Core_constructor_preserves_constraints_unless_view_options_override_them()
    {
        var core = new ValueColumn<Item, int>("Number", x => x.Number, options: new() { MinWidth = new(90), MaxWidth = new(120) });
        using var defaults = new UI.TextColumn<Item, int>(core);
        using var overridden = new UI.TextColumn<Item, int>(core, new() { MinWidth = new(40), MaxWidth = new(80) });
        Assert.Equal(90, defaults.MinimumWidth);
        Assert.Equal(120, defaults.MaximumWidth);
        Assert.Equal(40, overridden.MinimumWidth);
        Assert.Equal(80, overridden.MaximumWidth);
        Assert.Equal(90, core.Options.MinWidth.Value);
    }

    [Fact]
    public void View_metadata_notifications_suspend_and_dispose_with_presentation()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        source.Columns.Add(new TextColumn<Item, int>("Number", x => x.Number));
        using var view = TreeDataGridPresentation.Create(source);
        var column = view.NativeColumns[0];
        var changes = 0;
        view.ColumnsChanged += (_, _) => ++changes;
        column.Header = "Renamed";
        Assert.Equal(1, changes);
        view.Suspend();
        column.Header = "Detached";
        Assert.Equal(1, changes);
        view.Resume();
        changes = 0;
        column.Header = "Resumed";
        Assert.Equal(1, changes);
        view.Dispose();
        column.Header = "Disposed";
        Assert.Equal(1, changes);
    }

    private sealed class Item
    {
        public int Number { get; set; }
        public bool Checked { get; set; }
        public bool? NullableChecked { get; set; }
    }
}
