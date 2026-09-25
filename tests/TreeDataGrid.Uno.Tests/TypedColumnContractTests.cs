using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Xunit;
using Core = TreeDataGridCore;
using P = Uno.Controls.Presentation;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class TypedColumnContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Built_in_columns_share_the_typed_view_contract_without_changing_core_ownership(int kind)
    {
        using P.CellColumn native = kind switch
        {
            0 => new U.TextColumn<Model, string>("Text", x => x.Text, (x, value) => x.Text = value),
            1 => new U.CheckBoxColumn<Model>("Flag", x => x.Flag, (x, value) => x.Flag = value),
            _ => new U.TemplateColumn<Model>("Template", (object)"Display"),
        };
        var typed = Assert.IsAssignableFrom<U.IColumn<Model>>(native);
        var old = Assert.IsAssignableFrom<P.ICellColumn<Model>>(native);
        var row = new Row(new());
        var value = typed.CreateCell(row);
        using var lifetime = Assert.IsAssignableFrom<IDisposable>(value);
        Assert.IsAssignableFrom<P.CellValue>(value);
        object? expected = kind switch { 0 => row.Model.Text, 1 => row.Model.Flag, _ => row.Model };
        Assert.Equal(expected, value.Value);
        Assert.Equal(native.Header, typed.Header);
        Assert.Equal(native.Width, typed.Width);
        Assert.Same(typed, old);
    }

    [Fact]
    public void Concrete_typed_factory_dispatches_existing_untyped_overrides_once()
    {
        using var column = new SpecializedColumn();
        var row = new Row(new());
        using var concrete = column.CreateCell(row);
        using var typed = Assert.IsAssignableFrom<IDisposable>(((U.IColumn<Model>)column).CreateCell(row));
        using var legacy = Assert.IsAssignableFrom<IDisposable>(((P.ICellColumn<Model>)column).CreateCell(row));
        Assert.Equal(3, column.Creates);
        Assert.Same(row, column.LastRow);
        Assert.Equal("override", concrete.Value);
    }

    [Theory]
    [InlineData(ListSortDirection.Ascending)]
    [InlineData(ListSortDirection.Descending)]
    [InlineData((ListSortDirection)17)]
    public void Typed_dispatch_preserves_virtual_comparison_and_direction(ListSortDirection direction)
    {
        using var column = new SpecializedColumn();
        var contract = (U.IColumn<Model>)column;
        Assert.Same(column.GetComparison(direction), contract.GetComparison(direction));
        Assert.Same(column.GetComparison(direction), ((P.ICellColumn<Model>)column).GetComparison(direction));
    }

    [Fact]
    public void Compatibility_base_comparison_is_not_hidden_by_the_legacy_default_interface_body()
    {
        var column = new CustomColumn();
        var typed = (U.IColumn<Model>)column;
        Assert.Same(column.Compare, typed.GetComparison(ListSortDirection.Ascending));
        Assert.Same(column.Compare, ((P.ICellColumn<Model>)column).GetComparison(ListSortDirection.Ascending));
        var row = new Row(new());
        using var cell = Assert.IsAssignableFrom<IDisposable>(typed.CreateCell(row));
        Assert.Same(row, column.LastRow);
        Assert.Equal(1, column.Creates);
    }

    [Fact]
    public void Legacy_explicit_cell_factory_remains_callable_through_both_contracts()
    {
        var column = new LegacyColumn();
        var typed = (U.IColumn<Model>)column;
        var row = new Row(new());
        using var first = Assert.IsAssignableFrom<IDisposable>(typed.CreateCell(row));
        using var second = Assert.IsAssignableFrom<IDisposable>(((P.ICellColumn<Model>)column).CreateCell(row));
        Assert.Equal(2, column.Creates);
        Assert.Same(row, column.LastRow);
        Assert.Null(typed.GetComparison(ListSortDirection.Ascending));
        Assert.Null(typed.GetComparison((ListSortDirection)17));
    }

    [Fact]
    public void Typed_column_list_covariance_preserves_actual_columns_and_layout()
    {
        using var first = new U.TextColumn<Model, string>("Text", x => x.Text, width: new GridLength(80));
        using var second = new U.CheckBoxColumn<Model>("Flag", x => x.Flag, width: new GridLength(50));
        var columns = new U.ColumnList<Model> { first, second };
        IReadOnlyList<U.IColumn<Model>> typed = columns;
        Assert.Same(first, typed[0]); Assert.Same(second, typed[1]);
        var row = new Row(new());
        using var cell = Assert.IsAssignableFrom<IDisposable>(typed[0].CreateCell(row));
        Assert.Equal(130, columns.GetEstimatedWidth(500));
        columns.RemoveAt(0);
        Assert.Same(second, Assert.Single(typed));
    }

    [Fact]
    public void View_comparison_does_not_replace_the_attached_core_sort_policy()
    {
        Comparison<Model?> core = static (_, _) => 11;
        Comparison<Model?> view = static (_, _) => 23;
        var definition = new Core.Models.ValueColumn<Model, string>("Text", x => x.Text, options: new() { CompareAscending = core });
        using var column = new U.TextColumn<Model, string>(definition, new() { CompareAscending = view });
        var typed = (U.IColumn<Model>)column;
        Assert.Same(view, typed.GetComparison(ListSortDirection.Ascending));
        Assert.Same(core, definition.GetComparison(ListSortDirection.Ascending));
        Assert.Same(definition, column.Model);
    }

    [Fact]
    public void Warm_interface_comparisons_allocate_no_new_managed_storage()
    {
        using var column = new U.TextColumn<Model, string>("Text", x => x.Text);
        var typed = (U.IColumn<Model>)column;
        var a = new Model { Text = "A" }; var b = new Model { Text = "B" };
        for (var i = 0; i < 1024; ++i) _ = typed.GetComparison(ListSortDirection.Ascending)!(a, b);
        var total = 0; var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) total += Math.Sign(typed.GetComparison(ListSortDirection.Ascending)!(a, b));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(-4096, total); Assert.Equal(0L, allocated);
    }

    private sealed class Model { public string Text { get; set; } = "Text"; public bool? Flag { get; set; } = true; }
    private sealed class Row(Model model) : Core.Models.IRow<Model>
    {
        public Model Model => model;
        object? Core.Models.IRow.Model => Model;
        public object? Header => null;
        public Core.GridLength Height { get; set; } = Core.GridLength.Auto;
        public void UpdateModelIndex(int delta) { }
    }
    private sealed class ProbeValue : P.CellValue
    {
        public override object? Value => "override";
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new NotSupportedException();
    }
    private sealed class SpecializedColumn() : U.TextColumn<Model, string>("Text", x => x.Text)
    {
        private static readonly Comparison<Model?> Comparison = static (_, _) => 37;
        internal int Creates;
        internal Core.Models.IRow? LastRow;
        public override P.CellValue CreateCell(Core.Models.IRow row) { ++Creates; LastRow = row; return new ProbeValue(); }
        public override Comparison<Model?>? GetComparison(ListSortDirection direction) =>
            direction == ListSortDirection.Ascending ? Comparison : null;
    }
    private sealed class CustomColumn() : U.ColumnBase<Model>("Text", new GridLength(80), new())
    {
        internal readonly Comparison<Model?> Compare = static (_, _) => 43;
        internal int Creates;
        internal Core.Models.IRow? LastRow;
        public override U.ICell CreateCell(Core.Models.IRow<Model> row) { ++Creates; LastRow = row; return new ProbeValue(); }
        public override Comparison<Model?>? GetComparison(ListSortDirection direction) => Compare;
    }
    private sealed class LegacyColumn : P.CellColumnBase<Model>, P.ICellColumn<Model>
    {
        internal int Creates;
        internal Core.Models.IRow? LastRow;
        internal LegacyColumn() : base("Legacy", new GridLength(80), new()) { }
        public override U.ICell CreateCell(Core.Models.IRow<Model> row) => throw new InvalidOperationException("Must use the explicit legacy implementation.");
        U.ICell P.ICellColumn<Model>.CreateCell(Core.Models.IRow<Model> row) { ++Creates; LastRow = row; return new ProbeValue(); }
    }
}
