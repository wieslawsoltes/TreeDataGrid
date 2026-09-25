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
            0 => new A.TextColumn<Item, string>("Text", x => x.Text, options: new()
                { CanUserSortColumn = allow, CompareAscending = ascending, CompareDescending = descending }),
            1 => new A.CheckBoxColumn<Item>("Flag", x => x.Flag, options: new()
                { CanUserSortColumn = allow, CompareAscending = ascending, CompareDescending = descending }),
            _ => new A.TemplateColumn<Item>("Template", (object)"Display", options: new()
                { CanUserSortColumn = allow, CompareAscending = ascending, CompareDescending = descending }),
        };
        U.IColumn<Item> target = kind switch
        {
            0 => new U.TextColumn<Item, string>("Text", x => x.Text, options: new()
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
        Assert.Equal(reference.Width.Value, target.Width.Value);
        Assert.Equal((int)reference.Width.GridUnitType, (int)target.Width.GridUnitType);
        Assert.Equal(reference.CanUserResize, target.CanUserResize);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Typed_interface_cell_creation_uses_the_same_shared_core_row_and_binding(bool editable)
    {
        A.IColumn<Item> reference = new A.TextColumn<Item, string>("Text", x => x.Text,
            editable ? static (x, value) => x.Text = value : null);
        U.IColumn<Item> target = new U.TextColumn<Item, string>("Text", x => x.Text,
            editable ? static (x, value) => x.Text = value : null);
        using var targetLifetime = Assert.IsAssignableFrom<IDisposable>(target);
        var model = new Item { Text = "Original" };
        // The reference row contract already exposes its actual Core row. No
        // copied row or UI source is needed to compare both typed interfaces.
        var row = new SharedRow(model);
        var a = reference.CreateCell(row);
        var u = target.CreateCell(row);
        using var al = Assert.IsAssignableFrom<IDisposable>(a);
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

    private sealed class Item { public string Text { get; set; } = "Text"; public bool? Flag { get; set; } }
    private sealed class SharedRow(Item model) : A.IRow<Item>
    {
        public Item Model => model;
        public object? Header => null;
        public Avalonia.Controls.GridLength Height { get; set; } = Avalonia.Controls.GridLength.Auto;
        public void UpdateModelIndex(int delta) { }
    }
}
