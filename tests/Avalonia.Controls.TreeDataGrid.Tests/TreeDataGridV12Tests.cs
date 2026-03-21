using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests
{
    public class TreeDataGridV12Tests
    {
        [AvaloniaFact(Timeout = 10000)]
        public void ItemsSource_Columns_Create_Flat_Source_And_Rows()
        {
            var items = new AvaloniaList<TestRow>
            {
                new() { Name = "One", Age = 10, IsActive = true },
                new() { Name = "Two", Age = 20, IsActive = false },
            };

            var target = CreateTarget();
            target.ItemsSource = items;
            target.ColumnDefinitions.Add(new TreeDataGridTextColumn { Header = "Name", Binding = new Binding("Name") });
            target.ColumnDefinitions.Add(new TreeDataGridTextColumn { Header = "Age", Binding = new Binding("Age") });
            target.ColumnDefinitions.Add(new TreeDataGridCheckBoxColumn { Header = "Active", Binding = new Binding("IsActive") });

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(target.Source);
            Assert.False(target.Source!.IsHierarchical);
            Assert.Equal(3, target.Columns!.Count);
            Assert.Equal(2, target.Rows!.Count);

            var rows = target.RowsPresenter!
                .GetVisualChildren()
                .Cast<TreeDataGridRow>()
                .ToList();

            Assert.Equal(2, rows.Count);
            Assert.Equal(3, rows[0].CellsPresenter!.GetVisualChildren().Count());
        }

        [AvaloniaFact(Timeout = 10000)]
        public void ItemsSource_Hierarchical_Columns_Create_Hierarchical_Source()
        {
            var items = new AvaloniaList<TestRow>
            {
                new()
                {
                    Name = "Root",
                    Age = 50,
                    IsExpanded = true,
                    Children =
                    {
                        new TestRow { Name = "Child", Age = 10 },
                    },
                },
            };

            var target = CreateTarget();
            target.ItemsSource = items;
            target.ColumnDefinitions.Add(
                new TreeDataGridHierarchicalExpanderColumn
                {
                    Header = "Name",
                    ChildrenBinding = new Binding("Children"),
                    HasChildrenBinding = new Binding("HasChildren"),
                    IsExpandedBinding = new Binding("IsExpanded"),
                    InnerColumn = new TreeDataGridTextColumn { Binding = new Binding("Name") },
                });
            target.ColumnDefinitions.Add(new TreeDataGridTextColumn { Header = "Age", Binding = new Binding("Age") });

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(target.Source);
            Assert.True(target.Source!.IsHierarchical);
            Assert.Equal(2, target.Rows!.Count);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void SelectionMode_CellMultiple_Raises_Control_SelectionChanged()
        {
            var items = new AvaloniaList<TestRow>
            {
                new() { Name = "One", Age = 10 },
                new() { Name = "Two", Age = 20 },
            };

            var target = CreateTarget();
            target.SelectionMode = TreeDataGridSelectionMode.Cell | TreeDataGridSelectionMode.Multiple;
            target.ItemsSource = items;
            target.ColumnDefinitions.Add(new TreeDataGridTextColumn { Header = "Name", Binding = new Binding("Name") });
            target.ColumnDefinitions.Add(new TreeDataGridTextColumn { Header = "Age", Binding = new Binding("Age") });

            TreeDataGridSelectionChangedEventArgs? raised = null;
            target.SelectionChanged += (_, e) => raised = e;

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var selection = Assert.IsAssignableFrom<ITreeDataGridCellSelectionModel<object>>(target.ColumnSelection);
            selection.SetSelectedRange(new CellIndex(0, new IndexPath(0)), 2, 2);

            Assert.NotNull(raised);
            Assert.Equal(4, raised!.SelectedCellIndexes.Count);
            Assert.Equal(2, raised.SelectedItems.Count);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Declarative_Text_Column_Writes_Back_Through_Binding_Accessor()
        {
            var items = new AvaloniaList<TestRow>
            {
                new() { Age = 10 },
            };

            var target = CreateTarget();
            target.ItemsSource = items;
            target.ColumnDefinitions.Add(new TreeDataGridTextColumn { Header = "Age", Binding = new Binding("Age") });

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var cell = Assert.IsAssignableFrom<ITextCell>(target.Rows!.RealizeCell(target.Columns![0], 0, 0));

            cell.Text = "42";

            Assert.Equal(42, items[0].Age);
            target.Rows.UnrealizeCell((ICell)cell, 0, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Declarative_Text_Column_Supports_CompiledBinding()
        {
            var items = new AvaloniaList<TestRow>
            {
                new() { Age = 10 },
            };

            var target = CreateTarget();
            target.ItemsSource = items;
            target.ColumnDefinitions.Add(
                new TreeDataGridTextColumn
                {
                    Header = "Age",
                    Binding = CompiledBinding.Create((TestRow x) => x.Age),
                });

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var cell = Assert.IsAssignableFrom<ITextCell>(target.Rows!.RealizeCell(target.Columns![0], 0, 0));

            Assert.Equal("10", cell.Text);

            cell.Text = "55";

            Assert.Equal(55, items[0].Age);
            target.Rows.UnrealizeCell((ICell)cell, 0, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Declarative_CheckBox_Column_Writes_Back_Through_Binding_Accessor()
        {
            var items = new AvaloniaList<TestRow>
            {
                new() { IsActive = true },
            };

            var target = CreateTarget();
            target.ItemsSource = items;
            target.ColumnDefinitions.Add(new TreeDataGridCheckBoxColumn { Header = "Active", Binding = new Binding("IsActive") });

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var cell = Assert.IsType<CheckBoxCell>(target.Rows!.RealizeCell(target.Columns![0], 0, 0));

            cell.Value = false;

            Assert.False(items[0].IsActive);
            target.Rows.UnrealizeCell(cell, 0, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Hierarchical_IsExpandedBinding_Writes_Back_On_Expand_And_Collapse()
        {
            var items = new AvaloniaList<TestRow>
            {
                new()
                {
                    Name = "Root",
                    IsExpanded = true,
                    Children =
                    {
                        new TestRow { Name = "Child" },
                    },
                },
            };

            var target = CreateTarget();
            target.ItemsSource = items;
            target.ColumnDefinitions.Add(
                new TreeDataGridHierarchicalExpanderColumn
                {
                    Header = "Name",
                    ChildrenBinding = new Binding("Children"),
                    HasChildrenBinding = new Binding("HasChildren"),
                    IsExpandedBinding = new Binding("IsExpanded"),
                    InnerColumn = new TreeDataGridTextColumn { Binding = new Binding("Name") },
                });

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var source = Assert.IsType<HierarchicalTreeDataGridSource<object>>(target.Source);

            source.Collapse(new IndexPath(0));
            Assert.False(items[0].IsExpanded);

            source.Expand(new IndexPath(0));
            Assert.True(items[0].IsExpanded);
        }

        private static TreeDataGrid CreateTarget()
        {
            return new TreeDataGrid
            {
                Template = TestTemplates.TreeDataGridTemplate(),
            };
        }

        private static TestWindow CreateWindow(TreeDataGrid target)
        {
            return new TestWindow(target)
            {
                Styles =
                {
                    new Style(x => x.Is<TreeDataGridRow>())
                    {
                        Setters =
                        {
                            new Setter(TreeDataGridRow.TemplateProperty, TestTemplates.TreeDataGridRowTemplate()),
                        }
                    },
                    new Style(x => x.Is<TreeDataGridCell>())
                    {
                        Setters =
                        {
                            new Setter(TreeDataGridCell.HeightProperty, 24.0),
                        }
                    }
                }
            };
        }

        private class TestRow
        {
            public string? Name { get; set; }
            public int Age { get; set; }
            public bool IsActive { get; set; }
            public bool IsExpanded { get; set; }
            public bool HasChildren => Children.Count > 0;
            public ObservableCollection<TestRow> Children { get; } = new();
        }
    }
}
