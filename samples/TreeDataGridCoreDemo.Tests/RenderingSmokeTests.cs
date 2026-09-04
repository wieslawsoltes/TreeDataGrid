using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TreeDataGridCoreDemo.Tests.TestApplication))]

namespace TreeDataGridCoreDemo.Tests;

public sealed class TestApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://TreeDataGridCoreDemo.Tests/"))
        {
            Source = new Uri("avares://TreeDataGrid.Avalonia/Themes/Fluent.axaml")
        });
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>()
            .UseHarfBuzz()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class RenderingSmokeTests
{
    [AvaloniaFact]
    public void Full_Core_demo_renders_without_legacy_source_adapters()
    {
        var viewModel = new CoreDemoViewModel(loadRemoteContent: false);
        var window = new MainWindow(viewModel);
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var grids = new[]
            {
                "CountriesGrid", "FindGrid", "VariableGrid", "FilesGrid",
                "WikipediaGrid", "DragDropGrid", "PeopleGrid", "TemplateGrid"
            }.Select(name => window.FindControl<TreeDataGrid>(name)!).ToArray();
            Assert.Equal(8, grids.Length);
            Assert.All(grids, grid =>
            {
                Assert.NotNull(grid.Model);
                Assert.Null(grid.Source);
                Assert.NotNull(grid.Presentation);
            });

            var tabs = window.FindControl<TabControl>("Tabs")!;
            for (var i = 0; i < grids.Length; ++i)
            {
                tabs.SelectedIndex = i;
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(grids[i].RowsPresenter);
            }

            var people = viewModel.PeopleSource.Items.First();
            var expandedCount = viewModel.PeopleSource.Rows.Count;
            people.Expansion.IsExpanded = false;
            Assert.True(viewModel.PeopleSource.Rows.Count < expandedCount);
            people.Expansion.IsExpanded = true;
            Assert.Equal(expandedCount, viewModel.PeopleSource.Rows.Count);

            tabs.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Save(window, "core-demo-countries.png");
            tabs.SelectedIndex = 6;
            Dispatcher.UIThread.RunJobs();
            Save(window, "core-demo-people.png");
            tabs.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    private static void Save(TopLevel topLevel, string fileName)
    {
        var frame = topLevel.CaptureRenderedFrame() ??
            throw new InvalidOperationException("No rendered frame was captured.");
        var root = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(AppContext.BaseDirectory, "headless-screenshots");
        Directory.CreateDirectory(root);
        var path = Path.GetFullPath(Path.Combine(root, fileName));
        frame.Save(path);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }
}
