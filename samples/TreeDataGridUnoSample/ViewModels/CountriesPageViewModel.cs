using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Uno.Controls;
using Uno.Controls.Models;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Selection;
using TreeDataGridDemo.Models;

namespace TreeDataGridDemo.ViewModels;

internal class CountriesPageViewModel : NotifyingBase
{
    private readonly ObservableCollection<Country> _data;
    private bool _cellSelection;

    public CountriesPageViewModel()
    {
        _data = new ObservableCollection<Country>(Countries.All);

        Source = new FlatTreeDataGridSource<Country>(_data)
        {
            Columns =
            {
                new TextColumn<Country, string>("Country", x => x.Name, (row, value) => row.Name = value ?? string.Empty, new GridLength(6, GridUnitType.Star), new TextColumnOptions<Country>
                {
                    IsTextSearchEnabled = true,
                }),
                new TemplateColumn<Country>("Region", "RegionCell", "RegionEditCell"),
                new TextColumn<Country, int>("Population", x => x.Population, new GridLength(3, GridUnitType.Star)),
                new TextColumn<Country, int>("Area", x => x.Area, new GridLength(3, GridUnitType.Star)),
                new TextColumn<Country, int>("GDP", x => x.GDP, new GridLength(3, GridUnitType.Star), new TextColumnOptions<Country>
                {
                    TextAlignment = Avalonia.Media.TextAlignment.Right,
                    MaxWidth = new GridLength(150),
                }),
            }
        };

        Source.RowSelection!.SingleSelect = false;
    }

    public bool CellSelection
    {
        get => _cellSelection;
        set
        {
            if (!RaiseAndSetIfChanged(ref _cellSelection, value))
                return;

            Source.Selection = _cellSelection
                ? new TreeDataGridCellSelectionModel<Country>(Source) { SingleSelect = false }
                : new TreeDataGridRowSelectionModel<Country>(Source) { SingleSelect = false };
        }
    }

    public FlatTreeDataGridSource<Country> Source { get; }

    public void AddCountry(Country country) => _data.Add(country);

    public void RemoveSelected()
    {
        var selectedIndexes = ((ITreeSelectionModel)Source.Selection!).SelectedIndexes.ToList();
        for (var i = selectedIndexes.Count - 1; i >= 0; --i)
            _data.RemoveAt(selectedIndexes[i][0]);
    }
}
