using System;
using System.Linq;
using System.Reflection;
using TreeDataGridCore;
using Uno.Controls.Presentation;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = Uno.Controls.Models.TreeDataGrid;
using C = TreeDataGridCore.Models;

namespace TreeDataGrid.Parity.Tests;

public sealed class DeclaredColumnFactoriesParityTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Theory]
    [InlineData("Width")]
    [InlineData("MinWidth")]
    [InlineData("MaxWidth")]
    public void Width_policies_are_declared_on_the_definition_owner(string name)
    {
        var expected = typeof(Avalonia.Controls.TreeDataGridColumn).GetProperty(name, Declared)!;
        var actual = typeof(Uno.Controls.TreeDataGridColumn).GetProperty(name, Declared);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetMethod!.IsPublic, actual!.GetMethod!.IsPublic);
        Assert.Equal(expected.SetMethod!.IsPublic, actual.SetMethod!.IsPublic);
        Assert.Equal(expected.PropertyType.IsGenericType, actual.PropertyType.IsGenericType);
        Assert.Equal(expected.PropertyType.Name, actual.PropertyType.Name);
        Assert.Null(typeof(Uno.Controls.TreeDataGridColumn).GetField($"<{name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Width_definition_and_base_references_share_the_same_storage(int unit)
    {
        Uno.Controls.TreeDataGridColumn column = new Uno.Controls.TreeDataGridTextColumn();
        Uno.Controls.ColumnCreateOptions options = column;
        var first = new Microsoft.UI.Xaml.GridLength(unit == 0 ? 1 : 40, (Microsoft.UI.Xaml.GridUnitType)unit);
        var second = new Microsoft.UI.Xaml.GridLength(71);
        column.Width = column.MinWidth = first;
        column.MaxWidth = first;
        Assert.Equal(first, options.Width);
        Assert.Equal(first, options.MinWidth);
        Assert.Equal(first, options.MaxWidth);
        options.Width = options.MinWidth = second;
        options.MaxWidth = null;
        Assert.Equal(second, column.Width);
        Assert.Equal(second, column.MinWidth);
        Assert.Null(column.MaxWidth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Reuse_is_declared_on_each_builtin_column(int kind)
    {
        Type[] reference = [typeof(A.TextColumn<Model, int>), typeof(A.CheckBoxColumn<Model>), typeof(A.TemplateColumn<Model>)];
        Type[] native = [typeof(U.TextColumn<Model, int>), typeof(U.CheckBoxColumn<Model>), typeof(U.TemplateColumn<Model>)];
        var expected = reference[kind].GetMethod("TryReuseCell", Declared)!;
        var actual = native[kind].GetMethod("TryReuseCell", Declared);
        Assert.NotNull(actual);
        Assert.Equal(expected.ReturnType, actual!.ReturnType);
        Assert.Equal(expected.IsVirtual, actual.IsVirtual);
        Assert.Equal(expected.GetParameters().Select(p => p.Name), actual.GetParameters().Select(p => p.Name));
        Assert.Equal(typeof(C.IRow<Model>), actual.GetParameters()[1].ParameterType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Core_row_factories_preserve_the_existing_native_slot_and_untyped_customization(bool template)
    {
        var referenceType = template ? typeof(A.TemplateColumn<Model>) : typeof(A.CheckBoxColumn<Model>);
        var nativeType = template ? typeof(U.TemplateColumn<Model>) : typeof(U.CheckBoxColumn<Model>);
        var expected = referenceType.GetMethod("CreateCell", Declared, null, [typeof(C.IRow<Model>)], null)!;
        var actual = nativeType.GetMethod("CreateCell", Declared, null, [typeof(C.IRow<Model>)], null);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetParameters().Select(p => p.Name), actual!.GetParameters().Select(p => p.Name));
        // The already published Uno factory is virtual and returns CellValue.
        // Keep that source/binary contract; it remains a raw reference adaptation.
        Assert.True(actual.IsVirtual);
        Assert.Equal(typeof(CellValue), actual.ReturnType);
        Assert.Equal(nativeType.BaseType, actual.GetBaseDefinition().DeclaringType);
        using var source = new FlatTreeDataGridSource<Model>([new Model { Number = 10, Flag = true }]);
        var row = source.Rows[0];
        if (template)
        {
            using var column = new DerivedTemplateColumn();
            using var cell = ((U.TemplateColumn<Model>)column).CreateCell(row);
            Assert.Equal(1, column.Calls);
            Assert.Same(row, column.LastRow);
            Assert.Same(row.Model, cell.Value);
        }
        else
        {
            using var column = new DerivedCheckBoxColumn();
            using var cell = ((U.CheckBoxColumn<Model>)column).CreateCell(row);
            Assert.Equal(1, column.Calls);
            Assert.Same(row, column.LastRow);
            Assert.Equal(true, cell.Value);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Declared_reuse_keeps_the_same_cell_and_rejects_another_column_owner(int kind)
    {
        var first = new Model { Number = 10, Flag = true };
        var second = new Model { Number = 20, Flag = false };
        using var source = new FlatTreeDataGridSource<Model>([first, second]);
        using var text = new U.TextColumn<Model, int>("Text", x => x.Number);
        using var check = new U.CheckBoxColumn<Model>("Check", x => x.Flag);
        using var template = new U.TemplateColumn<Model>("Template", "Display");
        CellColumn[] columns = [text, check, template];
        using var cell = columns[kind].CreateCell(source.Rows[0]);
        var model = cell.PresentationModel;
        var reused = kind switch
        {
            0 => text.TryReuseCell(model, source.Rows[1]),
            1 => check.TryReuseCell(model, source.Rows[1]),
            _ => template.TryReuseCell(model, source.Rows[1]),
        };
        Assert.True(reused);
        Assert.Same(model, cell.PresentationModel);
        Assert.Equal(kind switch { 0 => (object)20, 1 => false, _ => second }, cell.Value);
        using var other = columns[(kind + 1) % columns.Length].CreateCell(source.Rows[0]);
        Assert.False(kind switch
        {
            0 => text.TryReuseCell(other, source.Rows[1]),
            1 => check.TryReuseCell(other, source.Rows[1]),
            _ => template.TryReuseCell(other, source.Rows[1]),
        });
    }

    private sealed class DerivedCheckBoxColumn() : U.CheckBoxColumn<Model>("Check", x => x.Flag)
    {
        public int Calls;
        public C.IRow? LastRow;
        public override CellValue CreateCell(C.IRow row)
        {
            ++Calls;
            LastRow = row;
            return base.CreateCell(row);
        }
    }

    private sealed class DerivedTemplateColumn() : U.TemplateColumn<Model>("Template", "Display")
    {
        public int Calls;
        public C.IRow? LastRow;
        public override CellValue CreateCell(C.IRow row)
        {
            ++Calls;
            LastRow = row;
            return base.CreateCell(row);
        }
    }

    private sealed class Model
    {
        public int Number { get; set; }
        public bool Flag { get; set; }
    }
}
