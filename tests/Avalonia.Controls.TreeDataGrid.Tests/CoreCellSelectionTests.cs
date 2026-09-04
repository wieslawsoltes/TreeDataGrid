using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls.Presentation;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Core = global::TreeDataGridCore;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests
{
    public class CoreCellSelectionTests
    {
        [AvaloniaFact]
        public void Presentation_maps_sorted_rows_and_hidden_columns_without_legacy_sources()
        {
            using var source = Flat();
            using var selection = new Core.Selection.TreeDataGridCellSelectionModel<Node>(source);
            source.Selection = selection;
            source.Columns[0].IsVisible = false;
            source.SortBy(source.Columns[1], ListSortDirection.Ascending);
            using var presentation = new TreeDataGridPresentation<Node>(source);
            selection.SelectedIndex = new(1, 1); // A, first visible cell
            Assert.True(presentation.SelectionInteraction!.IsCellSelected(0, 0));
            Assert.False(presentation.SelectionInteraction.IsCellSelected(0, 1));
            Assert.False(presentation.SelectionInteraction.IsCellSelected(-1, 0));
            Assert.False(presentation.SelectionInteraction.IsCellSelected(0, 20));
            Assert.Null(presentation.SelectedIndexes); // Cell selection does not enable row dragging.
            Assert.False(presentation.SelectionInteraction.IsRowSelected(0));
            source.ClearSort();
            Assert.True(presentation.SelectionInteraction.IsCellSelected(0, 1));
        }

        [AvaloniaFact]
        public void Selection_replacement_and_disposal_unsubscribe_old_interactions()
        {
            using var source = Flat();
            using var first = new Core.Selection.TreeDataGridCellSelectionModel<Node>(source);
            source.Selection = first;
            using var presentation = new TreeDataGridPresentation<Node>(source);
            var stale = 0;
            presentation.SelectionInteraction!.SelectionChanged += (_, _) => ++stale;
            first.SelectedIndex = new(0, 0);
            Assert.Equal(1, stale);
            using var second = new Core.Selection.TreeDataGridCellSelectionModel<Node>(source);
            source.Selection = second;
            first.SelectedIndex = new(1, 1);
            Assert.Equal(1, stale);
            var changes = 0;
            presentation.SelectionInteraction!.SelectionChanged += (_, _) => ++changes;
            second.SelectedIndex = new(0, 1);
            Assert.Equal(1, changes);
            presentation.Dispose();
            second.Clear();
            Assert.Equal(1, changes);
        }

        [AvaloniaFact]
        public void Keyboard_selection_extends_backwards_and_restores_after_detach()
        {
            using var source = Flat();
            using var selection = new Core.Selection.TreeDataGridCellSelectionModel<Node>(source) { SingleSelect = false };
            source.Selection = selection;
            var grid = new TreeDataGrid { Model = source, Template = TestTemplates.TreeDataGridTemplate() };
            var window = new TestWindow(grid) { Styles = { TestTemplates.TreeDataGridRowStyle } };
            try
            {
                window.UpdateLayout();
                selection.SelectedIndex = new(1, 1);
                Press(Key.Up, KeyModifiers.Shift);
                Press(Key.Left, KeyModifiers.Shift);
                Assert.Equal(4, selection.Count);
                Assert.True(selection.IsSelected(new(0, 0)));
                Press(Key.Down, KeyModifiers.None);
                Assert.Single(selection.SelectedIndexes);
                window.Content = null;
                Assert.Null(grid.Presentation!.SelectionInteraction);
                selection.SelectedIndex = new(0, 0);
                window.Content = grid;
                window.UpdateLayout();
                Assert.True(grid.Presentation.SelectionInteraction!.IsCellSelected(0, 0));
                Assert.Null(grid.Source);
            }
            finally { window.Close(); }
            void Press(Key key, KeyModifiers modifiers)
            {
                var e = new KeyEventArgs { Key = key, KeyModifiers = modifiers, RoutedEvent = InputElement.KeyDownEvent };
                grid.Presentation!.SelectionInteraction!.OnKeyDown(grid, e);
                Assert.True(e.Handled);
            }
        }

        [AvaloniaFact]
        public void Pointer_selection_paints_cells_and_honors_cancellation_and_right_click()
        {
            using var source = Flat();
            using var selection = new Core.Selection.TreeDataGridCellSelectionModel<Node>(source) { SingleSelect = false };
            source.Selection = selection;
            var grid = new TreeDataGrid { Model = source, Template = TestTemplates.TreeDataGridTemplate() };
            var window = new TestWindow(grid) { Styles = { TestTemplates.TreeDataGridRowStyle } };
            try
            {
                window.UpdateLayout();
                var interaction = grid.Presentation!.SelectionInteraction!;
                var cell = Assert.IsAssignableFrom<TreeDataGridCell>(grid.TryGetRow(1)!.TryGetCell(1));
                var pointer = new Pointer(0, PointerType.Mouse, true);
                var cancel = true;
                grid.SelectionChanging += (_, e) => e.Cancel = cancel;
                Press(false);
                Assert.Empty(selection.SelectedIndexes);
                cancel = false;
                Press(false);
                Assert.Equal(new Core.CellIndex(1, 1), selection.SelectedIndex);
                Assert.True(cell.IsSelected);
                Assert.False(Assert.IsAssignableFrom<TreeDataGridCell>(grid.TryGetRow(0)!.TryGetCell(0)).IsSelected);
                selection.SetSelectedRange(new(0, 0), 2, 2);
                Press(true);
                Assert.Equal(4, selection.Count);
                interaction.OnPointerReleased(grid, new PointerReleasedEventArgs(cell, pointer, grid,
                    new Point(2, 2), 1, new PointerPointProperties(RawInputModifiers.None,
                    PointerUpdateKind.RightButtonReleased), KeyModifiers.None, MouseButton.Right));
                Assert.Equal(4, selection.Count);
                void Press(bool right) => interaction.OnPointerPressed(grid, new PointerPressedEventArgs(
                    cell, pointer, grid, new Point(2, 2), 0,
                    new PointerPointProperties(right ? RawInputModifiers.RightMouseButton : RawInputModifiers.LeftMouseButton,
                        right ? PointerUpdateKind.RightButtonPressed : PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
            }
            finally { window.Close(); }
        }

        [AvaloniaTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void Native_columns_preserve_fixed_and_auto_measurement(bool auto)
        {
            using var source = Flat();
            source.Columns.Clear();
            var width = auto ? Core.GridLength.Auto : new Core.GridLength(80);
            var model = new Core.Models.TextColumn<Node, string>("Name", x => x.Name, width);
            model.PresentationKey = "custom";
            source.Columns.Add(model);
            var options = new TreeDataGridPresentationOptions<Node>();
            options.Columns.Add("custom", _ => new NativeColumn(auto ? GridLength.Auto : new GridLength(80)));
            var grid = new TreeDataGrid
            {
                PresentationOptions = options, Model = source,
                Template = TestTemplates.TreeDataGridTemplate(), ElementFactory = new TestElementFactory()
            };
            var window = new TestWindow(grid) { Styles = { TestTemplates.TreeDataGridRowStyle } };
            try
            {
                window.UpdateLayout();
                var cell = Assert.IsType<LayoutTestCellControl>(grid.TryGetRow(0)!.TryGetCell(0));
                Assert.NotEmpty(cell.MeasureConstraints);
                Assert.Equal(auto ? double.PositiveInfinity : 80, cell.MeasureConstraints[0].Width);
                Assert.Equal(auto, ((IColumnMeasurementOptions)grid.Columns![0]).RequiresUnconstrainedWidthMeasurement);
                Assert.False(grid.Columns[0] is IColumn<Node>);
                Assert.True(typeof(IColumnMeasurementOptions).IsPublic);
            }
            finally { window.Close(); }
        }

        private static Core.FlatTreeDataGridSource<Node> Flat() => new(new ObservableCollection<Node> { new("B"), new("A") })
        {
            Columns = { new Core.Models.TextColumn<Node, string>("One", x => x.Name), new Core.Models.TextColumn<Node, string>("Two", x => x.Name) }
        };
        private sealed record Node(string Name);
        private sealed class NativeColumn : CellColumnBase<Node>
        {
            public NativeColumn(GridLength width) : base("Name", width, new CellColumnOptions()) { }
            public override ICell CreateCell(Core.Models.IRow<Node> row) => new LayoutTestCell(row.Model.Name);
        }
    }
}
