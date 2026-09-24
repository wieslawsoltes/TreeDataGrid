using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Core = TreeDataGridCore;
using CoreModels = TreeDataGridCore.Models;
using CellColumn = Uno.Controls.Presentation.CellColumn;
using BeginEditGestures = Uno.Controls.Models.TreeDataGrid.BeginEditGestures;

namespace TreeDataGridUnoSample;

/// <summary>
/// Observes actual browser-dispatched input. Setup and assertions use public APIs;
/// only Playwright sends selection, navigation, edit, sort, resize and wheel input.
/// </summary>
internal static class BrowserInputRuntimeChecks
{
    internal static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        var items = Enumerable.Range(0, 100).Select(index => new Item(index)).ToArray();
        using var source = new Core.FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new CoreModels.TextColumn<Item, string>("Name", item => item.Name,
            (item, value) => item.Name = value ?? string.Empty, width: new(240)));
        source.Columns.Add(new CoreModels.TextColumn<Item, string>("Kind", item => item.Kind, width: new(200)));
        source.Columns.Add(new CoreModels.TextColumn<Item, string>("Code", item => item.Code, width: new(240)));
        var grid = new global::Uno.Controls.TreeDataGrid
        {
            Width = 600, Height = 400,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            SelectionMode = global::Uno.Controls.TreeDataGridSelectionMode.MultipleRows,
            CanUserResizeColumns = true,
            CanUserSortColumns = true,
            Model = source,
        };
        var container = new Border { Padding = new Thickness(24), Child = grid };
        var phase = "initial-layout";
        try
        {
            foreach (var column in grid.Presentation!.Columns.Cast<CellColumn>())
                column.EditGestures = BeginEditGestures.F2;
            page.Content = container;
            await Until(() => grid.TryGetCell(0, 5) is { ActualWidth: > 0, ActualHeight: > 0 } &&
                grid.ColumnHeadersPresenter?.TryGetElement(0) is { ActualWidth: > 0 });

            Emit("select-row", grid.TryGetCell(0, 1));
            await Until(() => source.RowSelection!.SelectedIndex == new Core.IndexPath(1));
            Emit("arrow-down");
            await Until(() => source.RowSelection!.SelectedIndex == new Core.IndexPath(2));
            Emit("begin-edit");
            await Until(() => grid.EditingCell is { IsEditing: true, RowIndex: 2 });
            Emit("commit-edit", text: "Browser edited");
            await Until(() => items[2].Name == "Browser edited" && grid.EditingCell is null);

            Emit("select-cancel-row", grid.TryGetCell(0, 3));
            await Until(() => source.RowSelection!.SelectedIndex == new Core.IndexPath(3));
            Emit("begin-cancel-edit");
            await Until(() => grid.EditingCell is { IsEditing: true, RowIndex: 3 });
            Emit("cancel-edit", text: "Must not persist");
            await Until(() => grid.EditingCell is null);
            Check(items[3].Name == "Item 003", "Escape committed the cancelled editor text.");

            Emit("ctrl-select", grid.TryGetCell(0, 5));
            await Until(() => source.RowSelection!.Count == 2 &&
                source.RowSelection.IsSelected(new Core.IndexPath(3)) &&
                source.RowSelection.IsSelected(new Core.IndexPath(5)));

            var header = grid.ColumnHeadersPresenter!.TryGetElement(0)!;
            var thumb = FindVisual<Thumb>(header) ??
                throw new InvalidOperationException("The resize-enabled header has no native Thumb.");
            var oldWidth = grid.Presentation!.Columns[0].ActualWidth;
            Emit("resize-column", thumb);
            await Until(() => grid.Presentation!.Columns[0].ActualWidth >= oldWidth + 20);
            Check(grid.Presentation!.Columns[0].ActualWidth <= oldWidth + 80,
                "Column dragging applied a device-pixel delta as a layout-pixel delta.");

            header = grid.ColumnHeadersPresenter!.TryGetElement(0)!;
            Emit("sort-column", header, fractionX: 0.25);
            await Until(() => source.IsSorted && ReferenceEquals(source.Rows[0].Model, items[2]));
            Check(items[2].Name == "Browser edited", "Header sorting lost the committed model value.");

            Emit("wheel-scroll", grid.Scroll);
            await Until(() => grid.Scroll!.VerticalOffset > 0);
            Check(grid.RowsPresenter!.RealizedRows.Count is > 0 and < 50 && source.Rows.Count == 100,
                "Browser wheel scrolling lost the source or unboundedly realized rows.");
            Console.WriteLine("UNO_BROWSER_INPUT_PASSED: pointer selection, ArrowDown, F2, typed commit, Escape cancellation, Ctrl selection, Thumb resize, header sort and wheel virtualization");
        }
        finally
        {
            grid.Model = null;
            container.Child = null;
            page.Content = previous;
        }

        void Emit(string name, FrameworkElement? hit = null, string? text = null, double fractionX = 0.5)
        {
            phase = name;
            grid.UpdateLayout();
            Point point = default;
            if (hit is not null)
            {
                Check(hit.ActualWidth > 0 && hit.ActualHeight > 0, "Input target has no usable native bounds: " + name);
                point = hit.TransformToVisual(page).TransformPoint(new Point(hit.ActualWidth * fractionX, hit.ActualHeight / 2));
                Check(double.IsFinite(point.X) && double.IsFinite(point.Y), "Input target has nonfinite coordinates.");
            }
            Console.WriteLine("UNO_BROWSER_INPUT_STEP=" + JsonSerializer.Serialize(
                new BrowserInputStep(name, point.X, point.Y, text), BrowserInputJsonContext.Default.BrowserInputStep));
        }
        async Task Until(Func<bool> condition)
        {
            var watch = Stopwatch.StartNew();
            while (true)
            {
                grid.UpdateLayout();
                if (condition()) return;
                if (watch.Elapsed > TimeSpan.FromSeconds(30))
                    throw new TimeoutException($"Browser input '{phase}' did not reach its asserted state: selected={source.RowSelection?.SelectedIndex}, count={source.RowSelection?.Count}, editingRow={grid.EditingCell?.RowIndex}, offset={grid.Scroll?.VerticalOffset}.");
                // Poll an observable condition, not a fixed wait that assumes
                // input or layout succeeded. A missing browser driver must fail.
                await Task.Delay(25);
            }
        }
    }

    private static T? FindVisual<T>(DependencyObject owner) where T : FrameworkElement
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(owner); ++index)
        {
            var child = VisualTreeHelper.GetChild(owner, index);
            if (child is T match) return match;
            if (FindVisual<T>(child) is { } descendant) return descendant;
        }
        return null;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed class Item(int index) : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs NameChanged = new(nameof(Name));
        private string _name = $"Item {index:D3}";
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; PropertyChanged?.Invoke(this, NameChanged); } }
        }
        public string Kind => "Browser input row";
        public string Code => index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

internal sealed record BrowserInputStep(string name, double x, double y, string? text);
[JsonSerializable(typeof(BrowserInputStep))]
internal partial class BrowserInputJsonContext : JsonSerializerContext { }
