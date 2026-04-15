using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Xunit;

namespace Avalonia.Controls.TreeDataGridUnoTests
{
    public class TreeDataGridUnoParityTests
    {
        [Fact]
        public void FlatSource_Supports_Filter_And_Refresh()
        {
            var data = CreateRows(10);
            var target = CreateFlatTarget(data);
            var maxId = 4;

            target.Filter(x => x.Id <= maxId);

            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, GetRowIds<Row>(target.Rows, static x => x.Id));

            maxId = 2;
            target.RefreshFilter();

            Assert.Equal(new[] { 0, 1, 2 }, GetRowIds<Row>(target.Rows, static x => x.Id));

            target.Filter(null);

            Assert.Equal(Enumerable.Range(0, 10).ToArray(), GetRowIds<Row>(target.Rows, static x => x.Id));
        }

        [Fact]
        public void FlatSource_ClearSort_Restores_Order()
        {
            var data = CreateRows(10);
            var target = CreateFlatTarget(data);

            Assert.True(((ITreeDataGridSource)target).SortBy(target.Columns[0], ListSortDirection.Descending));
            Assert.Equal(new[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 }, GetRowIds<Row>(target.Rows, static x => x.Id));

            target.ClearSort();

            Assert.Null(target.Columns[0].SortDirection);
            Assert.Equal(Enumerable.Range(0, 10).ToArray(), GetRowIds<Row>(target.Rows, static x => x.Id));
        }

        [Fact]
        public void HierarchicalSource_ClearSort_Restores_Order()
        {
            var data = CreateNodes(2, 0);
            var target = CreateHierarchicalTarget(data);

            Assert.True(target.SortBy(target.Columns[0], ListSortDirection.Descending));
            Assert.Equal(new[] { 1, 0 }, GetRowIds<Node>(target.Rows, static x => x.Id));

            target.ClearSort();

            Assert.Null(target.Columns[0].SortDirection);
            Assert.Equal(new[] { 0, 1 }, GetRowIds<Node>(target.Rows, static x => x.Id));
        }

        [Fact]
        public void HierarchicalSource_Filters_Children()
        {
            var data = CreateNodes(1, 2);
            var target = CreateHierarchicalTarget(data);

            target.Filter(x => x.Id % 2 == 0);

            Assert.Equal(new[] { 0 }, GetRowIds<Node>(target.Rows, static x => x.Id));

            target.Expand(new IndexPath(0));

            Assert.Equal(new[] { 0, 2 }, GetRowIds<Node>(target.Rows, static x => x.Id));
        }

        [Fact]
        public void HierarchicalSource_TryGetModelAt_Uses_Filtered_Roots()
        {
            var data = new ObservableCollection<Node>
            {
                new() { Id = 1, Caption = "Odd" },
                new() { Id = 2, Caption = "Even" },
            };
            var target = CreateHierarchicalTarget(data);

            target.Filter(x => x.Id % 2 == 0);

            Assert.True(target.TryGetModelAt(new IndexPath(0), out var result));
            Assert.Same(data[1], result);
        }

        [Fact]
        public void HierarchicalSource_RowEvents_Use_TreeDataGridRowModelEventArgs()
        {
            var data = CreateNodes(1, 1);
            var target = CreateHierarchicalTarget(data);
            TreeDataGridRowModelEventArgs? expanded = null;
            TreeDataGridRowModelEventArgs? collapsed = null;

            target.RowExpanded += (_, e) => expanded = e;
            target.RowCollapsed += (_, e) => collapsed = e;

            target.Expand(new IndexPath(0));
            target.Collapse(new IndexPath(0));

            Assert.NotNull(expanded);
            Assert.NotNull(collapsed);
            Assert.Same(data[0], expanded!.Row.Model);
            Assert.Equal(new IndexPath(0), expanded.Row.ModelIndexPath);
            Assert.Same(data[0], collapsed!.Row.Model);
            Assert.Equal(new IndexPath(0), collapsed.Row.ModelIndexPath);
        }

        [Fact]
        public void RowSelection_Uses_TreeDataGridSelectionChangedEventArgs()
        {
            var data = CreateRows(3);
            var target = CreateFlatTarget(data);
            TreeDataGridSelectionChangedEventArgs<Row>? raised = null;

            target.RowSelection!.SelectionChanged += (_, e) => raised = e;

            target.RowSelection.Select(new IndexPath(1));

            Assert.NotNull(raised);
            Assert.Equal(new[] { new IndexPath(1) }, raised!.SelectedIndexes);
            Assert.Equal(new[] { data[1] }, raised.SelectedItems);
            Assert.Empty(raised.SelectedCellIndexes);
        }

        [Fact]
        public void CellSelection_Uses_TreeDataGridSelectionChangedEventArgs()
        {
            var data = CreateRows(2);
            var target = CreateFlatTarget(data);
            var selection = new TreeDataGridCellSelectionModel<Row>(target)
            {
                SingleSelect = false,
            };
            TreeDataGridSelectionChangedEventArgs<Row>? raised = null;

            selection.SelectionChanged += (_, e) => raised = e;

            selection.SetSelectedRange(new CellIndex(0, new IndexPath(0)), 2, 2);

            Assert.NotNull(raised);
            Assert.Equal(4, raised!.SelectedCellIndexes.Count);
            Assert.Equal(new[] { new IndexPath(0), new IndexPath(1) }, raised.SelectedIndexes);
            Assert.Equal(new[] { data[0], data[1] }, raised.SelectedItems);
        }

        [Theory]
        [InlineData("*", 1, GridUnitType.Star)]
        [InlineData("3*", 3, GridUnitType.Star)]
        [InlineData("Auto", 1, GridUnitType.Auto)]
        [InlineData("120", 120, GridUnitType.Pixel)]
        public void GridLengthTypeConverter_Parses_Xaml_Values(string text, double expectedValue, GridUnitType expectedUnit)
        {
            var converter = TypeDescriptor.GetConverter(typeof(GridLength));
            var result = Assert.IsType<GridLength>(converter.ConvertFrom(null, CultureInfo.InvariantCulture, text));

            Assert.Equal(expectedValue, result.Value);
            Assert.Equal(expectedUnit, result.GridUnitType);
        }

        private static FlatTreeDataGridSource<Row> CreateFlatTarget(IEnumerable<Row> rows)
        {
            return new FlatTreeDataGridSource<Row>(rows)
            {
                Columns =
                {
                    new TextColumn<Row, int>("ID", x => x.Id),
                    new TextColumn<Row, string?>("Caption", x => x.Caption),
                },
            };
        }

        private static HierarchicalTreeDataGridSource<Node> CreateHierarchicalTarget(IEnumerable<Node> roots)
        {
            return new HierarchicalTreeDataGridSource<Node>(roots)
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<Node>(
                        new TextColumn<Node, int>("ID", x => x.Id),
                        x => x.Children),
                    new TextColumn<Node, string?>("Caption", x => x.Caption),
                },
            };
        }

        private static int[] GetRowIds<TModel>(IRows rows, Func<TModel, int> getId)
            where TModel : class
        {
            return Enumerable.Range(0, rows.Count)
                .Select(i => getId((TModel)rows[i].Model!))
                .ToArray();
        }

        private static ObservableCollection<Row> CreateRows(int count)
        {
            return new ObservableCollection<Row>(
                Enumerable.Range(0, count)
                    .Select(i => new Row
                    {
                        Id = i,
                        Caption = $"Row {i}",
                    }));
        }

        private static ObservableCollection<Node> CreateNodes(int count, int childCount)
        {
            var nextId = 0;
            var result = new ObservableCollection<Node>();

            for (var i = 0; i < count; ++i)
            {
                var node = new Node
                {
                    Id = nextId++,
                    Caption = $"Node {i}",
                    Children = new ObservableCollection<Node>(),
                };

                result.Add(node);

                for (var j = 0; j < childCount; ++j)
                {
                    node.Children.Add(new Node
                    {
                        Id = nextId++,
                        Caption = $"Node {i}-{j}",
                        Children = new ObservableCollection<Node>(),
                    });
                }
            }

            return result;
        }

        private sealed class Row
        {
            public int Id { get; init; }
            public string? Caption { get; init; }
        }

        private sealed class Node
        {
            public int Id { get; init; }
            public string? Caption { get; init; }
            public ObservableCollection<Node> Children { get; init; } = new();
        }
    }

}
