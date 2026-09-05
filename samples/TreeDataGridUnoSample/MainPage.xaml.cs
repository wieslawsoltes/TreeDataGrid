using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridDemo.Models;
using TreeDataGridDemo.ViewModels;
using Uno.Controls.Presentation;
using GridLength = TreeDataGridCore.GridLength;

namespace TreeDataGridUnoSample;

public sealed partial class MainPage : Page
{
    private readonly FlatTreeDataGridSource<Country> _source;
    private readonly FlatTreeDataGridSource<Country> _variableSource;
    private readonly PeopleXamlPageViewModel _people = new();
    private readonly HierarchicalTreeDataGridSource<Person> _peopleSource;
    private readonly ObservableCollection<TemplateColumnItem> _templateItems = new();
    private readonly FlatTreeDataGridSource<TemplateColumnItem> _templateSource;
    private readonly WikipediaViewModel _wikipedia = new();
    private bool _ready;
    private int _newPerson;
    private readonly Dictionary<IColumn, GridLength> _originalWidths = new();
    public MainPage()
    {
        InitializeComponent();
        _source = CreateCountrySource(Countries.All);
        _variableSource = CreateCountrySource(CreateVariableCountries());
        _peopleSource = new(_people.People);
        _peopleSource.Columns.Add(new HierarchicalExpanderColumn<Person>(
            new TextColumn<Person, string?>("Name", x => x.Name, (x, value) => x.Name = value, width: new(250)),
            x => x.Children, x => x.Children.Count > 0, x => x.IsExpanded));
        _peopleSource.Columns.Add(new TextColumn<Person, string?>("Title", x => x.Title, (x, value) => x.Title = value, width: new(240)));
        _peopleSource.Columns.Add(new TextColumn<Person, int>("Age", x => x.Age, (x, value) =>
        {
            if (value is < 0 or > 150) throw new ArgumentOutOfRangeException(nameof(value), "Age must be between 0 and 150.");
            x.Age = value;
        }, width: new(100)));
        _peopleSource.Columns.Add(new CheckBoxColumn<Person>("Active", x => x.IsActive, (x, value) => x.IsActive = value, width: new(100)));
        foreach (var index in Enumerable.Range(1, 200)) _templateItems.Add(CreateTemplateItem(index));
        _templateSource = new(_templateItems);
        _templateSource.Columns.Add(new TemplateColumn<TemplateColumnItem>("Flag", "Flag", width: new(70), options: new()
        {
            CompareAscending = (x, y) => Nullable.Compare(x?.IsFlagged, y?.IsFlagged),
            CompareDescending = (x, y) => Nullable.Compare(y?.IsFlagged, x?.IsFlagged),
        }));
        _templateSource.Columns.Add(new TextColumn<TemplateColumnItem, string>("Name", x => x.Name, width: new(200)));
        _templateSource.Columns.Add(new TextColumn<TemplateColumnItem, string>("Type", x => x.Type, width: new(150)));
        _templateSource.Columns.Add(new TemplateColumn<TemplateColumnItem>("Details", "Details", width: new(400), options: new()
        {
            CompareAscending = (x, y) => string.CompareOrdinal(x?.Details, y?.Details),
            CompareDescending = (x, y) => string.CompareOrdinal(y?.Details, x?.Details),
        }));
        CountriesGrid.CellTemplates["Flag"] = (DataTemplate)Resources["FlagTemplate"];
        CountriesGrid.CellTemplates["Details"] = (DataTemplate)Resources["DetailsTemplate"];
        CountriesGrid.CellTemplates["WikipediaImage"] = (DataTemplate)Resources["WikipediaImageTemplate"];
        CountriesGrid.CellTemplates["WikipediaTitle"] = (DataTemplate)Resources["WikipediaTitleTemplate"];
        CountriesGrid.CellTemplates["WikipediaExtract"] = (DataTemplate)Resources["WikipediaExtractTemplate"];
        WikipediaStatus.SetBinding(TextBlock.TextProperty, new Microsoft.UI.Xaml.Data.Binding { Source = _wikipedia, Path = new PropertyPath(nameof(WikipediaViewModel.Status)) });
        Unloaded += (_, _) => _wikipedia.CancelLoad();
        foreach (var source in new ITreeDataGridSource[] { _source, _peopleSource, _templateSource, _variableSource, _wikipedia.Source })
            foreach (var column in source.Columns) _originalWidths.Add(column, column.Width);
        _ready = true;
        ShowScenario(0);
    }
    public Uno.Controls.TreeDataGrid Grid => CountriesGrid;
    internal HierarchicalTreeDataGridSource<Person> PeopleSource => _peopleSource;
    internal FlatTreeDataGridSource<TemplateColumnItem> TemplateSource => _templateSource;
    internal ObservableCollection<TemplateColumnItem> TemplateItems => _templateItems;
    internal WikipediaViewModel Wikipedia => _wikipedia;
    internal void ShowScenario(int index)
    {
        if (!_ready) return;
        if (Scenarios.SelectedIndex != index) { Scenarios.SelectedIndex = index; return; }
        CountriesGrid.CancelEdit();
        if (index != 4) _wikipedia.CancelLoad();
        CountriesGrid.Model = index switch { 1 => _peopleSource, 2 => _templateSource, 3 => _variableSource, 4 => _wikipedia.Source, _ => _source };
        ApplySizingMode();
        ScenarioDescription.Text = index switch
        {
            1 => "People · shared Avalonia sample models · expand, edit and mutate the hierarchy",
            2 => "Templates · 200 shared-model rows · sort, scroll and replace selected rows",
            3 => "Variable row countries · shared Country data · multi-line names and measured row heights",
            4 => "Wikipedia · shared feed models · async data, images and virtualized wrapping rows",
            _ => "Countries · shared Core source · click column headers to sort",
        };
        MutateButton.Visibility = RemoveButton.Visibility = index is 1 or 2 ? Visibility.Visible : Visibility.Collapsed;
        EditButton.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        WikipediaActions.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
        MutateButton.Content = index == 1 ? "Add child" : "Replace selected";
        if (CountriesGrid.IsLoaded) CountriesGrid.Scroll.ChangeView(0, 0, null, true);
        if (index == 4 && !_wikipedia.Source.Items.Any() && !_wikipedia.IsLoading)
        {
            if (Environment.GetCommandLineArgs().Any(x => x is "--smoke" or "--offline")) _wikipedia.ShowOffline();
            else _ = _wikipedia.ReloadAsync();
        }
    }
    private async void OnReloadWikipedia(object sender, RoutedEventArgs e) => await _wikipedia.ReloadAsync();
    private void OnOfflineWikipedia(object sender, RoutedEventArgs e) => _wikipedia.ShowOffline();
    private void OnCancelWikipedia(object sender, RoutedEventArgs e) => _wikipedia.CancelLoad();
    private static TemplateColumnItem CreateTemplateItem(int index) => new()
    {
        Name = $"Item {index:000}", Type = $"Type {(char)('A' + index % 4)}",
        Details = $"Details for item {index:000}", IsFlagged = index % 3 == 0,
    };
    private static FlatTreeDataGridSource<Country> CreateCountrySource(IEnumerable<Country> countries)
    {
        var source = new FlatTreeDataGridSource<Country>(countries);
        source.Columns.Add(new TextColumn<Country, string?>("Country", x => x.Name, width: new(210)));
        source.Columns.Add(new TextColumn<Country, string>("Region", x => x.Region, width: new(190)));
        source.Columns.Add(new TextColumn<Country, int>("Population", x => x.Population, width: new(150)));
        source.Columns.Add(new TextColumn<Country, int>("Area", x => x.Area, width: new(150)));
        source.Columns.Add(new TextColumn<Country, double>("Density", x => x.PopulationDensity, width: new(150)));
        source.Columns.Add(new TextColumn<Country, int>("GDP", x => x.GDP, width: new(150)));
        return source;
    }
    private static Country[] CreateVariableCountries()
    {
        var random = new Random(42);
        return Countries.All.Select(country => new Country(
            string.Join(Environment.NewLine, Enumerable.Repeat(country.Name, random.Next(1, 5))),
            country.Region, country.Population, country.Area, country.PopulationDensity,
            country.CoastLine, country.NetMigration, country.InfantMortality, country.GDP,
            country.LiteracyPercent, country.Phones, country.BirthRate, country.DeathRate)).ToArray();
    }
    private void OnScenarioChanged(object sender, SelectionChangedEventArgs e) => ShowScenario(Scenarios.SelectedIndex);
    private void OnSizingModeChanged(object sender, SelectionChangedEventArgs e) => ApplySizingMode();
    private void OnRowHeightModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CountriesGrid is not null) CountriesGrid.RowHeight = RowHeightModes.SelectedIndex switch { 1 => 28, 2 => 48, _ => double.NaN };
    }
    private void ApplySizingMode()
    {
        if (!_ready || CountriesGrid.Model is not { } source) return;
        for (var i = 0; i < source.Columns.Count; ++i)
        {
            var column = source.Columns[i];
            column.Width = SizingModes.SelectedIndex switch
            {
                1 => GridLength.Auto,
                2 => i == 0 ? GridLength.Auto : GridLength.Star,
                _ => _originalWidths[column],
            };
        }
    }
    private void OnEdit(object sender, RoutedEventArgs e) => CountriesGrid.BeginEdit();
    private void OnMutate(object sender, RoutedEventArgs e)
    {
        if (!CountriesGrid.CommitEdit()) return;
        var row = CountriesGrid.Presentation?.Selection.GetAnchor(true).Row ?? -1;
        if (Scenarios.SelectedIndex == 1)
        {
            var person = new Person { Name = $"New person {++_newPerson}", Title = "Team member", Age = 25, IsActive = true };
            if ((uint)row < (uint)_peopleSource.Rows.Count)
            {
                var parent = (Person)_peopleSource.Rows[row].Model!;
                parent.Children.Add(person);
                parent.IsExpanded = true;
            }
            else _people.People.Add(person);
        }
        else if (Scenarios.SelectedIndex == 2 && (uint)row < (uint)_templateSource.Rows.Count)
        {
            var modelIndex = _templateSource.Rows.RowIndexToModelIndex(row)[0];
            var previous = _templateItems[modelIndex];
            _templateItems[modelIndex] = new()
            {
                Name = previous.Name, Type = previous.Type,
                Details = previous.Details + " · replaced", IsFlagged = !previous.IsFlagged,
            };
        }
    }
    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (!CountriesGrid.CommitEdit()) return;
        var row = CountriesGrid.Presentation?.Selection.GetAnchor(true).Row ?? -1;
        if (Scenarios.SelectedIndex == 1 && (uint)row < (uint)_peopleSource.Rows.Count)
        {
            var path = _peopleSource.Rows.RowIndexToModelIndex(row);
            var siblings = _people.People;
            for (var i = 0; i < path.Count - 1; ++i) siblings = siblings[path[i]].Children;
            siblings.RemoveAt(path[path.Count - 1]);
        }
        else if (Scenarios.SelectedIndex == 2 && (uint)row < (uint)_templateSource.Rows.Count)
            _templateItems.RemoveAt(_templateSource.Rows.RowIndexToModelIndex(row)[0]);
    }
    private void OnSelectionModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CountriesGrid is null) return;
        CountriesGrid.SelectionMode = SelectionModes.SelectedIndex switch
        {
            1 => TreeDataGridSelectionMode.MultipleRows,
            2 => TreeDataGridSelectionMode.MultipleCells,
            3 => TreeDataGridSelectionMode.None,
            _ => TreeDataGridSelectionMode.Source,
        };
    }
    public void VerifyInitialRender()
    {
        if (!ReferenceEquals(CountriesGrid.Presentation?.Rows, _source.Rows))
            throw new InvalidOperationException("Uno must expose the actual Core rows.");
        if (!CountriesGrid.RowsPresenter.RealizedCells.Any(cell => cell.ActualWidth > 0 && cell.ActualHeight > 0))
            throw new InvalidOperationException("No cells were rendered.");
        if (CountriesGrid.RowsPresenter.RealizedCells.Count >= _source.Rows.Count * _source.Columns.Count)
            throw new InvalidOperationException("The sample did not virtualize its rows.");
        Console.WriteLine($"Initial realized cells: {CountriesGrid.RowsPresenter.RealizedCells.Count}");
    }
    public void VerifyScrolledRender()
    {
        if (!CountriesGrid.RowsPresenter.RealizedCells.Any(cell => cell.RowIndex > 0))
            throw new InvalidOperationException("Scrolling did not realize later rows.");
        foreach (var cell in CountriesGrid.RowsPresenter.RealizedCells)
            if (!ReferenceEquals(cell.RowModel, _source.Rows[cell.RowIndex].Model))
                throw new InvalidOperationException("A recycled cell retained an old Core row.");
        Console.WriteLine($"Scrolled realized cells: {CountriesGrid.RowsPresenter.RealizedCells.Count}");
    }
}
