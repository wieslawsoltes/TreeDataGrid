using System;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;

namespace TreeDataGridUnoSample;

public partial class App : Application
{
    private Window? _window;
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var page = new MainPage();
        _window = new Window { Content = page, Title = "TreeDataGrid — Uno / shared Core" };
        _window.Activate();
        if (Environment.GetCommandLineArgs().Contains("--smoke")) _ = SmokeAsync(page);
    }
    private async Task SmokeAsync(MainPage page)
    {
        try
        {
            await Task.Delay(1500);
            page.VerifyInitialRender();
            page.Grid.SelectCell(1, 0);
            await CaptureAsync(page, "countries");
            page.Grid.Scroll.ChangeView(300, 500, null, true);
            await Task.Delay(500);
            page.VerifyScrolledRender();
            await ShowcaseRuntimeChecks.RunAsync(page, CaptureAsync);
            await WikipediaRuntimeChecks.RunAsync(page, CaptureAsync);
            await FilesAndFindRuntimeChecks.RunAsync(page, CaptureAsync);
            await RuntimeChecks.RunAsync(page.Grid, (DataTemplate)page.Resources["RuntimeCellTemplate"]);
            await SelectionRuntimeChecks.RunAsync(page.Grid, (Microsoft.UI.Xaml.Controls.ControlTemplate)page.Resources["AlternateGridTemplate"]);
            await EditingRuntimeChecks.RunAsync(page.Grid, (DataTemplate)page.Resources["RuntimeCellTemplate"], (DataTemplate)page.Resources["RuntimeEditingTemplate"]);
            await ColumnSizingRuntimeChecks.RunAsync(page.Grid);
            await RowSizingRuntimeChecks.RunAsync(page.Grid, (DataTemplate)page.Resources["WrappingTemplate"]);
            Console.WriteLine("UNO_CORE_SAMPLE_SMOKE_PASSED");
            Exit();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Environment.Exit(1);
        }
    }
    private static async Task CaptureAsync(UIElement element, string name)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "--screenshot-dir");
        if (index < 0 || index + 1 >= args.Length) return;
        var directory = Path.GetFullPath(args[index + 1]);
        Directory.CreateDirectory(directory);
        if (element is FrameworkElement frameworkElement) frameworkElement.UpdateLayout();
        await Task.Delay(75);
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element);
        using var image = SKImage.FromPixelCopy(new SKImageInfo(bitmap.PixelWidth, bitmap.PixelHeight,
            SKColorType.Bgra8888, SKAlphaType.Premul), (await bitmap.GetPixelsAsync()).ToArray());
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var path = Path.Combine(directory, name + ".png");
        File.WriteAllBytes(path, png.ToArray());
        Console.WriteLine($"UNO_SCREENSHOT: {path} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
    }
}
