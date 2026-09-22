using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using Windows.Foundation;
using UI = Uno.Controls.Models.TreeDataGrid;
using GridLength = Microsoft.UI.Xaml.GridLength;

namespace TreeDataGridUnoSample;

/// <summary>Checks that vertical range changes do not dirty unaffected cell subtrees.</summary>
internal static class ViewportMeasurementRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        grid.Model = null;
        var previousFactory = grid.ElementFactory;
        var previousOptions = grid.PresentationOptions;
        var previousWidth = grid.Width;
        var previousHeight = grid.Height;
        var previousRowHeight = grid.RowHeight;
        var factory = new CountingFactory();
        var columnMeasurements = new Dictionary<int, int>();
        var presentationOptions = new TreeDataGridPresentationOptions();
        presentationOptions.Columns["MeasureCount"] = column => new MeasurementColumn(
            (TreeDataGridCore.Models.ValueColumn<Item, string?>)column, columnMeasurements);
        var items = new ObservableCollection<Item>(
            Enumerable.Range(0, 1000).Select(index => new Item($"Row {index}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        for (var column = 0; column < 16; ++column)
            Uno.Controls.TreeDataGridSourceExtensions.WithTextColumn(source, $"Column {column}", x => x.Name,
                options => { options.Width = new GridLength(100); options.IsReadOnly = true; });
        source.Columns[0].PresentationKey = "MeasureCount";
        try
        {
            grid.Width = 440;
            grid.Height = 260;
            grid.RowHeight = 28;
            grid.ElementFactory = factory;
            grid.PresentationOptions = presentationOptions;
            grid.Model = source;
            await Settle();
            grid.Scroll!.ChangeView(0, 280, null, true);
            await Settle();
            var retained = grid.RowsPresenter!.RealizedCells.OfType<CountingTextCell>()
                .ToDictionary(cell => cell, cell => (cell.RowModel, cell.ColumnIndex, cell.Measures));
            Check(retained.Count > 0, "The measurement fixture did not realize counting cells.");

            var previousMeasurements = new Dictionary<int, int>(columnMeasurements);
            grid.Scroll.ChangeView(null, 308, null, true);
            await Settle();
            var unchanged = grid.RowsPresenter.RealizedCells.OfType<CountingTextCell>()
                .Where(cell => retained.TryGetValue(cell, out var old) &&
                    ReferenceEquals(old.RowModel, cell.RowModel) && old.ColumnIndex == cell.ColumnIndex)
                .ToArray();
            Check(unchanged.Length > 0, "The vertical shift did not preserve any row/cell identities.");
            var unnecessary = unchanged.Sum(cell => cell.Measures - retained[cell].Measures);
            Check(unnecessary == 0,
                $"Vertical-only scrolling remeasured {unnecessary} unchanged fixed-width cell subtrees.");
            var unnecessaryColumnMeasures = unchanged.Where(cell => cell.ColumnIndex == 0)
                .Sum(cell => columnMeasurements.GetValueOrDefault(cell.RowIndex) - previousMeasurements.GetValueOrDefault(cell.RowIndex));
            Check(unnecessaryColumnMeasures == 0,
                $"Vertical-only scrolling repeated {unnecessaryColumnMeasures} unchanged column measurement callbacks.");
            VerifyValues();

            // No-change viewport updates must not hide genuine content invalidation.
            var live = unchanged.First(cell => cell.ColumnIndex == 0);
            var before = live.Measures;
            ((Item)live.RowModel!).Name = "Updated after vertical scroll";
            await Settle();
            Check(live.Measures > before, "A live value change did not remeasure its retained native cell.");
            VerifyValues();

            before = live.Measures;
            source.Columns[0].Width = new TreeDataGridCore.GridLength(140);
            await Settle();
            Check(live.Measures > before && Math.Abs(live.ActualWidth - 140) < 1,
                "Column resizing failed to invalidate retained cell geometry.");

            grid.Scroll.ChangeView(600, null, null, true);
            await Settle();
            VerifyValues();
            Check(grid.RowsPresenter.RealizedCells.Any(cell => cell.ColumnIndex >= 6),
                "Horizontal scrolling failed to realize the new column range.");
            Check(grid.RowsPresenter.RealizedCells.Count < 180,
                "Viewport optimization disabled bounded two-axis realization.");
            Console.WriteLine($"UNO_RUNTIME_VIEWPORT_MEASUREMENT_PASSED: retained={unchanged.Length}; verticalOnlyMeasures={unnecessary}; columnMeasurements={unnecessaryColumnMeasures}; live content/width/horizontal changes preserved");
        }
        finally
        {
            grid.Model = null;
            grid.ElementFactory = previousFactory;
            grid.PresentationOptions = previousOptions;
            grid.RowHeight = previousRowHeight;
            grid.Width = previousWidth;
            grid.Height = previousHeight;
        }

        void VerifyValues()
        {
            foreach (var cell in grid.RowsPresenter!.RealizedCells)
            {
                var model = items[cell.RowIndex];
                Check(ReferenceEquals(cell.RowModel, model), "A cell retained the wrong shared Core model.");
                var text = ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>()
                    .FirstOrDefault(part => part.Name == "PART_Text");
                Check(text?.Text == model.Name, "A retained native TextBlock did not display the current model value.");
            }
        }
        async Task Settle() { await Task.Delay(100); grid.UpdateLayout(); }
    }

    private sealed class MeasurementColumn(
        TreeDataGridCore.Models.ValueColumn<Item, string?> column, Dictionary<int, int> measurements)
        : UI.TextColumn<Item, string>(column)
    {
        public override double CellMeasured(double width, int rowIndex)
        {
            measurements[rowIndex] = measurements.GetValueOrDefault(rowIndex) + 1;
            return base.CellMeasured(width, rowIndex);
        }
    }
    private sealed partial class CountingTextCell : TreeDataGridTextCell
    {
        public int Measures { get; private set; }
        protected override Size MeasureOverride(Size availableSize)
        {
            ++Measures;
            return base.MeasureOverride(availableSize);
        }
    }
    private sealed class CountingFactory : TreeDataGridElementFactory
    {
        protected override Control CreateElement(object? data) =>
            data is UI.ITextCell ? new CountingTextCell() : base.CreateElement(data);
        protected override string GetDataRecycleKey(object? data) =>
            data is UI.ITextCell ? nameof(CountingTextCell) : base.GetDataRecycleKey(data);
        protected override string GetElementRecycleKey(Control element) =>
            element is CountingTextCell ? nameof(CountingTextCell) : base.GetElementRecycleKey(element);
    }
    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                PropertyChanged?.Invoke(this, new(nameof(Name)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
