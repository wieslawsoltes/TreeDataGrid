using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using TreeDataGridDemo.ViewModels;

namespace TreeDataGridDemo
{
    /// <summary>
    /// Opt-in scripted walkthrough (TDG_TOUR=1) used to compare this demo with the Uno demo.
    /// Each step prints "TOUR_STEP name" once the UI has settled so an external tool can
    /// capture the on-screen window. The Uno sample runs the identical step list.
    /// </summary>
    internal static class DemoTour
    {
        public static bool IsEnabled => Environment.GetEnvironmentVariable("TDG_TOUR") == "1";

        public static async Task RunAsync(MainWindow window)
        {
            var vm = (MainWindowViewModel)window.DataContext!;
            var tabs = window.FindControl<TabControl>("tabs")!;
            var names = new[] { "template", "people", "countries", "find", "bringintoview", "files", "wikipedia", "dragdrop" };

            await Task.Delay(1500);
            for (var i = 0; i < names.Length; ++i)
            {
                tabs.SelectedIndex = i;
                await Step($"tab{i}-{names[i]}", i == 6 ? 15000 : 1500);
            }

            tabs.SelectedIndex = 2;
            var countries = vm.Countries.Source;
            ((ITreeDataGridSource)countries).SortBy(countries.Columns[1], ListSortDirection.Descending);
            await Step("countries-sort-desc");
            countries.ClearSort();
            vm.Countries.FilterText = "europe";
            await Step("countries-filter");
            vm.Countries.FilterText = string.Empty;
            countries.RowSelection!.Select(new IndexPath(0));
            countries.RowSelection.Select(new IndexPath(2));
            countries.RowSelection.Select(new IndexPath(4));
            await Step("countries-select");
            vm.Countries.CellSelection = true;
            countries.CellSelection!.SetSelectedRange(new CellIndex(1, new IndexPath(1)), 2, 3);
            await Step("countries-cellselect");
            vm.Countries.CellSelection = false;
            var countriesGrid = window.FindControl<TreeDataGrid>("countries")!;
            countriesGrid.RowSelection!.SelectedIndex = new IndexPath(0);
            await BeginEditAsync(countriesGrid, 2, 0);
            await Step("countries-edit-template");
            await CancelEditAsync(countriesGrid, 2, 0);
            await BeginEditAsync(countriesGrid, 1, 1);
            await Step("countries-edit-text");
            await CancelEditAsync(countriesGrid, 1, 1);

            tabs.SelectedIndex = 1;
            var people = window.FindControl<TreeDataGrid>("peopleXamlGrid")!;
            people.RowSelection!.Select(new IndexPath(0));
            people.RowSelection.Select(new IndexPath(2));
            await Step("people-select");
            ((TreeDataGridCore.Models.IExpander)people.Rows![0]).IsExpanded = false;
            await Step("people-collapse");

            tabs.SelectedIndex = 7;
            var dragDrop = (HierarchicalTreeDataGridSource<Models.DragDropItem>)vm.DragDrop.Source;
            dragDrop.Expand(new IndexPath(0));
            await Step("dragdrop-expand");

            tabs.SelectedIndex = 5;
            var files = window.FindControl<TreeDataGrid>("fileViewer")!;
            await Task.Delay(500);
            ((TreeDataGridCore.Models.IExpander)files.Rows![1]).IsExpanded = true;
            await Step("files-expand");
            vm.Files.FlatList = true;
            await Step("files-flat");
            vm.Files.FlatList = false;

            tabs.SelectedIndex = 3;
            await Task.Delay(500);
            var list = SelectedTabContent(tabs).GetLogicalDescendants<ListBox>("displayedRowCountryList");
            list!.SelectedIndex = 150;
            await Step("find-select");

            tabs.SelectedIndex = 4;
            await Task.Delay(500);
            var bringList = SelectedTabContent(tabs).GetLogicalDescendants<ListBox>(null);
            bringList!.SelectedIndex = 150;
            await Step("bringintoview-select");

            Console.WriteLine("TOUR_DONE");
            await Task.Delay(500);
            window.Close();
        }

        private static async Task Step(string name, int settle = 1500)
        {
            await Task.Delay(settle);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Console.WriteLine($"TOUR_STEP {name}");
            await Task.Delay(1200);
        }

        private static async Task BeginEditAsync(TreeDataGrid grid, int column, int row)
        {
            await Task.Delay(300);
            var cell = grid.TryGetCell(column, row)!;
            cell.Focus();
            cell.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.F2 });
        }

        private static async Task CancelEditAsync(TreeDataGrid grid, int column, int row)
        {
            var target = (Avalonia.Input.IInputElement?)TopLevel.GetTopLevel(grid)?.FocusManager?.GetFocusedElement() ?? grid.TryGetCell(column, row)!;
            ((Avalonia.Interactivity.Interactive)target).RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape });
            await Task.Delay(300);
        }

        private static Control SelectedTabContent(TabControl tabs) => (Control)((TabItem)tabs.SelectedItem!).Content!;

        private static T? GetLogicalDescendants<T>(this Control root, string? name) where T : Control =>
            Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root).OfType<T>()
                .FirstOrDefault(x => (name is null || x.Name == name) && x.IsEffectivelyVisible);
    }
}
