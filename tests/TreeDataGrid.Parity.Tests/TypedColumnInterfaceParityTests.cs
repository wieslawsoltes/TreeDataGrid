using System;
using System.ComponentModel;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Parity.Tests;

public sealed class TypedColumnInterfaceParityTests
{
    [Theory]
    [InlineData(0, null)]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, null)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, null)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void Typed_interface_comparison_matches_reference_for_every_builtin_policy(int kind, bool? allow)
    {
        Comparison<Item?> ascending = static (a, b) => StringComparer.Ordinal.Compare(a?.Text, b?.Text);
        Comparison<Item?> descending = static (a, b) => StringComparer.Ordinal.Compare(b?.Text, a?.Text);
        A.IColumn<Item> reference = kind switch
        {
            0 => new A.TextColumn<Item, string?>("Text", x => x.Text, options: new()
                { CanUserSortColumn = allow, CompareAscending = ascending, CompareDescending = descending }),
            1 => new A.CheckBoxColumn<Item>("Flag", x => x.Flag, options: new()
                { CanUserSortColumn = allow, CompareAscending = ascending, CompareDescending = descending }),
            _ => new A.TemplateColumn<Item>("Template", (object)"Display", options: new()
                { CanUserSortColumn = allow, CompareAscending = ascending, CompareDescending = descending }),
        };
        U.IColumn<Item> target = kind switch
        {
            0 => new U.TextColumn<Item, string?>("Text", x => x.Text, options: new()
                { CanUserSortColumn = allow, CompareAscending = ascending, CompareDescending = descending }),
            1 => new U.CheckBoxColumn<Item>("Flag", x => x.Flag, options: new()
                { CanUserSortColumn = allow, CompareAscending = ascending, CompareDescending = descending }),
            _ => new U.TemplateColumn<Item>("Template", (object)"Display", options: new()
                { CanUserSortColumn = allow, CompareAscending = ascending, CompareDescending = descending }),
        };
        using var targetLifetime = Assert.IsAssignableFrom<IDisposable>(target);
        foreach (var direction in new[] { ListSortDirection.Ascending, ListSortDirection.Descending, (ListSortDirection)42 })
            Assert.Same(reference.GetComparison(direction), target.GetComparison(direction));
        Assert.Equal(reference.Header, target.Header);
        AssertWidth(reference.Width, target.Width);
        Assert.Equal(reference.CanUserResize, target.CanUserResize);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    public void Typed_width_contract_preserves_units_numeric_weights_and_initial_pixel_geometry(int kind, int unit)
    {
        var aw = new Avalonia.Controls.GridLength(unit == 1 ? 80 : 2, (Avalonia.Controls.GridUnitType)unit);
        var uw = new Microsoft.UI.Xaml.GridLength(unit == 1 ? 80 : 2, (Microsoft.UI.Xaml.GridUnitType)unit);
        A.IColumn<Item> reference = kind switch
        {
            0 => new A.TextColumn<Item, string?>("Text", x => x.Text, width: aw),
            1 => new A.CheckBoxColumn<Item>("Flag", x => x.Flag, width: aw),
            _ => new A.TemplateColumn<Item>("Template", (object)"Display", width: aw),
        };
        U.IColumn<Item> target = kind switch
        {
            0 => new U.TextColumn<Item, string?>("Text", x => x.Text, width: uw),
            1 => new U.CheckBoxColumn<Item>("Flag", x => x.Flag, width: uw),
            _ => new U.TemplateColumn<Item>("Template", (object)"Display", width: uw),
        };
        using var lifetime = Assert.IsAssignableFrom<IDisposable>(target);
        AssertWidth(reference.Width, target.Width);
        Assert.Equal(reference.ActualWidth, target.ActualWidth);
        if (unit == 1) Assert.Equal(80, target.ActualWidth);
        else Assert.True(double.IsNaN(target.ActualWidth));
    }

    private static void AssertWidth(Avalonia.Controls.GridLength reference, Microsoft.UI.Xaml.GridLength target)
    {
        Assert.Equal(reference.IsAuto, target.IsAuto);
        Assert.Equal(reference.IsAbsolute, target.IsAbsolute);
        Assert.Equal(reference.IsStar, target.IsStar);
        Assert.Equal((int)reference.GridUnitType, (int)target.GridUnitType);
        // Auto's numeric payload is ignored by native layout. Avalonia stores
        // zero and WinUI stores one; pixels and star weights must still match.
        if (!reference.IsAuto) Assert.Equal(reference.Value, target.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Typed_interface_cell_creation_uses_the_same_shared_core_row_and_binding(bool editable)
    {
        A.IColumn<Item> reference = editable
            ? new A.TextColumn<Item, string?>("Text", x => x.Text, static (x, value) => x.Text = value)
            : new A.TextColumn<Item, string?>("Text", x => x.Text);
        U.IColumn<Item> target = editable
            ? new U.TextColumn<Item, string?>("Text", x => x.Text, static (x, value) => x.Text = value)
            : new U.TextColumn<Item, string?>("Text", x => x.Text);
        using var targetLifetime = Assert.IsAssignableFrom<IDisposable>(target);
        var model = new Item { Text = "Original" };
        // Both interfaces operate on this same Core-compatible reference row.
        var row = new SharedRow(model);
        var a = reference.CreateCell(row);
        using var al = Assert.IsAssignableFrom<IDisposable>(a);
        var u = target.CreateCell(row);
        using var ul = Assert.IsAssignableFrom<IDisposable>(u);
        Assert.Equal(a.Value, u.Value);
        Assert.Equal(a.CanEdit, u.CanEdit);
        Assert.Equal((int)a.EditGestures, (int)u.EditGestures);
        if (editable)
        {
            ((A.ITextCell)a).Text = "Reference";
            Assert.Equal("Reference", model.Text);
            ((U.ITextCell)u).Text = "Target";
            Assert.Equal("Target", model.Text);
        }
        Assert.Same(model, ((TreeDataGridCore.Models.IRow<Item>)row).Model);
    }

    private sealed class Item { public string? Text { get; set; } = "Text"; public bool? Flag { get; set; } }
    private sealed class SharedRow(Item model) : A.IRow<Item>
    {
        public Item Model => model;
        public object? Header => null;
        public Avalonia.Controls.GridLength Height { get; set; } = Avalonia.Controls.GridLength.Auto;
        public void UpdateModelIndex(int delta) { }
    }
}
