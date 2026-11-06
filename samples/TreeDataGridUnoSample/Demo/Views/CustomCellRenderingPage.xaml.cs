using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridUnoSamples;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Primitives;
using UnoTextColumn = Uno.Controls.Models.TreeDataGrid.TextColumn<TreeDataGridUnoSample.Demo.Views.CustomCellRow, string>;

namespace TreeDataGridUnoSample.Demo.Views;

public sealed class CustomCellRow(int index)
{
    public int Index { get; } = index;
    public string Label { get; } = $"Row {index:D5}";
    public string Code { get; } = $"{index * 7919 % 100000:D5}";
}

/// <summary>Compares default template cells with custom-drawn cells from a custom element factory.</summary>
public sealed partial class CustomCellRenderingPage : UserControl
{
    private const int Rows = 10_000;
    private const int Columns = 32;
    private const double ColumnWidth = 128;
    private const int Warmup = 10;
    private const int Steps = 60;
    private readonly FlatTreeDataGridSource<CustomCellRow> _source;
    private bool _running;

    public CustomCellRenderingPage()
    {
        InitializeComponent();
        _source = new FlatTreeDataGridSource<CustomCellRow>(
            new ObservableCollection<CustomCellRow>(Enumerable.Range(0, Rows).Select(index => new CustomCellRow(index))));
        for (var column = 0; column < Columns; ++column)
        {
            var header = $"C{column:D2}";
            _source.Columns.Add(column % 2 == 0
                ? new UnoTextColumn(header, row => row.Label, new Microsoft.UI.Xaml.GridLength(ColumnWidth))
                : new UnoTextColumn(header, row => row.Code, new Microsoft.UI.Xaml.GridLength(ColumnWidth)));
        }
        grid.Source = _source;
        // TDG_CUSTOM_CELLS=skia starts with the custom cells; TDG_CUSTOM_CELLS_BENCHMARK=1 runs the
        // comparison once the page is shown (for headless runs).
        if (Environment.GetEnvironmentVariable("TDG_CUSTOM_CELLS") == "skia" && SkiaTextCellElementFactory.IsSupported)
            renderingMode.SelectedIndex = 1;
        if (Environment.GetEnvironmentVariable("TDG_CUSTOM_CELLS_BENCHMARK") == "1")
            Loaded += async (_, _) => { await Task.Delay(500); Benchmark_Click(this, new RoutedEventArgs()); };
        if (!SkiaTextCellElementFactory.IsSupported)
        {
            renderingMode.IsEnabled = false;
            status.Text = "Custom Skia cells need a Skia renderer target.";
        }
    }

    private void RenderingMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (grid is null || _running) return;
        UseSkiaCells(renderingMode.SelectedIndex == 1);
    }

    private void UseSkiaCells(bool skia) =>
        grid.ElementFactory = skia ? new SkiaTextCellElementFactory() : new TreeDataGridElementFactory();

    private async void Benchmark_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        _running = true;
        benchmarkButton.IsEnabled = false;
        renderingMode.IsEnabled = false;
        try
        {
            var modes = SkiaTextCellElementFactory.IsSupported ? new[] { false, true } : new[] { false };
            var measured = new Dictionary<(bool Skia, string Operation), double>();
            foreach (var skia in modes)
            {
                UseSkiaCells(skia);
                foreach (var operation in new[] { "Vertical scroll", "Horizontal scroll", "Diagonal jump" })
                {
                    status.Text = $"Measuring {operation.ToLowerInvariant()} with {(skia ? "Skia" : "template")} cells…";
                    measured[(skia, operation)] = await MeasureAsync(operation);
                }
            }
            results.Text = Format(measured, modes.Length == 2);
            Console.WriteLine("TDG_CUSTOM_CELLS_BENCHMARK\n" + results.Text);
            status.Text = $"Median of {Steps} steps after {Warmup} warm-up steps (ChangeView + UpdateLayout).";
        }
        finally
        {
            UseSkiaCells(renderingMode.SelectedIndex == 1);
            grid.Scroll?.ChangeView(0, 0, null, true);
            benchmarkButton.IsEnabled = true;
            renderingMode.IsEnabled = SkiaTextCellElementFactory.IsSupported;
            _running = false;
        }
    }

    private async Task<double> MeasureAsync(string operation)
    {
        var scroll = grid.Scroll ?? throw new InvalidOperationException("The grid has no scroll viewer.");
        scroll.ChangeView(0, 0, null, true);
        grid.UpdateLayout();
        await Task.Delay(200);
        var rowHeight = grid.RowsPresenter?.GetRowHeight(0) is > 0 and var height ? height : 28;
        var samples = new List<double>(Steps);
        var random = new Random(521);
        for (var step = 0; step < Warmup + Steps; ++step)
        {
            var (x, y) = operation switch
            {
                "Vertical scroll" => (0d, rowHeight * 3 * (step + 1)),
                "Horizontal scroll" => (ColumnWidth * ((step + 1) % (Columns - 6)), 0d),
                _ => (ColumnWidth * random.Next(Columns - 6), rowHeight * random.Next(Rows - 50)),
            };
            var started = Stopwatch.GetTimestamp();
            scroll.ChangeView(x, y, null, true);
            grid.UpdateLayout();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (step >= Warmup) samples.Add(elapsed);
            // Let the frame render outside the measured interval.
            await Task.Delay(16);
        }
        samples.Sort();
        return samples[samples.Count / 2];
    }

    private static string Format(Dictionary<(bool Skia, string Operation), double> measured, bool both)
    {
        var text = new StringBuilder();
        text.AppendLine(both ? "Operation          Template   Skia   Ratio" : "Operation          Template");
        foreach (var operation in measured.Keys.Select(key => key.Operation).Distinct())
        {
            var template = measured[(false, operation)];
            text.Append($"{operation,-18} {template,7:F2}ms");
            if (both)
            {
                var skia = measured[(true, operation)];
                text.Append($" {skia,6:F2}ms {skia / template,6:F2}");
            }
            text.AppendLine();
        }
        return text.ToString();
    }
}
