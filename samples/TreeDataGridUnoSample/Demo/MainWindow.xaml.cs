using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TreeDataGridCore;
using TreeDataGridDemo.Models;
using TreeDataGridUnoSample.Demo.ViewModels;
using Uno.Controls;
using Uno.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace TreeDataGridUnoSample.Demo
{
    /// <summary>Uno counterpart of the Avalonia demo's MainWindow.</summary>
    public sealed partial class MainWindow : Page
    {
        private readonly DispatcherTimer _realizedCountTimer;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = ViewModel = new MainWindowViewModel();

            // The Avalonia demo opens on Countries (TabItem IsSelected="True").
            tabs.SelectedIndex = int.TryParse(Environment.GetEnvironmentVariable("TDG_START_TAB"), out var startTab) ? startTab : 2;

            _realizedCountTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _realizedCountTimer.Tick += (_, _) => UpdateRealizedCount();
            Loaded += (_, _) => _realizedCountTimer.Start();
            Unloaded += (_, _) => _realizedCountTimer.Stop();
        }

        internal MainWindowViewModel ViewModel { get; }

        internal TabView Tabs => tabs;

        public void AddCountryClick(object sender, RoutedEventArgs e)
        {
            var country = new Country(
                countryTextBox.Text,
                regionTextBox.Text,
                int.TryParse(populationTextBox.Text, out var population) ? population : 0,
                int.TryParse(areaTextBox.Text, out var area) ? area : 0,
                0,
                0,
                null,
                null,
                int.TryParse(gdpTextBox.Text, out var gdp) ? gdp : 0,
                null,
                null,
                null,
                null);
            ViewModel.Countries.AddCountry(country);

            var index = ViewModel.Countries.Source.Rows.Count - 1;
            countries.RowsPresenter?.BringIntoView(index);
            countries.TryGetRow(index)?.Focus(FocusState.Programmatic);
        }

        public void RemoveCountryClick(object sender, RoutedEventArgs e)
        {
            ViewModel.Countries.RemoveSelected();
        }

        public void ClearCountrySortClick(object sender, RoutedEventArgs e)
        {
            ViewModel.Countries.ClearSort();
        }

        public void RefreshCountryFilterClick(object sender, RoutedEventArgs e)
        {
            ViewModel.Countries.RefreshFilter();
        }

        public void People_SelectionChanged(object? sender, TreeDataGridSelectionChangedEventArgs e)
        {
            var currentSelection = (sender as TreeDataGrid)?.RowSelection?.SelectedItems
                .OfType<Person>()
                .Select(x => x.Name)
                .ToArray();

            peopleSelectionText.Text = currentSelection is { Length: > 0 }
                ? $"Selection: {string.Join(", ", currentSelection)}"
                : "Selection: none";
        }

        private void SelectedPath_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                ViewModel.Files.SelectedPath = ((TextBox)sender).Text;
            }
        }

        private void DragDrop_RowDragStarted(object? sender, TreeDataGridRowDragStartedEventArgs e)
        {
            foreach (DragDropItem i in e.Models)
            {
                if (!i.AllowDrag)
                    e.AllowedEffects = DataPackageOperation.None;
            }
        }

        private void DragDrop_RowDragOver(object? sender, TreeDataGridRowDragEventArgs e)
        {
            if (e.Position == TreeDataGridRowDropPosition.Inside &&
                e.TargetRow?.Model is DragDropItem i &&
                !i.AllowDrop)
                e.Inner.AcceptedOperation = DataPackageOperation.None;
        }

        private void UpdateRealizedCount()
        {
            var (treeDataGrid, textBlock) = tabs.SelectedIndex switch
            {
                // As in Avalonia, the People selection text also has the realized-count class.
                1 => (peopleXamlGrid, peopleSelectionText),
                2 => (countries, countriesRealizedCount),
                4 => (bringIntoViewNonUniformRowsGrid, bringIntoViewRealizedCount),
                5 => (fileViewer, filesRealizedCount),
                6 => (wikipedia, wikipediaRealizedCount),
                7 => (dragDrop, dragDropRealizedCount),
                _ => ((TreeDataGrid?)null, (TextBlock?)null),
            };
            var rows = treeDataGrid?.RowsPresenter;
            if (rows is null || textBlock is null)
                return;

            var realizedRowCount = rows.GetRealizedElements().Count();
            var unrealizedRowCount = VisualTreeHelper.GetChildrenCount(rows) - realizedRowCount;
            textBlock.Text = $"{realizedRowCount} rows realized ({unrealizedRowCount} unrealized)";
        }

        private void BringIntoViewNonUniformRows_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var treeDataGrid = bringIntoViewNonUniformRowsGrid;

            if (e.AddedItems.Count > 0)
            {
                var item = e.AddedItems[0];
                var source = treeDataGrid.Source;
                if (item is null || source is null)
                    return;
                var rowIndex = source.FindDisplayedRowIndex(item);

                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (rowIndex >= 0)
                        treeDataGrid.RowsPresenter?.BringIntoView(rowIndex);
                });
            }
        }
    }
}
