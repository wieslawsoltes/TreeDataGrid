using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Threading;
using TreeDataGridDemo.Models;
using TreeDataGridDemo.ViewModels;

namespace TreeDataGridDemo
{
    internal static class AotSmokeTest
    {
        private const string V12XamlArg = "--smoke-test-v12-xaml";
        private const string ResultFileName = "aot-smoke-test-result.txt";

        public static bool IsEnabled(string[] args)
        {
            foreach (var arg in args)
            {
                if (string.Equals(arg, V12XamlArg, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static void Attach(MainWindow mainWindow, IClassicDesktopStyleApplicationLifetime desktop)
        {
            void OnOpened(object? sender, EventArgs e)
            {
                mainWindow.Opened -= OnOpened;
                Dispatcher.UIThread.Post(() => Run(mainWindow, desktop), DispatcherPriority.Background);
            }

            mainWindow.Opened += OnOpened;
        }

        private static void Run(MainWindow mainWindow, IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                ValidateV12XamlGrid(mainWindow);
                WriteResult("PASS");
                Console.WriteLine("AOT smoke test passed.");
                desktop.Shutdown(0);
            }
            catch (Exception ex)
            {
                var message = $"FAIL{Environment.NewLine}{ex}";
                WriteResult(message);
                Console.Error.WriteLine(message);
                desktop.Shutdown(1);
            }
        }

        private static void ValidateV12XamlGrid(MainWindow mainWindow)
        {
            var viewModel = mainWindow.DataContext as MainWindowViewModel
                ?? throw new InvalidOperationException("MainWindow DataContext was not initialized.");
            var grid = mainWindow.FindControl<TreeDataGrid>("peopleXamlGrid")
                ?? throw new InvalidOperationException("Could not find the v12 XAML TreeDataGrid.");

            if (grid.Source is not HierarchicalTreeDataGridSource<object> source)
                throw new InvalidOperationException("The v12 XAML TreeDataGrid did not generate a hierarchical source.");

            if (grid.Columns?.Count != 5)
                throw new InvalidOperationException($"Expected 5 columns, found {grid.Columns?.Count ?? 0}.");

            if (grid.Rows is null || grid.Rows.Count < 3)
                throw new InvalidOperationException("The generated source did not realize the expected rows.");

            var root = viewModel.PeopleXaml.People[0];

            if (root.Name != "Eleanor Pope")
                throw new InvalidOperationException("Unexpected root row model.");

            var activeCell = grid.Rows.RealizeCell(grid.Columns[4], 4, 0) as CheckBoxCell
                ?? throw new InvalidOperationException("Could not realize the Active checkbox cell.");

            try
            {
                activeCell.Value = false;

                if (root.IsActive)
                    throw new InvalidOperationException("Compiled checkbox binding did not write back to the row model.");

                activeCell.Value = true;

                if (!root.IsActive)
                    throw new InvalidOperationException("Compiled checkbox binding did not restore the row model value.");
            }
            finally
            {
                grid.Rows.UnrealizeCell(activeCell, 4, 0);
            }

            source.Collapse(new IndexPath(0));

            if (root.IsExpanded)
                throw new InvalidOperationException("Compiled IsExpanded binding did not write back on collapse.");

            source.Expand(new IndexPath(0));

            if (!root.IsExpanded)
                throw new InvalidOperationException("Compiled IsExpanded binding did not write back on expand.");
        }

        private static void WriteResult(string message)
        {
            var path = Path.Combine(AppContext.BaseDirectory, ResultFileName);
            File.WriteAllText(path, message);
        }
    }
}
