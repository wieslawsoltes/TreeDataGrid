using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using TreeDataGridUnoSamples;

namespace TreeDataGridUnoActivityMonitor;

public partial class App : Application
{
    private Window? _window;
    public App()
    {
        UnhandledException += (_, e) => { if (SampleRunContext.HasArgument("--smoke")) SampleRunContext.ReportResult(false, e.Exception.ToString()); };
        InitializeComponent();
    }
    public static void InitializeLogging() { }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var page = new MainPage();
        _window = new Window { Content = page, Title = "Activity Monitor — Uno / shared Core" };
        _window.Closed += (_, _) => page.Stop();
        _window.Activate();
        if (SampleRunContext.HasArgument("--smoke")) _ = SmokeAsync(page);
    }

    private async Task SmokeAsync(MainPage page)
    {
        try
        {
            await Task.Delay(1000);
            await ActivityMonitorRuntimeChecks.RunAsync(page);
            page.Stop();
            Console.WriteLine("UNO_ACTIVITY_MONITOR_SMOKE_PASSED");
            SampleRunContext.ReportResult(true);

#if !__WASM__
            Exit();
#endif
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            SampleRunContext.ReportResult(false, error.ToString());
            if (!OperatingSystem.IsBrowser()) Environment.Exit(1);
        }
    }
}
