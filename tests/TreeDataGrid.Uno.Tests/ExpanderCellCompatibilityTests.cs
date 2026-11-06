using System;
using System.Collections.ObjectModel;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public class ExpanderCellCompatibilityTests
{
    [Fact]
    public void Public_Expander_Contract_Preserves_Core_Row_And_Inner_Cell_Identity()
    {
        var root = new Node("root");
        root.Children.Add(new("child"));
        using var source = new HierarchicalTreeDataGridSource<Node>([root]);
        source.Columns.Add(new HierarchicalExpanderColumn<Node>(
            new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
        using var view = TreeDataGridPresentation.Create(source);
        using var value = view.RealizeCell(0, 0);
        var cell = Assert.IsAssignableFrom<UI.IExpanderCell>(value);
        Assert.IsAssignableFrom<UI.IExpanderCellPresentation>(cell);
        Assert.IsAssignableFrom<UI.IExpander>(cell);
        Assert.Same(source.Rows[0], cell.Row);
        Assert.Same(root, cell.Row.Model);
        Assert.Same(Assert.IsAssignableFrom<ExpanderCellValue>(value).Content, cell.Content);
        Assert.False(cell.IsExpanded);
        Assert.True(cell.ShowExpander);
        Assert.Single(source.Rows);
        cell.IsExpanded = true;
        Assert.Equal(2, source.Rows.Count);
        Assert.Same(root.Children[0], source.Rows[1].Model);
        cell.IsExpanded = false;
        Assert.Single(source.Rows);
    }

    [Fact]
    public void Custom_Expander_Contract_Is_Recognized_Without_An_Additional_Adapter_Interface()
    {
        var model = new Node("root");
        using var source = new HierarchicalTreeDataGridSource<Node>([model]);
        source.Columns.Add(new HierarchicalExpanderColumn<Node>(
            new TextColumn<Node, string>("Name", x => x.Name), x => x.Children));
        var custom = new CustomExpander(source.Rows[0]);
        using var adapted = CellColumnAdapter<Node>.Adapt(custom, ownsModel: true);
        var value = Assert.IsAssignableFrom<ExpanderCellValue>(adapted);
        Assert.Same(custom, value.PresentationModel);
        Assert.Same(custom.Row, value.Row);
        Assert.Same(custom.Content, value.Content);
        value.IsExpanded = true;
        Assert.True(custom.IsExpanded);
        Assert.Equal("root", value.Inner.DisplayText);
    }

    private sealed class CustomExpander(IRow row) : UI.IExpanderCell
    {
        public IRow Row { get; } = row;
        public object? Content { get; } = new UI.TextCell<string>("root");
        public object? Value => ((UI.ICell)Content!).Value;
        public bool CanEdit => false;
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.None;
        public bool IsExpanded { get; set; }
        public bool ShowExpander => true;
    }
    private sealed record Node(string Name)
    {
        public ObservableCollection<Node> Children { get; } = new();
    }
}
