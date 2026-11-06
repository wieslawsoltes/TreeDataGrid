using System;
using System.Collections.Generic;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridDemo.Models;

namespace TreeDataGridUnoSample;

internal sealed class FindCountryViewModel : NotifyingBase, IDisposable
{
    private Country[] _visible;
    private Country? _selected;
    private string _filter = "";
    private string _status = "Select a country from the complete model list.";
    public FindCountryViewModel()
    {
        AllCountries = Countries.All.ToArray();
        _visible = AllCountries.ToArray();
        Source = new(_visible);
        Source.Columns.Add(new TextColumn<Country, string?>("Country", x => x.Name, width: new(220)));
        Source.Columns.Add(new TextColumn<Country, string>("Region", x => x.Region, width: new(240)));
        Source.Columns.Add(new TextColumn<Country, int>("Population", x => x.Population, width: new(140)));
        Source.Columns.Add(new TextColumn<Country, int>("Area", x => x.Area, width: new(120)));
        Source.Sorted += UpdateLocation;
    }
    public IReadOnlyList<Country> AllCountries { get; }
    public FlatTreeDataGridSource<Country> Source { get; }
    public int DisplayedRow { get; private set; } = -1;
    public string Status { get => _status; private set => RaiseAndSetIfChanged(ref _status, value); }
    public event Action? LocationChanged;
    public Country? SelectedCountry
    {
        get => _selected;
        set { if (RaiseAndSetIfChanged(ref _selected, value)) UpdateLocation(); }
    }
    public string FilterText
    {
        get => _filter;
        set
        {
            if (!RaiseAndSetIfChanged(ref _filter, value)) return;
            _visible = AllCountries.Where(x => string.IsNullOrWhiteSpace(value) ||
                (x.Name?.Contains(value, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                x.Region.Contains(value, StringComparison.CurrentCultureIgnoreCase)).ToArray();
            Source.Items = _visible;
            UpdateLocation();
        }
    }
    private void UpdateLocation()
    {
        var modelIndex = Array.IndexOf(_visible, SelectedCountry);
        DisplayedRow = modelIndex < 0 ? -1 : Source.Rows.ModelIndexToRowIndex(new IndexPath(modelIndex));
        Status = SelectedCountry is null ? "Select a country from the complete model list." :
            DisplayedRow < 0 ? $"{SelectedCountry.Name} is not displayed by the current filter." :
            $"{SelectedCountry.Name} is displayed at zero-based row index {DisplayedRow}.";
        LocationChanged?.Invoke();
    }
    public void Dispose() { Source.Sorted -= UpdateLocation; Source.Dispose(); }
}
