using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TreeDataGridCore;
using GridControl = global::Uno.Controls.TreeDataGrid;
using CellControl = global::Uno.Controls.Primitives.TreeDataGridCell;

namespace TreeDataGrid.Parity.Uno;

public partial class App : Application
{
    private Window? _window;
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var host = new NativeHost();
        _window = new Window { Content = host.Grid, Title = "Uno native parity" };
        _window.Activate();
        _ = RunAsync(host);
    }
    private static async Task RunAsync(NativeHost host)
    {
        var code = 0;
        try { await Task.Delay(500); await ParityWorkload.RunAsync(host); }
        catch (Exception error) { Console.Error.WriteLine(error); code = 1; }
        finally { host.Dispose(); }
        Environment.Exit(code);
    }
}

internal sealed class NativeHost : INativeParityHost
{
    public GridControl Grid { get; } = new()
    {
        Width = ParityWorkload.Width, Height = ParityWorkload.Height,
        RowHeight = ParityWorkload.RowHeight, FontSize = 14,
        FontFamily = new FontFamily("DejaVu Sans"), ShowColumnHeaders = false,
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        BorderThickness = new Thickness(0), RequestedTheme = ElementTheme.Light,
    };
    public string Framework => "Uno";
    public string UiAssembly => typeof(Application).Assembly.FullName!;
    public void Bind(FlatTreeDataGridSource<BenchRow> source)
    {
        Grid.Model = source;
        Grid.UpdateLayout();
        var scroll = Grid.Scroll ?? throw new InvalidOperationException("The Uno scroll template is missing.");
        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        Grid.RowsPresenter!.CacheLength = 0;
    }
    public void Scroll(double x, double y) => Grid.Scroll!.ChangeView(x, y, null, true);
    public void UpdateLayout() => Grid.UpdateLayout();
    public Frame Inspect(FlatTreeDataGridSource<BenchRow> source, double x, double y)
    {
        var scroll = Grid.Scroll!;
        var rows = Grid.RowsPresenter!.RealizedRows;
        var cells = Grid.RowsPresenter.RealizedCells;
        string? error = null;
        if (Math.Abs(scroll.HorizontalOffset - x) > 0.5 || Math.Abs(scroll.VerticalOffset - y) > 0.5) error = "offset";
        if (Math.Abs(scroll.ViewportWidth - ParityWorkload.Width) > 0.5 || Math.Abs(scroll.ViewportHeight - ParityWorkload.Height) > 0.5) error = "viewport";
        if (rows.Count is 0 or > 64 || cells.Count is 0 or > 1024) error = "bounded realization";
        var first = (int)(y / ParityWorkload.RowHeight);
        if (Grid.TryGetRow(first) is null || Grid.TryGetRow(first + 14) is null) error = "coverage";
        foreach (var row in rows)
        {
            if (!ReferenceEquals(row.Model, source.Rows[row.RowIndex].Model)) { error = "row identity"; break; }
            if (Math.Abs(row.ActualHeight - ParityWorkload.RowHeight) > 0.5) { error = "row height"; break; }
        }
        foreach (var cell in cells)
        {
            var model = (BenchRow)source.Rows[cell.RowIndex].Model!;
            if (!ReferenceEquals(cell.RowModel, model)) { error = "cell identity"; break; }
            if (!ContainsText(cell, model.Label)) { error = "stale text"; break; }
            if (Math.Abs(cell.ActualWidth - source.Columns[cell.ColumnIndex].Width.Value) > 0.5) { error = "column width"; break; }
        }
        return new(scroll.HorizontalOffset, scroll.VerticalOffset, scroll.ViewportWidth, scroll.ViewportHeight, rows.Count, cells.Count, error);
    }
    private static bool ContainsText(DependencyObject root, string expected)
    {
        if (root is TextBlock text && text.Text == expected) return true;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); ++i)
            if (ContainsText(VisualTreeHelper.GetChild(root, i), expected)) return true;
        return false;
    }
    public void Dispose() => Grid.Model = null;
}
