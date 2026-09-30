using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace TreeDataGridDemo
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            Bogus.Randomizer.Seed = new System.Random(0);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;

                if (int.TryParse(System.Environment.GetEnvironmentVariable("TDG_START_TAB"), out var startTab))
                {
                    mainWindow.FindControl<Avalonia.Controls.TabControl>("tabs")!.SelectedIndex = startTab;
                }

                if (DemoTour.IsEnabled)
                {
                    mainWindow.Opened += (_, _) => _ = DemoTour.RunAsync(mainWindow);
                }

                if (AotSmokeTest.IsEnabled(Program.Arguments))
                {
                    AotSmokeTest.Attach(mainWindow, desktop);
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
