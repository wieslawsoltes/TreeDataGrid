using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using TreeDataGridDemo.Models;
using Uno.Controls;
using GridLength = Microsoft.UI.Xaml.GridLength;
using GridUnitType = Microsoft.UI.Xaml.GridUnitType;

namespace TreeDataGridUnoSample.Demo.ViewModels
{
    internal class CountriesPageViewModel : NotifyingBase
    {
        private readonly ObservableCollection<Country> _data;
        private bool _cellSelection;
        private string _filterText = string.Empty;

        public CountriesPageViewModel(bool useVariableHeightRows = false)
        {
            _data = new ObservableCollection<Country>(useVariableHeightRows ?
                CreateCountriesWithVariableHeightRows() :
                Countries.All);

            Source = new FlatTreeDataGridSource<Country>(_data)
                .WithRowHeaderColumn()
                .WithTextColumn("Country", x => x.Name, o =>
                {
                    o.Width = new GridLength(6, GridUnitType.Star);
                    o.IsTextSearchEnabled = true;
                })
                .WithTemplateColumnFromResourceKeys("Region", "RegionCell", "RegionEditCell", o =>
                {
                    o.TextSearchBinding = new Microsoft.UI.Xaml.Data.Binding { Path = new PropertyPath(nameof(Country.Region)) };
                })
                .WithTextColumn(x => x.Population, o => o.Width = new GridLength(3, GridUnitType.Star))
                .WithTextColumn(x => x.Area, o => o.Width = new GridLength(3, GridUnitType.Star))
                .WithTextColumn("GDP", x => x.GDP, o =>
                {
                    o.Width = new GridLength(3, GridUnitType.Star);
                    o.TextAlignment = TextAlignment.Right;
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
                }
            }
        }

        public void AddCountry(Country country) => _data.Add(country);

        public void ClearSort() => Source.ClearSort();

        public void RefreshFilter() => Source.RefreshFilter();

        public void RemoveSelected()
        {
            var selection = Source.Selection switch
            {
                ITreeSelectionModel rows => rows.SelectedIndexes.ToList(),
                ITreeDataGridCellSelectionModel<Country> cells => cells.SelectedIndexes.Select(x => x.RowIndex).Distinct().ToList(),
                _ => new List<IndexPath>(),
            };

            foreach (var index in selection.OrderByDescending(x => x[0]))
            {
                _data.RemoveAt(index[0]);
            }
        }

        private static IEnumerable<Country> CreateCountriesWithVariableHeightRows()
        {
            var random = new Random(42);

            foreach (var country in Countries.All)
            {
                var name = country.Name ?? string.Empty;
                var lineCount = random.Next(1, 6);

                yield return new Country(
                    string.Join(Environment.NewLine, Enumerable.Repeat(name, lineCount)),
                    country.Region,
                    country.Population,
                    country.Area,
                    country.PopulationDensity,
                    country.CoastLine,
                    country.NetMigration,
                    country.InfantMortality,
                    country.GDP,
                    country.LiteracyPercent,
                    country.Phones,
                    country.BirthRate,
                    country.DeathRate);
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
                (x.Name?.Contains(_filterText, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                x.Region.Contains(_filterText, StringComparison.CurrentCultureIgnoreCase));
        }
    }
}
