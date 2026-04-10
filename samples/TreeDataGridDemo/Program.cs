using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.ReactiveUI;

namespace TreeDataGridDemo
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args)
        {
            Arguments = args;
            Stopwatch = Stopwatch.StartNew();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static string[] Arguments { get; private set; } = Array.Empty<string>();
        public static Stopwatch? Stopwatch;

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .UseReactiveUI()
                .LogToTrace();
    }
}
