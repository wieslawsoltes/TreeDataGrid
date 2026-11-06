using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using Xunit;
using GridLength = Microsoft.UI.Xaml.GridLength;
using GridUnitType = Microsoft.UI.Xaml.GridUnitType;

namespace TreeDataGrid.Uno.Tests;

/// <summary>Native columns added to shared Core sources, as with Avalonia column construction.</summary>
public class NativeColumnSourceTests
{
    [Fact]
    public void Collection_initializer_adds_core_definitions_with_native_presentations()
    {
        var name = new TextColumn<Node, string>("Name", x => x.Name, new GridLength(1, GridUnitType.Star),
            new TextColumnOptions<Node> { TextAlignment = TextAlignment.Right });
        var check = new CheckBoxColumn<Node>("Flag", x => x.Flag, (x, value) => x.Flag = value);
        using var source = new FlatTreeDataGridSource<Node>([new("A")])
        {
            Columns = { name, check }
        };

        Assert.Same(name.Model, source.Columns[0]);
        Assert.Same(check.Model, source.Columns[1]);
        using var first = TreeDataGridPresentation.Create(source);
        using var second = TreeDataGridPresentation.Create(source);
        var firstName = Assert.IsType<TextColumn<Node, string>>(first.NativeColumns[0]);
        var secondName = Assert.IsType<TextColumn<Node, string>>(second.NativeColumns[0]);
        Assert.NotSame(firstName, secondName);
        Assert.Same(name.Options, firstName.Options);
        Assert.Equal(TextAlignment.Right, firstName.TextOptions!.TextAlignment);
        Assert.IsType<CheckBoxColumn<Node>>(first.NativeColumns[1]);
        Assert.Same(source.Columns[0], first.NativeColumns[0].Model);
    }

    [Fact]
    public void Template_columns_keep_resource_keys_in_each_view()
    {
        var template = new TemplateColumn<Node>("Template", "NodeTemplate", "NodeEditTemplate");
        using var source = new FlatTreeDataGridSource<Node>([new("A")]) { Columns = { template } };

        using var view = TreeDataGridPresentation.Create(source);

        var native = Assert.IsType<TemplateColumn<Node>>(view.NativeColumns[0]);
        Assert.NotSame(template, native);
        Assert.Same(template.Options, native.Options);
    }

    [Fact]
    public void Native_expander_column_accepts_a_native_inner_column()
    {
        var root = new Node("Root");
        root.Children.Add(new Node("Child"));
        using var source = new HierarchicalTreeDataGridSource<Node>([root])
        {
            Columns =
            {
                new HierarchicalExpanderColumn<Node>(
                    new TextColumn<Node, string>("Name", x => x.Name), x => x.Children),
                new CheckBoxColumn<Node>("Flag", x => x.Flag),
            }
        };

        source.Expand(new IndexPath(0));
        using var view = TreeDataGridPresentation.Create(source);

        Assert.Equal(2, source.Rows.Count);
        Assert.Equal("Name", view.NativeColumns[0].Header);
        Assert.Equal(CellKind.Expander, view.NativeColumns[0].Kind);
    }

    [Fact]
    public void Adding_a_column_without_a_core_definition_is_rejected()
    {
        using var source = new FlatTreeDataGridSource<Node>([]);
        Assert.Throws<ArgumentNullException>(() => source.Columns.Add((ICellColumn<Node>)null!));
    }

    public sealed class Node(string name)
    {
        public string Name { get; set; } = name;
        public bool Flag { get; set; }
        public List<Node> Children { get; } = new();
    }
}
