using System.Collections.Specialized;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridDemo.Models;
using TreeDataGridUnoSample.Demo.ViewModels;

namespace TreeDataGridUnoSample.Demo.Views
{
    public sealed partial class FindDisplayedRowIndexPage : UserControl
    {
        private FlatTreeDataGridSource<Country>? _source;
        private bool _isAttachedToVisualTree;

        public FindDisplayedRowIndexPage()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                _isAttachedToVisualTree = true;
                AttachToSource();
            };
            Unloaded += (_, _) =>
            {
                _isAttachedToVisualTree = false;
                DetachFromSource();
            };
            DataContextChanged += (_, _) =>
            {
                DetachFromSource();
                if (_isAttachedToVisualTree)
                    AttachToSource();
            };
        }

        public void ClearSortClick(object sender, RoutedEventArgs e)
        {
            (DataContext as CountriesPageViewModel)?.ClearSort();
        }

        public void CountrySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            QueueBringSelectedCountryIntoView();
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
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, BringSelectedCountryIntoView);
        }

        private void BringSelectedCountryIntoView()
        {
            var country = displayedRowCountryList.SelectedItem as Country;

            if (_source is null)
            {
                return;
            }

            var rowIndex = _source.FindDisplayedRowIndex(country);

            if (country is null)
            {
                displayedRowStatus.Text = "Select a country to find its displayed row.";
            }
            else if (rowIndex < 0)
            {
                displayedRowStatus.Text = $"{country.Name} is not displayed by the current filter.";
            }
            else
            {
                displayedRowStatus.Text = $"{country.Name} is displayed at zero-based row index {rowIndex}.";
                displayedRowGrid.RowsPresenter?.BringIntoView(rowIndex);
            }
        }
    }
}
