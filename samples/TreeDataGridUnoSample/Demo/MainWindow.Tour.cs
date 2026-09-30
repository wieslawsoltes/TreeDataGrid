using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TreeDataGridCore;
using TreeDataGridCore.Selection;
using TreeDataGridDemo.Models;
using IndexPath = TreeDataGridCore.IndexPath;

namespace TreeDataGridUnoSample.Demo
{
    /// <summary>
    /// Opt-in scripted walkthrough (TDG_TOUR=1) mirroring samples/TreeDataGridDemo/DemoTour.cs.
    /// Each step prints "TOUR_STEP name" once the UI has settled so an external tool can
    /// capture the on-screen window.
    /// </summary>
    public sealed partial class MainWindow
    {
        internal static bool IsTourEnabled => Environment.GetEnvironmentVariable("TDG_TOUR") == "1";

        internal async Task RunTourAsync()
        {
            var vm = ViewModel;
            var names = new[] { "template", "people", "countries", "find", "bringintoview", "files", "wikipedia", "dragdrop" };

            await Task.Delay(1500);
            for (var i = 0; i < names.Length; ++i)
            {
                tabs.SelectedIndex = i;
                await Step($"tab{i}-{names[i]}", i == 6 ? 15000 : 1500);
            }

            tabs.SelectedIndex = 2;
            var countries = vm.Countries.Source;
            countries.SortBy(countries.Columns[1], ListSortDirection.Descending);
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
            ((TreeDataGridCellSelectionModel<Country>)countries.Selection!).SetSelectedRange(new CellIndex(1, new IndexPath(1)), 2, 3);
            await Step("countries-cellselect");
            vm.Countries.CellSelection = false;
            countries.RowSelection!.SelectedIndex = new IndexPath(0);
            await Task.Delay(300);
            this.countries.BeginEdit(0, 2);
            await Step("countries-edit-template");
            this.countries.CancelEdit();
            await Task.Delay(300);
            this.countries.BeginEdit(1, 1);
            await Step("countries-edit-text");
            this.countries.CancelEdit();
            await Task.Delay(300);

            tabs.SelectedIndex = 1;
            peopleXamlGrid.RowSelection!.Select(new IndexPath(0));
            peopleXamlGrid.RowSelection.Select(new IndexPath(2));
            await Step("people-select");
            ((TreeDataGridCore.Models.IExpander)peopleXamlGrid.Source!.Rows[0]).IsExpanded = false;
            await Step("people-collapse");

            tabs.SelectedIndex = 7;
            var dragDrop = (HierarchicalTreeDataGridSource<DragDropItem>)vm.DragDrop.Source;
            dragDrop.Expand(new IndexPath(0));
            await Step("dragdrop-expand");

            tabs.SelectedIndex = 5;
            await Task.Delay(500);
            ((TreeDataGridCore.Models.IExpander)fileViewer.Source!.Rows[1]).IsExpanded = true;
            await Step("files-expand");
            vm.Files.FlatList = true;
            await Step("files-flat");
            vm.Files.FlatList = false;

            tabs.SelectedIndex = 3;
            await Task.Delay(500);
            var list = FindDescendants<ListView>(this).First(x => x.Name == "displayedRowCountryList");
            list.SelectedIndex = 150;
            await Step("find-select");

            tabs.SelectedIndex = 4;
            await Task.Delay(500);
            bringIntoViewList.SelectedIndex = 150;
            await Step("bringintoview-select");

            Console.WriteLine("TOUR_DONE");
            await Task.Delay(500);
            Application.Current.Exit();
        }

        private static async Task Step(string name, int settle = 1500)
        {
            await Task.Delay(settle);
            Console.WriteLine($"TOUR_STEP {name}");
            await Task.Delay(1200);
        }

        private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; ++i)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                    yield return match;
                foreach (var nested in FindDescendants<T>(child))
                    yield return nested;
            }
        }
    }
}
