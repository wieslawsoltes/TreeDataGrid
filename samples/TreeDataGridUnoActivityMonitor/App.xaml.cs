using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace TreeDataGridUnoActivityMonitor;

public partial class App : Application
{
    private Window? _window;
    public App() => InitializeComponent();
    public static void InitializeLogging() { }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var page = new MainPage();
        _window = new Window { Content = page, Title = "Activity Monitor — Uno / shared Core" };
        _window.Closed += (_, _) => page.Stop();
        _window.Activate();
        if (Environment.GetCommandLineArgs().Contains("--smoke")) _ = SmokeAsync(page);
    }

    private async Task SmokeAsync(MainPage page)
    {
        try
        {
            await Task.Delay(1000);
            await ActivityMonitorRuntimeChecks.RunAsync(page);
            page.Stop();
            Console.WriteLine("UNO_ACTIVITY_MONITOR_SMOKE_PASSED");
            Exit();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Environment.Exit(1);
        }
    }
}
