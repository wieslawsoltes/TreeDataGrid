using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls.Presentation;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Core = global::TreeDataGridCore;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests
{
    public class CoreSourcePresentationTests
    {
        [AvaloniaFact]
        public void Presentation_uses_neutral_sort_selection_and_column_state()
        {
            var items = new ObservableCollection<Node> { new("B"), new("A") };
            using var source = Flat(items);
            source.RowSelection!.SelectedIndex = 0;
            using var presentation = new TreeDataGridPresentation<Node>(source);
            Assert.Same(source.RowSelection.SelectedItem, presentation.SelectedItems![0]);
            Assert.Same(source.Rows[0], presentation.Rows[0]);
            Assert.False(typeof(ITreeDataGridSource).IsAssignableFrom(presentation.GetType()));
            Assert.IsType<TextColumn<Node, string>>(presentation.Columns[0]);
            presentation.SortBy(presentation.Columns[0], ListSortDirection.Ascending);
            Assert.Equal("A", ((Node)presentation.Rows[0].Model!).Name);
            Assert.Equal(1, presentation.Rows.ModelIndexToRowIndex(presentation.SelectedIndexes![0]));
            presentation.Select(1, true);
            Assert.Equal(new Core.IndexPath(1), source.RowSelection.SelectedIndex);
            source.RowSelection.SelectedIndex = 0;
            Assert.Equal(new IndexPath(0), presentation.SelectedIndexes![0]);
            presentation.Columns.SetColumnWidth(0, new GridLength(142));
            Assert.Equal(new Core.GridLength(142), source.Columns[0].Width);
            source.Columns[0].Width = Core.GridLength.Star;
            Assert.Equal(GridLength.Star, presentation.Columns[0].Width);
            source.Columns[0].IsVisible = false;
            Assert.Empty(presentation.Columns);
            source.Columns[0].IsVisible = true;
            Assert.Single(presentation.Columns);
        }
        [AvaloniaFact]
        public void Hierarchy_cells_expand_the_neutral_projection_and_update_models()
        {
            var parent = new Node("Root") { Children = { new("Child") } };
            using var source = new Core.HierarchicalTreeDataGridSource<Node>(new[] { parent });
            source.Columns.Add(new Core.Models.HierarchicalExpanderColumn<Node>(
                new Core.Models.TextColumn<Node, string>("Name", x => x.Name), x => x.Children,
                setIsExpanded: (x, expanded) => x.Expanded = expanded));
            using var presentation = new TreeDataGridPresentation<Node>(source);
            var cell = (IExpanderCellPresentation)presentation.Rows.RealizeCell(presentation.Columns[0], 0, 0);
            cell.IsExpanded = true;
            Assert.True(parent.Expanded);
            Assert.Equal(2, source.Rows.Count);
            Assert.Equal(2, presentation.Rows.Count);
            parent.Children.Add(new("Second"));
            Assert.Equal(3, presentation.Rows.Count);
            source.Collapse(0);
            Assert.False(cell.IsExpanded);
            Assert.Single(presentation.Rows);
            presentation.Rows.UnrealizeCell(cell, 0, 0);
        }
        [AvaloniaFact]
        public void Disposal_detaches_presentation_and_allows_reattachment()
        {
            var items = new ObservableCollection<Node> { new("A") };
            using var source = Flat(items);
            var presentation = new TreeDataGridPresentation<Node>(source);
            var selectionEvents = 0;
            ((ITreeDataGridSelectionInteraction)presentation.SelectionInteraction!).SelectionChanged += (_, _) => ++selectionEvents;
            source.RowSelection!.SelectedIndex = 0;
            Assert.Equal(1, selectionEvents);
            presentation.Dispose();
            presentation.Dispose();
            source.RowSelection.Clear();
            items.Add(new("B"));
            Assert.Equal(1, selectionEvents);
            using var replacement = new TreeDataGridPresentation<Node>(source);
            Assert.Equal(2, replacement.Rows.Count);
            source.RowSelection.SelectedIndex = 1;
            Assert.Same(items[1], replacement.SelectedItems![0]);
        }
        [AvaloniaFact]
        public void Control_renders_edits_scrolls_and_navigates_a_neutral_source()
        {
            var items = new ObservableCollection<Node>(Enumerable.Range(0, 100).Select(x => new Node($"Row {x}")));
            using var source = Flat(items);
            var grid = new TreeDataGrid { Model = source, Template = TestTemplates.TreeDataGridTemplate() };
            var presentation = grid.Presentation!;
            var window = new TestWindow(grid)
            {
                Styles = { TestTemplates.TreeDataGridRowStyle, new Style(x => x.OfType<TreeDataGridCell>()) { Setters = { new Setter(TreeDataGridCell.HeightProperty, 10.0) } } }
            };
            try
            {
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.NotEmpty(grid.RowsPresenter!.GetRealizedElements());
                source.RowSelection!.SelectedIndex = 0;
                ((ITreeDataGridSelectionInteraction)presentation.SelectionInteraction!).OnKeyDown(grid, new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
                Assert.Equal(new Core.IndexPath(1), source.RowSelection.SelectedIndex);
                var cell = presentation.Rows.RealizeCell(presentation.Columns[0], 0, 1);
                ((ITextCell)cell).Text = "Edited";
                Assert.Equal("Edited", items[1].Name);
                presentation.Rows.UnrealizeCell(cell, 0, 1);
                grid.Scroll!.Offset = new Vector(0, 400);
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.NotEmpty(grid.RowsPresenter.GetRealizedElements());
                items.Insert(0, new("Inserted"));
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.Equal(new Core.IndexPath(2), source.RowSelection.SelectedIndex);
            }
            finally { window.Close(); }
        }
        [AvaloniaFact]
        public void Model_binding_suspends_subscriptions_and_retains_view_state_after_detach()
        {
            using var source = Flat(new() { new("A") });
            var grid = new TreeDataGrid { Model = source, Template = TestTemplates.TreeDataGridTemplate() };
            var window = new TestWindow(grid) { Styles = { TestTemplates.TreeDataGridRowStyle } };
            try
            {
                window.UpdateLayout();
                Assert.Null(grid.Source);
                var first = grid.Presentation;
                Assert.NotNull(first);
                var rowChanges = 0;
                first!.Rows.CollectionChanged += (_, _) => ++rowChanges;
                window.Content = null;
                Assert.Same(first, grid.Presentation);
                Assert.Null(grid.Presentation!.SelectionInteraction);
                Assert.Same(source, grid.Model);
                source.Columns[0].Width = new Core.GridLength(175);
                source.Items = new ObservableCollection<Node> { new("Changed"), new("New") };
                source.RowSelection!.SelectedIndex = 0;
                Assert.Equal(0, rowChanges);
                window.Content = grid;
                window.UpdateLayout();
                Assert.NotNull(grid.Presentation);
                Assert.Same(first, grid.Presentation);
                Assert.Equal(new GridLength(175), grid.Columns![0].Width);
                Assert.Equal(2, grid.Rows!.Count);
                Assert.Equal(175, grid.Columns[0].ActualWidth);
                source.Items = new ObservableCollection<Node> { new("Third") };
                Assert.Equal(1, rowChanges);
                source.RowSelection.SelectedIndex = 0;
                Assert.Same(source.RowSelection.SelectedItem, grid.Presentation!.SelectedItems![0]);
                // The explicit legacy Source API still takes precedence when assigned.
                using var legacy = new FlatTreeDataGridSource<Node>(Array.Empty<Node>());
                grid.Source = legacy;
                Assert.Null(grid.Model);
                Assert.Same(legacy, grid.Source);
            }
            finally { window.Close(); }
        }

        [AvaloniaFact]
        public void Model_assigned_while_unattached_is_suspended_until_attached()
        {
            var items = new ObservableCollection<Node> { new("A") };
            using var source = Flat(items);
            var grid = new TreeDataGrid { Model = source, Template = TestTemplates.TreeDataGridTemplate() };
            var presentation = grid.Presentation!;
            var rowChanges = 0;
            presentation.Rows.CollectionChanged += (_, _) => ++rowChanges;

            Assert.Null(presentation.SelectionInteraction);
            items.Add(new("B"));
            Assert.Equal(0, rowChanges);

            var window = new TestWindow(grid) { Styles = { TestTemplates.TreeDataGridRowStyle } };
            try
            {
                window.UpdateLayout();
                Assert.NotNull(presentation.SelectionInteraction);
                Assert.Equal(2, presentation.Rows.Count);
                items.Add(new("C"));
                Assert.Equal(1, rowChanges);
            }
            finally { window.Close(); }
        }

        [AvaloniaFact]
        public void Clearing_model_clears_the_Core_presentation()
        {
            using var source = Flat(new() { new("A") });
            var grid = new TreeDataGrid { Model = source };
            Assert.NotNull(grid.Presentation);

            grid.Model = null;

            Assert.Null(grid.Presentation);
            Assert.Null(grid.Columns);
            Assert.Null(grid.Rows);
        }

        [AvaloniaFact]
        public void Presentation_key_changed_while_detached_is_applied_on_reattach()
        {
            using var source = Flat(new() { new("A") });
            var options = new TreeDataGridPresentationOptions<Node>();
            options.Columns.Add("card", column => new TemplateColumn<Node>(column.Header,
                new Templates.FuncDataTemplate<Node>((model, _) => new TextBlock { Text = model?.Name })));
            var grid = new TreeDataGrid
            {
                Model = source,
                PresentationOptions = options,
                Template = TestTemplates.TreeDataGridTemplate(),
            };
            var window = new TestWindow(grid) { Styles = { TestTemplates.TreeDataGridRowStyle } };
            try
            {
                window.UpdateLayout();
                Assert.IsType<TextColumn<Node, string>>(grid.Columns![0]);
                window.Content = null;
                source.Columns[0].PresentationKey = "card";
                Assert.IsType<TextColumn<Node, string>>(grid.Columns[0]);
                window.Content = grid;
                window.UpdateLayout();
                Assert.IsType<TemplateColumn<Node>>(grid.Columns[0]);
            }
            finally { window.Close(); }
        }

        [AvaloniaFact]
        public void Views_share_Core_state_but_keep_layout_and_cell_objects_separate()
        {
            using var source = Flat(new() { new("A") });
            var second = new Core.Models.TextColumn<Node, string>("Other", x => x.Name);
            source.Columns.Add(second);
            using var firstView = new TreeDataGridPresentation<Node>(source);
            using var secondView = new TreeDataGridPresentation<Node>(source);
            var firstCellColumn = firstView.Columns[0];
            Assert.NotSame(firstCellColumn, secondView.Columns[0]);
            var firstBinding = ((TextColumn<Node, string>)firstCellColumn).Binding;
            var secondBinding = ((TextColumn<Node, string>)secondView.Columns[0]).Binding;
            Assert.NotSame(firstBinding, secondBinding);
            Assert.NotSame(firstBinding.Links, secondBinding.Links);
            firstBinding.Read = _ => "View override";
            Assert.Equal("A", secondBinding.Read!(source.Items.First()));
            Assert.Same(source.Rows[0], firstView.Rows[0]);
            Assert.Same(source.Rows[0], secondView.Rows[0]);
            firstView.Columns.SetColumnWidth(0, new GridLength(150));
            Assert.Equal(new Core.GridLength(150), source.Columns[0].Width);
            Assert.Equal(new GridLength(150), secondView.Columns[0].Width);
            source.Columns.Move(0, 1);
            Assert.Same(firstCellColumn, firstView.Columns[1]);
            source.Columns[1].IsVisible = false;
            source.Columns[1].IsVisible = true;
            Assert.Same(firstCellColumn, firstView.Columns[1]);
        }

        [AvaloniaFact]
        public void Row_notifications_use_Core_arguments_and_stop_on_disposal()
        {
            var items = new ObservableCollection<Node> { new("A") };
            using var source = Flat(items);
            System.Collections.Specialized.NotifyCollectionChangedEventArgs? modelArgs = null;
            source.Rows.CollectionChanged += (_, args) => modelArgs = args;
            var view = new TreeDataGridPresentation<Node>(source);
            var calls = 0;
            view.Rows.CollectionChanged += (_, args) => { ++calls; Assert.Same(modelArgs, args); };
            items.Add(new("B"));
            Assert.Equal(1, calls);
            view.Dispose();
            items.Add(new("C"));
            Assert.Equal(1, calls);
        }

        [AvaloniaFact]
        public void Delegate_columns_observe_the_model_and_edit_without_expression_compilation()
        {
            var item = new BoundValue { Value = "A" };
            using var source = new Core.FlatTreeDataGridSource<BoundValue>(new[] { item });
            Func<BoundValue, string> getter = x => x.Value;
            var column = Core.Models.ValueColumn<BoundValue, string>.FromDelegate("Value", getter,
                nameof(BoundValue.Value), (x, value) => x.Value = value);
            source.Columns.Add(column);
            Assert.Same(getter, column.Getter);
            Assert.Null(column.GetterExpression);
            using var view = new TreeDataGridPresentation<BoundValue>(source);
            var cell = view.Rows.RealizeCell(view.Columns[0], 0, 0);
            Assert.Equal("A", ((ITextCell)cell).Text);
            item.Value = "B";
            Assert.Equal("B", ((ITextCell)cell).Text);
            ((ITextCell)cell).Text = "C";
            Assert.Equal("C", item.Value);
            view.Rows.UnrealizeCell(cell, 0, 0);
        }

        [AvaloniaFact]
        public void Checkbox_presentations_preserve_two_and_three_state_editing()
        {
            var item = new BoundValue();
            using var source = new Core.FlatTreeDataGridSource<BoundValue>(new[] { item });
            source.Columns.Add(new Core.Models.CheckBoxColumn<BoundValue>("Two", x => x.Flag, (x, value) => x.Flag = value));
            source.Columns.Add(new Core.Models.CheckBoxColumn<BoundValue>("Three", x => x.OptionalFlag, (x, value) => x.OptionalFlag = value));
            using var view = new TreeDataGridPresentation<BoundValue>(source);
            var two = (CheckBoxCell)view.Rows.RealizeCell(view.Columns[0], 0, 0);
            var three = (CheckBoxCell)view.Rows.RealizeCell(view.Columns[1], 1, 0);
            Assert.False(two.IsThreeState);
            Assert.True(three.IsThreeState);
            two.Value = true;
            three.Value = null;
            Assert.True(item.Flag);
            Assert.Null(item.OptionalFlag);
            two.Dispose(); three.Dispose();
        }

        [AvaloniaFact]
        public void Automation_selects_Core_rows_and_moves_use_the_Core_source()
        {
            var items = new ObservableCollection<Node> { new("A"), new("B"), new("C") };
            using var source = Flat(items);
            var grid = new TreeDataGrid { Model = source, Template = TestTemplates.TreeDataGridTemplate() };
            var window = new TestWindow(grid) { Styles = { TestTemplates.TreeDataGridRowStyle } };
            try
            {
                window.UpdateLayout();
                var peer = new global::Avalonia.Controls.Automation.Peers.TreeDataGridAutomationPeer(grid);
                var row = new global::Avalonia.Controls.Automation.Peers.TreeDataGridRowAutomationPeer(grid.TryGetRow(1)!);
                row.Select();
                Assert.Equal(new Core.IndexPath(1), source.RowSelection!.SelectedIndex);
                Assert.True(row.IsSelected);
                Assert.Single(peer.GetSelection());
                row.RemoveFromSelection();
                Assert.Empty(source.RowSelection.SelectedIndexes);
                grid.Presentation!.MoveRows(new[] { new IndexPath(0) }, 2, TreeDataGridRowDropPosition.After, DragDropEffects.Move);
                Assert.Equal(new[] { "B", "C", "A" }, items.Select(x => x.Name));
            }
            finally { window.Close(); }
        }

        [AvaloniaFact]
        public void Named_templates_are_view_factories_over_Core_rows()
        {
            using var source = new Core.FlatTreeDataGridSource<Node>(new[] { new Node("A") });
            source.Columns.Add(new Core.Models.TemplateColumn<Node>("Card", "card", new Core.GridLength(125)));
            var options = new TreeDataGridPresentationOptions<Node>();
            options.Columns.Add("card", column => new TemplateColumn<Node>(column.Header,
                new Templates.FuncDataTemplate<Node>((model, _) => new TextBlock { Text = model?.Name })));
            using var view = new TreeDataGridPresentation<Node>(source, options);
            Assert.IsType<TemplateColumn<Node>>(view.Columns[0]);
            Assert.Equal(new GridLength(125), view.Columns[0].Width);
            var cell = view.Rows.RealizeCell(view.Columns[0], 0, 0);
            Assert.Same(source.Items.First(), cell.Value);
            view.Rows.UnrealizeCell(cell, 0, 0);
        }

        [AvaloniaFact]
        public void Expander_presentation_disposes_its_custom_inner_column()
        {
            using var source = new Core.HierarchicalTreeDataGridSource<Node>(new[] { new Node("A") });
            source.Columns.Add(new Core.Models.HierarchicalExpanderColumn<Node>(
                new Core.Models.TemplateColumn<Node>("Card", "disposable"), x => x.Children));
            var options = new TreeDataGridPresentationOptions<Node>();
            DisposableTemplateColumn? inner = null;
            options.Columns.Add("disposable", _ => inner = new DisposableTemplateColumn());

            var view = new TreeDataGridPresentation<Node>(source, options);
            view.Dispose();

            Assert.NotNull(inner);
            Assert.True(inner!.IsDisposed);
        }

        private sealed class BoundValue : INotifyPropertyChanged
        {
            private string _value = "";
            public string Value { get => _value; set { _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
            public bool Flag { get; set; }
            public bool? OptionalFlag { get; set; } = true;
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private sealed class DisposableTemplateColumn : TemplateColumn<Node>, IDisposable
        {
            public DisposableTemplateColumn()
                : base("Card", new Templates.FuncDataTemplate<Node>((model, _) =>
                    new TextBlock { Text = model?.Name }))
            {
            }

            public bool IsDisposed { get; private set; }
            public void Dispose() => IsDisposed = true;
        }

        [AvaloniaFact]
        public void V12_selection_mode_and_events_use_the_neutral_selection()
        {
            using var source = Flat(new() { new("A"), new("B") });
            source.Selection = null;
            var grid = new TreeDataGrid { SelectionMode = TreeDataGridSelectionMode.Row | TreeDataGridSelectionMode.Multiple, Model = source };
            Assert.NotNull(source.RowSelection);
            Assert.False(source.RowSelection!.SingleSelect);
            var eventCount = 0;
            grid.SelectionChanged += (_, e) =>
            {
                ++eventCount;
                Assert.Single(e.SelectedIndexes);
                Assert.Equal("B", ((Node)e.SelectedItems[0]!).Name);
            };
            var window = new TestWindow(grid);
            try
            {
                source.RowSelection.SelectedIndex = 1;
                Assert.Equal(1, eventCount);
                grid.SelectionMode = TreeDataGridSelectionMode.Row;
                Assert.True(source.RowSelection.SingleSelect);
                grid.Model = null;
            }
            finally { window.Close(); }
        }

        private static Core.FlatTreeDataGridSource<Node> Flat(ObservableCollection<Node> items)
        {
            var source = new Core.FlatTreeDataGridSource<Node>(items);
            source.Columns.Add(new Core.Models.TextColumn<Node, string>("Name", x => x.Name, (x, value) => x.Name = value));
            return source;
        }
        public sealed class Node
        {
            public Node(string name) => Name = name;
            public string Name { get; set; }
            public bool Expanded { get; set; }
            public ObservableCollection<Node> Children { get; } = new();
        }
    }
}
