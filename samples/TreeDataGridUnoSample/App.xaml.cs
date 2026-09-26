using System;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using TreeDataGridUnoSamples;

namespace TreeDataGridUnoSample;

public partial class App : Application
{
    private Window? _window;
    public App()
    {
        UnhandledException += (_, e) => { if (SampleRunContext.HasArgument("--smoke")) SampleRunContext.ReportResult(false, e.Exception.ToString()); };
        InitializeComponent();
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var page = new MainPage();
        _window = new Window { Content = page, Title = "TreeDataGrid — Uno / shared Core" };
        _window.Activate();
        if (SampleRunContext.HasArgument("--smoke")) _ = SmokeAsync(page);
    }
    private async Task SmokeAsync(MainPage page)
    {
        try
        {
            await Task.Delay(1500);
            // This route requires an external browser input driver. It is not
            // registered as an isolated native suite and cannot pass by calling
            // the grid's selection/edit/sort APIs from the fixture itself.
            if (SampleRunContext.HasArgument("--browser-input"))
            {
                if (!OperatingSystem.IsBrowser())
                    throw new PlatformNotSupportedException("The browser-input route requires its browser driver.");
                await BrowserInputRuntimeChecks.RunAsync(page);
                SampleRunContext.ReportResult(true);
                return;
            }
            if (await TryRunSelectedSuiteAsync(page)) return;
            page.VerifyInitialRender();
            page.Grid.SelectCell(1, 0);
            await CaptureAsync(page, "countries");
            page.Grid.Scroll!.ChangeView(300, 500, null, true);
            await Task.Delay(500);
            page.VerifyScrolledRender();
            await ShowcaseRuntimeChecks.RunAsync(page, CaptureAsync);
            await WikipediaRuntimeChecks.RunAsync(page, CaptureAsync);
            await FilesAndFindRuntimeChecks.RunAsync(page, CaptureAsync);
            await RuntimeChecks.RunAsync(page.Grid, (DataTemplate)page.Resources["RuntimeCellTemplate"]);
            await ViewportMeasurementRuntimeChecks.RunAsync(page.Grid);
            HeaderLifetimeRuntimeChecks.Run();
            await PresentationPoolRuntimeChecks.RunAsync(page.Grid);
            await SelectionRuntimeChecks.RunAsync(page.Grid, (Microsoft.UI.Xaml.Controls.ControlTemplate)page.Resources["AlternateGridTemplate"]);
            await SelectionInteractionRuntimeChecks.RunAsync(page);
            await ParityContractRuntimeChecks.RunSelectionHooksAsync(page);
            await ColumnSelectionRuntimeChecks.RunAsync(page);
            await ParityContractRuntimeChecks.RunTextOptionsAsync(page);
            await FocusRuntimeChecks.RunAsync(page);
            await EditingRuntimeChecks.RunAsync(page.Grid, (DataTemplate)page.Resources["RuntimeCellTemplate"], (DataTemplate)page.Resources["RuntimeEditingTemplate"]);
            // Exercise the same ownership boundaries in the sequential native
            // and published trimmed browser consumers, not only isolated hosts.
            await EditStartReentrancyRuntimeChecks.RunAsync(page);
            await EditCompletionReentrancyRuntimeChecks.RunAsync(page);
            await CellLifecycleRuntimeChecks.RunAsync(page.Grid);
            CellRetirementRuntimeChecks.Run();
            await CellRenderingRuntimeChecks.RunAsync(page);
            await CellContentLayoutRuntimeChecks.RunAsync(page);
            CellScalarReentrancyRuntimeChecks.Run();
            BindingWriteContractRuntimeChecks.Run();
            BindingSubscriptionLifetimeChecks.RunAll();
            await TypedBindingRuntimeChecks.RunAsync(page);
            await TypedRootLifetimeRuntimeChecks.RunAsync(page);
            HierarchyOwnershipRuntimeChecks.Run();
            await PresentationOptionsRuntimeChecks.RunAsync(page.Grid);
            await ValueColumnBaseRuntimeChecks.RunAsync(page.Grid);
            await CustomCellOptionsRuntimeChecks.RunAsync(page.Grid);
            await CustomCellWriteLifetimeRuntimeChecks.RunAsync(page.Grid);
            await BuiltInColumnComparisonRuntimeChecks.RunAsync(page.Grid);
            await BuiltInBindingRuntimeChecks.RunAsync(page.Grid);
            await ColumnCompatibilityRuntimeChecks.RunAsync(page.Grid, (DataTemplate)page.Resources["RuntimeCellTemplate"], (DataTemplate)page.Resources["RuntimeEditingTemplate"]);
            await SourceExtensionsRuntimeChecks.RunAsync(page.Grid, (DataTemplate)page.Resources["RuntimeCellTemplate"], (DataTemplate)page.Resources["RuntimeEditingTemplate"]);
            await DeclarativeRuntimeChecks.RunAsync(page);
            await BindingLifetimeRuntimeChecks.RunAsync(page.Grid, (DataTemplate)page.Resources["RuntimeCellTemplate"]);
            await SourceCompatibilityRuntimeChecks.RunAsync(page.Grid);
            await AutomationRuntimeChecks.RunAsync(page.Grid);
            await TextSearchRuntimeChecks.RunAsync(page);
            await AppearanceRuntimeChecks.RunAsync(page);
            await ElementFactoryRuntimeChecks.RunAsync(page.Grid);
            StandaloneCellRuntimeChecks.Run((DataTemplate)page.Resources["RuntimeCellTemplate"]);
            await StandaloneRowRuntimeChecks.RunAsync(page);
            // Validate retirement/creation callbacks in real published consumers
            // as well as fresh isolated native processes. Do not skip failures
            // in the browser or replace these checks with build-only coverage.
            RowLifetimeRuntimeChecks.Run();
            RowConstructionRuntimeChecks.Run();
            await PublicExpanderRuntimeChecks.RunAsync(page);
            await CustomExpanderLifetimeRuntimeChecks.RunAsync(page);
            await GenericPresenterRuntimeChecks.RunAsync(page);
            await SpecializedCellRuntimeChecks.RunAsync(page.Grid, (DataTemplate)page.Resources["RuntimeCellTemplate"],
                (DataTemplate)page.Resources["RuntimeEditingTemplate"], (Microsoft.UI.Xaml.Controls.ControlTemplate)page.Resources["CompatibleTextCellTemplate"],
                (Microsoft.UI.Xaml.Controls.ControlTemplate)page.Resources["CompatibleTemplateCellTemplate"]);
            await ExpanderFactoryRuntimeChecks.RunAsync(page.Grid, (DataTemplate)page.Resources["RuntimeCellTemplate"], (DataTemplate)page.Resources["RuntimeEditingTemplate"]);
            await CustomReuseRuntimeChecks.RunAsync(page.Grid);
            await ColumnSizingRuntimeChecks.RunAsync(page.Grid);
            await RowSizingRuntimeChecks.RunAsync(page.Grid, (DataTemplate)page.Resources["WrappingTemplate"]);
            await ViewportCacheRuntimeChecks.RunAsync(page.Grid);
            Console.WriteLine("UNO_CORE_SAMPLE_SMOKE_PASSED");
            SampleRunContext.ReportResult(true);
            CompleteNativeValidation();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            SampleRunContext.ReportResult(false, error.ToString());
            if (!OperatingSystem.IsBrowser()) Environment.Exit(1);
        }
    }
    private void CompleteNativeValidation()
    {
#if !__WASM__
        // Browser results are delivered through SampleRunContext. Native-only
        // APIs must also be absent from its compilation, not merely unreachable
        // behind a runtime check: Uno validates API availability at build time.
#if TREEDATAGRID_SKIA
        // Application.Exit requests immediate host termination; on X11 that can
        // race its render thread. Closing the last native window instead lets
        // the host stop and join the render loop before terminating normally.
        DispatcherShutdownMode = DispatcherShutdownMode.OnLastWindowClose;
        _window?.Close();
#else
        Exit();
#endif
#endif
    }

    private static async Task CaptureAsync(UIElement element, string name)
    {
        var args = SampleRunContext.Arguments;
        var index = Array.IndexOf(args, "--screenshot-dir");
        if (index < 0 || index + 1 >= args.Length) return;
        var directory = Path.GetFullPath(args[index + 1]);
        Directory.CreateDirectory(directory);
        if (element is FrameworkElement frameworkElement) frameworkElement.UpdateLayout();
        await Task.Delay(75);
#if __WASM__
        throw new PlatformNotSupportedException("The DOM renderer requires browser-driver screenshots; RenderTargetBitmap is unavailable.");
#else
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element);
        using var image = SKImage.FromPixelCopy(new SKImageInfo(bitmap.PixelWidth, bitmap.PixelHeight,
            SKColorType.Bgra8888, SKAlphaType.Premul), (await bitmap.GetPixelsAsync()).ToArray());
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var path = Path.Combine(directory, name + ".png");
        File.WriteAllBytes(path, png.ToArray());
        Console.WriteLine($"UNO_SCREENSHOT: {path} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
#endif
    }
}
