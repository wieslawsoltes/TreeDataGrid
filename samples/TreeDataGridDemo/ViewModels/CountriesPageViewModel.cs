using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Data;
using ReactiveUI;
using TreeDataGridDemo.Models;

namespace TreeDataGridDemo.ViewModels
{
    internal class CountriesPageViewModel : ReactiveObject
    {
        private readonly ObservableCollection<Country> _data;
        private bool _cellSelection;
        private string _filterText = string.Empty;

        public CountriesPageViewModel()
        {
            _data = new ObservableCollection<Country>(Countries.All);

            Source = new FlatTreeDataGridSource<Country>(_data)
                .WithRowHeaderColumn()
                .WithTextColumn("Country", x => x.Name, o =>
                {
                    o.Width = new GridLength(6, GridUnitType.Star);
                    o.IsTextSearchEnabled = true;
                })
                .WithTemplateColumnFromResourceKeys("Region", "RegionCell", "RegionEditCell", o =>
                {
                    o.TextSearchBinding = CompiledBinding.Create((Country x) => x.Region);
                })
                .WithTextColumn(x => x.Population, o => o.Width = new GridLength(3, GridUnitType.Star))
                .WithTextColumn(x => x.Area, o => o.Width = new GridLength(3, GridUnitType.Star))
                .WithTextColumn("GDP", x => x.GDP, o =>
                {
                    o.Width = new GridLength(3, GridUnitType.Star);
                    o.TextAlignment = Avalonia.Media.TextAlignment.Right;
                    o.MaxWidth = new GridLength(150);
                });
            Source.RowSelection!.SingleSelect = false;
        }

        public bool CellSelection
        {
            get => _cellSelection;
            set
            {
                if (_cellSelection != value)
                {
                    _cellSelection = value;
                    if (_cellSelection)
                        Source.Selection = new TreeDataGridCellSelectionModel<Country>(Source) { SingleSelect = false };
                    else
                        Source.Selection = new TreeDataGridRowSelectionModel<Country>(Source) { SingleSelect = false };
                    this.RaisePropertyChanged();
                }
            }
        }

        public FlatTreeDataGridSource<Country> Source { get; }

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText != value)
                {
                    _filterText = value;
                    ApplyFilter();
                    this.RaisePropertyChanged();
                }
            }
        }

        public void AddCountry(Country country) => _data.Add(country);

        public void ClearSort() => Source.ClearSort();

        public void RefreshFilter() => Source.RefreshFilter();

        public void RemoveSelected()
        {
            var selection = ((ITreeSelectionModel)Source.Selection!).SelectedIndexes.ToList();

            for (var i = selection.Count - 1; i >= 0; --i)
            {
                _data.RemoveAt(selection[i][0]);
            }
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(_filterText))
            {
                Source.Filter(null);
                return;
            }

            Source.Filter(x =>
                (x.Name?.Contains(_filterText, System.StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                x.Region.Contains(_filterText, System.StringComparison.CurrentCultureIgnoreCase));
        }
    }
}
