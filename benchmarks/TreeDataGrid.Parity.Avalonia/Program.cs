using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using TreeDataGridCore;
using GridControl = global::Avalonia.Controls.TreeDataGrid;

namespace TreeDataGrid.Parity.Avalonia;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) => AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}

public sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
        Styles.Add(new global::Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://TreeDataGrid.Parity.Avalonia/"))
            { Source = new Uri("avares://TreeDataGrid.Avalonia/Themes/Fluent.axaml") });
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var host = new NativeHost();
            var window = new Window { Width = 900, Height = 620, Content = host.Grid, Title = "Avalonia native parity" };
            window.Opened += async (_, _) =>
            {
                var code = 0;
                try { await Task.Delay(500); await ParityWorkload.RunAsync(host); }
                catch (Exception error) { Console.Error.WriteLine(error); code = 1; }
                finally { host.Dispose(); }
                desktop.Shutdown(code);
            };
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class NativeHost : INativeParityHost
{
    private ScrollViewer? _scroll;
    private TreeDataGridRowsPresenter? _rows;
    public GridControl Grid { get; } = new()
    {
        Width = ParityWorkload.Width, Height = ParityWorkload.Height,
        FontSize = 14, FontFamily = new FontFamily("DejaVu Sans"), ShowColumnHeaders = false,
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        BorderThickness = new Thickness(0),
    };
    public NativeHost() => Grid.Styles.Add(new Style(selector => selector.OfType<TreeDataGridRow>())
        { Setters = { new Setter(Layoutable.HeightProperty, (double)ParityWorkload.RowHeight) } });
    public string Framework => "Avalonia";
    public string UiAssembly => typeof(Application).Assembly.FullName!;
    public void Bind(FlatTreeDataGridSource<BenchRow> source)
    {
        Grid.Model = source;
        Grid.UpdateLayout();
        _scroll = Grid.GetVisualDescendants().OfType<ScrollViewer>().Single(value => value.Name == "PART_ScrollViewer");
        _rows = Grid.GetVisualDescendants().OfType<TreeDataGridRowsPresenter>().Single();
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        _rows.CacheLength = 0;
    }
    public void Scroll(double x, double y) => _scroll!.Offset = new Vector(x, y);
    public void UpdateLayout() => Grid.UpdateLayout();
    public Frame Inspect(FlatTreeDataGridSource<BenchRow> source, double x, double y)
    {
        var scroll = _scroll!;
        var rows = _rows!.GetRealizedElements().OfType<TreeDataGridRow>().Where(row => row.RowIndex >= 0).ToArray();
        var cells = rows.SelectMany(row => row.CellsPresenter!.GetRealizedElements()).OfType<TreeDataGridCell>().Where(cell => cell.RowIndex >= 0).ToArray();
        string? error = null;
        if (Math.Abs(scroll.Offset.X - x) > 0.5 || Math.Abs(scroll.Offset.Y - y) > 0.5) error = "offset";
        if (Math.Abs(scroll.Viewport.Width - ParityWorkload.Width) > 0.5 || Math.Abs(scroll.Viewport.Height - ParityWorkload.Height) > 0.5) error = "viewport";
        if (rows.Length is 0 or > 64 || cells.Length is 0 or > 1024) error = "bounded realization";
        var first = (int)(y / ParityWorkload.RowHeight);
        if (Grid.TryGetRow(first) is null || Grid.TryGetRow(first + 14) is null) error = "coverage";
        foreach (var row in rows)
        {
            if (!ReferenceEquals(row.Model, source.Rows[row.RowIndex].Model)) { error = "row identity"; break; }
            if (Math.Abs(row.Bounds.Height - ParityWorkload.RowHeight) > 0.5) { error = "row height"; break; }
        }
        foreach (var cell in cells)
        {
            var model = (BenchRow)source.Rows[cell.RowIndex].Model!;
            if (!cell.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == model.Label)) { error = "stale text"; break; }
            if (Math.Abs(cell.Bounds.Width - source.Columns[cell.ColumnIndex].Width.Value) > 0.5) { error = "column width"; break; }
        }
        return new(scroll.Offset.X, scroll.Offset.Y, scroll.Viewport.Width, scroll.Viewport.Height, rows.Length, cells.Length, error);
    }
    public void Dispose() => Grid.Model = null;
}
