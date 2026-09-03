using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Presentation;
using Avalonia.Headless.XUnit;
using Core = global::TreeDataGridCore;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests;
public class CorePresentationRegressionTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nested_expansion_binding_observes_nested_property_changes(bool native)
    {
        var item = new Node(); item.Children.Add(new Node());
        if (native)
        {
            using var source = new Core.HierarchicalTreeDataGridSource<Node>(new[] { item });
            source.Columns.Add(new Core.Models.HierarchicalExpanderColumn<Node>(
                new Core.Models.TextColumn<Node, string>("Name", x => x.Name),
                x => x.Children, isExpandedSelector: x => x.State.IsExpanded));
            using var view = new TreeDataGridPresentation<Node>(source);
            var cell = view.Rows.RealizeCell(view.Columns[0], 0, 0);
            try { item.State.IsExpanded = true; Assert.Equal(2, source.Rows.Count); }
            finally { view.Rows.UnrealizeCell(cell, 0, 0); }
        }
        else
        {
            using var source = new HierarchicalTreeDataGridSource<Node>(new[] { item });
            source.Columns.Add(new HierarchicalExpanderColumn<Node>(
                new TextColumn<Node, string>("Name", x => x.Name),
                x => x.Children, isExpandedSelector: x => x.State.IsExpanded));
            var cell = source.Rows.RealizeCell(source.Columns[0], 0, 0);
            try { item.State.IsExpanded = true; Assert.Equal(2, source.Rows.Count); }
            finally { source.Rows.UnrealizeCell(cell, 0, 0); }
        }
    }

    [AvaloniaTheory]
    [InlineData("dispose")]
    [InlineData("hide")]
    [InlineData("replace")]
    public void Hierarchical_presentation_disposes_owned_custom_column(string action)
    {
        using var source = new Core.HierarchicalTreeDataGridSource<Node>(new[] { new Node() });
        source.Columns.Add(new Core.Models.HierarchicalExpanderColumn<Node>(
            new Core.Models.TemplateColumn<Node>("Name", "custom"), x => x.Children));
        var cellColumn = new DisposableColumn();
        var options = new TreeDataGridPresentationOptions<Node>();
        options.Columns.Add("custom", _ => cellColumn);
        var view = new TreeDataGridPresentation<Node>(source, options);
        options.Columns.Add("replacement", _ => new TextColumn<Node, string>("Name", x => x.Name));
        if (action == "hide")
        {
            source.Columns[0].IsVisible = false;
            Assert.Equal(0, cellColumn.DisposeCount);
            view.Dispose();
        }
        else if (action == "replace") source.Columns[0].PresentationKey = "replacement";
        else view.Dispose();
        Assert.Equal(1, cellColumn.DisposeCount);
        view.Dispose();
        Assert.Equal(1, cellColumn.DisposeCount);
    }

    public sealed class DisposableColumn : TextColumn<Node, string>, IDisposable
    {
        public DisposableColumn() : base("Name", x => x.Name) { }
        public int DisposeCount { get; private set; }
        public void Dispose() => ++DisposeCount;
    }
    public sealed class Node
    {
        public string Name => "Node";
        public State State { get; } = new();
        public ObservableCollection<Node> Children { get; } = new();
    }
    public sealed class State : INotifyPropertyChanged
    {
        private bool _expanded;
        public bool IsExpanded { get => _expanded; set { _expanded = value; PropertyChanged?.Invoke(this, new(nameof(IsExpanded))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
