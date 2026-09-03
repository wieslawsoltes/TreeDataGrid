using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TreeDataGridDemo.Models;

namespace TreeDataGridCoreDemo;

public sealed partial class MainWindow : Window
{
    internal MainWindow(CoreDemoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public MainWindow()
        : this(new CoreDemoViewModel())
    {
    }

    private CoreDemoViewModel ViewModel => (CoreDemoViewModel)DataContext!;

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ApplyCountryFilter(object? sender, RoutedEventArgs e) =>
        ViewModel.ApplyCountryFilter(this.FindControl<TextBox>("CountryFilter")?.Text);

    private void ClearCountrySort(object? sender, RoutedEventArgs e) =>
        ViewModel.CountriesSource.ClearSort();

    private void AddCountry(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddCountry();
        var grid = this.FindControl<TreeDataGrid>("CountriesGrid");
        Dispatcher.UIThread.Post(() =>
        {
            var last = ViewModel.CountriesSource.Rows.Count - 1;
            if (last >= 0)
                grid?.RowsPresenter?.BringIntoView(last);
        }, DispatcherPriority.Loaded);
    }

    private void RemoveCountry(object? sender, RoutedEventArgs e) =>
        ViewModel.RemoveSelectedCountry();

    private void FindCountry(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.OfType<Country>().FirstOrDefault() is not { } country)
            return;

        var index = -1;
        for (var i = 0; i < ViewModel.FindSource.Rows.Count; ++i)
        {
            if (ReferenceEquals(ViewModel.FindSource.Rows[i].Model, country))
            {
                index = i;
                break;
            }
        }

        ViewModel.FindStatus = index >= 0
            ? $"Model index {country.Name}: displayed Core row {index}"
            : $"{country.Name} is filtered out";
        if (index >= 0)
            this.FindControl<TreeDataGrid>("FindGrid")?.RowsPresenter?.BringIntoView(index);
    }

    private void BringCountryIntoView(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.OfType<Country>().FirstOrDefault() is not { } country)
            return;

        var index = ViewModel.VariableCountryItems.IndexOf(country);
        if (index >= 0)
        {
            Dispatcher.UIThread.Post(() =>
                this.FindControl<TreeDataGrid>("VariableGrid")?.RowsPresenter?.BringIntoView(index),
                DispatcherPriority.Loaded);
        }
    }

    private void DragStarted(object? sender, TreeDataGridRowDragStartedEventArgs e)
    {
        if (e.Models.OfType<DragDropItem>().Any(x => !x.AllowDrag))
            e.AllowedEffects = DragDropEffects.None;
    }

    private void DragOver(object? sender, TreeDataGridRowDragEventArgs e)
    {
        if (e.Position == TreeDataGridRowDropPosition.Inside &&
            e.TargetRow?.Model is DragDropItem item && !item.AllowDrop)
        {
            e.Inner.DragEffects = DragDropEffects.None;
        }
    }
}
