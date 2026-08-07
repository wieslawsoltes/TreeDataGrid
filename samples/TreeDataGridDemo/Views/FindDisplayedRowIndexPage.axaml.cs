using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TreeDataGridDemo.Internal.TreeDataGrid;
using TreeDataGridDemo.Models;
using TreeDataGridDemo.ViewModels;

namespace TreeDataGridDemo.Views;

public partial class FindDisplayedRowIndexPage : UserControl
{
    private FlatTreeDataGridSource<Country>? _source;
    private bool _isAttachedToVisualTree;

    public FindDisplayedRowIndexPage()
    {
        InitializeComponent();
    }

    public void ClearSortClick(object? sender, RoutedEventArgs e)
    {
        (DataContext as CountriesPageViewModel)?.ClearSort();
    }

    public void CountrySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        QueueBringSelectedCountryIntoView();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttachedToVisualTree = true;
        AttachToSource();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttachedToVisualTree = false;
        DetachFromSource();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        DetachFromSource();
        base.OnDataContextChanged(e);

        if (_isAttachedToVisualTree)
        {
            AttachToSource();
        }
    }

    private void AttachToSource()
    {
        DetachFromSource();

        _source = (DataContext as CountriesPageViewModel)?.Source;

        if (_source is not null)
        {
            _source.Rows.CollectionChanged += RowsCollectionChanged;
            _source.Sorted += SourceSorted;
        }
    }

    private void DetachFromSource()
    {
        if (_source is not null)
        {
            _source.Rows.CollectionChanged -= RowsCollectionChanged;
            _source.Sorted -= SourceSorted;
            _source = null;
        }
    }

    private void RowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueBringSelectedCountryIntoView();
    }

    private void SourceSorted()
    {
        QueueBringSelectedCountryIntoView();
    }

    private void QueueBringSelectedCountryIntoView()
    {
        Dispatcher.UIThread.Post(BringSelectedCountryIntoView, DispatcherPriority.Loaded);
    }

    private void BringSelectedCountryIntoView()
    {
        var countryList = this.FindControl<ListBox>("displayedRowCountryList");
        var grid = this.FindControl<TreeDataGrid>("displayedRowGrid");
        var status = this.FindControl<TextBlock>("displayedRowStatus");
        var country = countryList?.SelectedItem as Country;

        if (_source is null || grid is null || status is null)
        {
            return;
        }

        var rowIndex = _source.FindDisplayedRowIndex(country);

        if (country is null)
        {
            status.Text = "Select a country to find its displayed row.";
        }
        else if (rowIndex < 0)
        {
            status.Text = $"{country.Name} is not displayed by the current filter.";
        }
        else
        {
            status.Text = $"{country.Name} is displayed at zero-based row index {rowIndex}.";
            grid.RowsPresenter?.BringIntoView(rowIndex);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
