using System;
using System.Linq;
using Microsoft.UI.Xaml;

namespace TreeDataGrid.WinUI.Tests;

/// <summary>Runs the linked xunit tests on the XAML UI thread once the application has started.</summary>
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        // An exception escaping a XAML callback would otherwise end the process; the test that
        // caused it still fails through its own call into the framework.
        UnhandledException += (_, e) =>
        {
            Console.Error.WriteLine($"Unhandled exception: {e.Message}{Environment.NewLine}{e.Exception}");
            e.Handled = true;
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs();
        var filter = arguments.SkipWhile(a => a != "--filter").Skip(1).FirstOrDefault();
        var failed = 1;
        try
        {
            failed = await XunitInProcessRunner.RunAsync(typeof(App).Assembly, filter, Console.Out);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
        }
        Console.Out.Flush();
        Environment.Exit(failed == 0 ? 0 : 1);
    }
}
