using System;
using Uno.Controls;
using Avalonia.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Linq;
using TreeDataGridDemo.Models;
using TreeDataGridDemo.ViewModels;
using Windows.System;

namespace TreeDataGridUnoSample;

public sealed partial class MainPage : Page
{
    private readonly DispatcherTimer _realizedCountTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(500),
    };

    public MainPage()
    {
        ViewModel = new MainWindowViewModel();
        InitializeComponent();
        DataContext = ViewModel;

        _realizedCountTimer.Tick += OnRealizedCountTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    internal MainWindowViewModel ViewModel { get; }

    private void AddCountryClick(object sender, RoutedEventArgs e)
    {
        var country = new Country(
            countryTextBox.Text ?? string.Empty,
            regionTextBox.Text ?? string.Empty,
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
        var row = countries.RowsPresenter?.BringIntoView(index);
        row?.Focus(FocusState.Programmatic);
        _ = DispatcherQueue.TryEnqueue(UpdateSelectedRealizedCount);
    }

    private void RemoveCountryClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Countries.RemoveSelected();
        _ = DispatcherQueue.TryEnqueue(UpdateSelectedRealizedCount);
    }

    private void SelectedPath_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
            return;

        ViewModel.Files.SelectedPath = selectedPathTextBox.Text;
        _ = DispatcherQueue.TryEnqueue(UpdateSelectedRealizedCount);
        e.Handled = true;
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        QueueSelectedTabLayoutRefresh();
    }

    private void DragDrop_RowDragStarted(object? sender, TreeDataGridRowDragStartedEventArgs e)
    {
        foreach (var item in e.Models.OfType<DragDropItem>())
        {
            if (!item.AllowDrag)
            {
                e.AllowedEffects = DragDropEffects.None;
                break;
            }
        }
    }

    private void DragDrop_RowDragOver(object? sender, TreeDataGridRowDragEventArgs e)
    {
        if (e.Position == TreeDataGridRowDropPosition.Inside &&
            e.TargetRow?.Model is DragDropItem item &&
            !item.AllowDrop)
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _realizedCountTimer.Start();
        QueueSelectedTabLayoutRefresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _realizedCountTimer.Stop();
    }

    private void OnRealizedCountTimerTick(object? sender, object e)
    {
        UpdateSelectedRealizedCount();
    }

    private void QueueSelectedTabLayoutRefresh()
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            RefreshSelectedTabLayout();
            UpdateSelectedRealizedCount();
        });
    }

    private void RefreshSelectedTabLayout()
    {
        if (tabs.SelectedItem is TabViewItem { Content: FrameworkElement content })
        {
            content.InvalidateMeasure();
            content.InvalidateArrange();
            content.UpdateLayout();
        }

        if (GetSelectedGrid() is not { } grid)
            return;

        grid.InvalidateMeasure();
        grid.InvalidateArrange();
        grid.ColumnHeadersPresenter?.InvalidateMeasure();
        grid.ColumnHeadersPresenter?.InvalidateArrange();
        grid.RowsPresenter?.InvalidateMeasure();
        grid.RowsPresenter?.InvalidateArrange();
        grid.Scroll?.InvalidateMeasure();
        grid.Scroll?.InvalidateArrange();
        grid.UpdateLayout();
        grid.Scroll?.UpdateLayout();
    }

    private TreeDataGrid? GetSelectedGrid()
    {
        return tabs.SelectedIndex switch
        {
            0 => peopleXamlGrid,
            1 => countries,
            2 => fileViewer,
            3 => wikipedia,
            4 => dragDrop,
            _ => null,
        };
    }

    private void UpdateSelectedRealizedCount()
    {
        switch (tabs.SelectedIndex)
        {
            case 0:
                UpdateRealizedCount(peopleXamlGrid, peopleXamlRealizedCount);
                break;
            case 1:
                UpdateRealizedCount(countries, countriesRealizedCount);
                break;
            case 2:
                UpdateRealizedCount(fileViewer, filesRealizedCount);
                break;
            case 3:
                UpdateRealizedCount(wikipedia, wikipediaRealizedCount);
                break;
            case 4:
                UpdateRealizedCount(dragDrop, dragDropRealizedCount);
                break;
        }
    }

    private static void UpdateRealizedCount(TreeDataGrid grid, TextBlock textBlock)
    {
        var rows = grid.RowsPresenter;
        var realized = rows?.GetRealizedElements().Count() ?? 0;
        var unrealized = rows is null ? 0 : rows.Children.Count - realized;
        textBlock.Text = $"{realized} rows realized ({unrealized} unrealized)";
    }
}
