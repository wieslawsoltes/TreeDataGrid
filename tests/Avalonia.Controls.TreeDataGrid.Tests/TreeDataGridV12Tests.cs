using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
        public void Declarative_ItemsSource_Preserves_Collection_Tracking()
        {
            var items = new AvaloniaList<TestRow>
            {
                new() { Name = "One" },
            };

            var target = CreateTarget();
            target.ItemsSource = items;
            target.ColumnDefinitions.Add(new TreeDataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Single(target.Rows!);

            items.Add(new TestRow { Name = "Two" });
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, target.Rows!.Count);

            items.RemoveAt(0);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Single(target.Rows!);
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
        public void Declarative_Text_Column_Tracks_Nested_ReflectionBinding_Notifications()
        {
            var items = new AvaloniaList<NotifyingTestRow>
            {
                new()
                {
                    Customer = new CustomerInfo
                    {
                        Address = new AddressInfo
                        {
                            City = "Warsaw",
                        },
                    },
                },
            };

            var target = CreateTarget();
            target.ItemsSource = items;
            target.ColumnDefinitions.Add(new TreeDataGridTextColumn { Header = "City", Binding = new Binding("Customer.Address.City") });

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var cell = Assert.IsAssignableFrom<ITextCell>(target.Rows!.RealizeCell(target.Columns![0], 0, 0));
            Assert.Equal("Warsaw", cell.Text);

            items[0].Customer.Address.City = "Krakow";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Krakow", cell.Text);
            target.Rows.UnrealizeCell((ICell)cell, 0, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Declarative_Text_Column_Tracks_Nested_CompiledBinding_Notifications()
        {
            var items = new AvaloniaList<NotifyingTestRow>
            {
                new()
                {
                    Customer = new CustomerInfo
                    {
                        Address = new AddressInfo
                        {
                            City = "Warsaw",
                        },
                    },
                },
            };

            var target = CreateTarget();
            target.ItemsSource = items;
            target.ColumnDefinitions.Add(
                new TreeDataGridTextColumn
                {
                    Header = "City",
                    Binding = CompiledBinding.Create((NotifyingTestRow x) => x.Customer.Address.City),
                });

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var cell = Assert.IsAssignableFrom<ITextCell>(target.Rows!.RealizeCell(target.Columns![0], 0, 0));
            Assert.Equal("Warsaw", cell.Text);

            items[0].Customer.Address.City = "Krakow";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Krakow", cell.Text);
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
        public void Declarative_CheckBox_Column_Uses_Nullable_Binding_Type_For_Three_State_When_Source_Is_Empty()
        {
            var columnDefinition = new TreeDataGridCheckBoxColumn
            {
                Header = "Optional",
                Binding = new Binding("IsOptional"),
            };
            columnDefinition.InitializeFromSample(sampleModel: null, modelType: typeof(TestRow));

            var column = columnDefinition.CreateUntypedColumn();
            var row = new AnonymousRow<object>().Update(0, new TestRow { IsOptional = false });
            var cell = Assert.IsType<CheckBoxCell>(column.CreateCell(row));

            Assert.True(cell.IsThreeState);

            cell.Value = null;

            Assert.Null(((TestRow)row.Model).IsOptional);
            cell.Dispose();
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

        [AvaloniaFact(Timeout = 10000)]
        public void Hierarchical_Expander_Text_Column_Infers_Setter_From_Getter()
        {
            var items = new AvaloniaList<TestRow>
            {
                new()
                {
                    Name = "Root",
                    Children =
                    {
                        new TestRow { Name = "Child" },
                    },
                },
            };

            var source = new HierarchicalTreeDataGridSource<TestRow>(items)
                .WithHierarchicalExpanderTextColumn(x => x.Name, x => x.Children);

            var target = CreateTarget();
            target.Source = source;

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var expanderCell = Assert.IsAssignableFrom<IExpanderCell>(target.Rows!.RealizeCell(target.Columns![0], 0, 0));
            var textCell = Assert.IsAssignableFrom<ITextCell>(expanderCell.Content);

            textCell.Text = "Updated";

            Assert.Equal("Updated", items[0].Name);
            target.Rows.UnrealizeCell((ICell)expanderCell, 0, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void WithTextColumn_IsReadOnly_Does_Not_Infer_Setter()
        {
            var items = new AvaloniaList<TestRow>
            {
                new() { Name = "Original" },
            };

            var source = new FlatTreeDataGridSource<TestRow>(items)
                .WithTextColumn(x => x.Name, options => options.IsReadOnly = true);

            var column = Assert.IsType<TextColumn<TestRow, string>>(source.Columns[0]);
            Assert.Null(column.Binding.Write);

            var target = CreateTarget();
            target.Source = source;

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var cell = Assert.IsAssignableFrom<ITextCell>(target.Rows!.RealizeCell(target.Columns![0], 0, 0));

            Assert.False(cell.CanEdit);

            cell.Text = "Updated";

            Assert.Equal("Original", items[0].Name);
            target.Rows.UnrealizeCell((ICell)cell, 0, 0);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void WithThreeStateCheckBoxColumn_IsReadOnly_Does_Not_Infer_Setter()
        {
            var items = new AvaloniaList<TestRow>
            {
                new() { IsOptional = false },
            };

            var source = new FlatTreeDataGridSource<TestRow>(items)
                .WithThreeStateCheckBoxColumn(x => x.IsOptional, options => options.IsReadOnly = true);

            var column = Assert.IsType<CheckBoxColumn<TestRow>>(source.Columns[0]);
            Assert.Null(column.Binding.Write);

            var target = CreateTarget();
            target.Source = source;

            var root = CreateWindow(target);
            root.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var cell = Assert.IsType<CheckBoxCell>(target.Rows!.RealizeCell(target.Columns![0], 0, 0));

            Assert.True(cell.IsReadOnly);

            cell.Value = true;

            Assert.False(items[0].IsOptional);
            target.Rows.UnrealizeCell(cell, 0, 0);
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
            public bool? IsOptional { get; set; }
            public bool IsExpanded { get; set; }
            public bool HasChildren => Children.Count > 0;
            public ObservableCollection<TestRow> Children { get; } = new();
        }

        private abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;

            protected bool SetAndRaise<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
            {
                if (Equals(field, value))
                    return false;

                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                return true;
            }
        }

        private class NotifyingTestRow : NotifyPropertyChangedBase
        {
            private CustomerInfo _customer = new();

            public CustomerInfo Customer
            {
                get => _customer;
                set => SetAndRaise(ref _customer, value);
            }
        }

        private class CustomerInfo : NotifyPropertyChangedBase
        {
            private AddressInfo _address = new();

            public AddressInfo Address
            {
                get => _address;
                set => SetAndRaise(ref _address, value);
            }
        }

        private class AddressInfo : NotifyPropertyChangedBase
        {
            private string? _city;

            public string? City
            {
                get => _city;
                set => SetAndRaise(ref _city, value);
            }
        }
    }
}
