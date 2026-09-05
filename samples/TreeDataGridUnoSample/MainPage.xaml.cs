using System;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridDemo.Models;
using TreeDataGridDemo.ViewModels;
using Uno.Controls.Presentation;

namespace TreeDataGridUnoSample;

public sealed partial class MainPage : Page
{
    private readonly FlatTreeDataGridSource<Country> _source;
    private readonly PeopleXamlPageViewModel _people = new();
    private readonly HierarchicalTreeDataGridSource<Person> _peopleSource;
    private readonly ObservableCollection<TemplateColumnItem> _templateItems = new();
    private readonly FlatTreeDataGridSource<TemplateColumnItem> _templateSource;
    private bool _ready;
    private int _newPerson;
    public MainPage()
    {
        InitializeComponent();
        _source = new(Countries.All);
        _source.Columns.Add(new TextColumn<Country, string?>("Country", x => x.Name, width: new(210)));
        _source.Columns.Add(new TextColumn<Country, string>("Region", x => x.Region, width: new(190)));
        _source.Columns.Add(new TextColumn<Country, int>("Population", x => x.Population, width: new(150)));
        _source.Columns.Add(new TextColumn<Country, int>("Area", x => x.Area, width: new(150)));
        _source.Columns.Add(new TextColumn<Country, double>("Density", x => x.PopulationDensity, width: new(150)));
        _source.Columns.Add(new TextColumn<Country, int>("GDP", x => x.GDP, width: new(150)));
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
        _ready = true;
        ShowScenario(0);
    }
    public Uno.Controls.TreeDataGrid Grid => CountriesGrid;
    internal HierarchicalTreeDataGridSource<Person> PeopleSource => _peopleSource;
    internal FlatTreeDataGridSource<TemplateColumnItem> TemplateSource => _templateSource;
    internal ObservableCollection<TemplateColumnItem> TemplateItems => _templateItems;
    internal void ShowScenario(int index)
    {
        if (!_ready) return;
        if (Scenarios.SelectedIndex != index) { Scenarios.SelectedIndex = index; return; }
        CountriesGrid.CancelEdit();
        CountriesGrid.Model = index switch { 1 => _peopleSource, 2 => _templateSource, _ => _source };
        ScenarioDescription.Text = index switch
        {
            1 => "People · shared Avalonia sample models · expand, edit and mutate the hierarchy",
            2 => "Templates · 200 shared-model rows · sort, scroll and replace selected rows",
            _ => "Countries · shared Core source · click column headers to sort",
        };
        MutateButton.Visibility = RemoveButton.Visibility = index == 0 ? Visibility.Collapsed : Visibility.Visible;
        EditButton.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        MutateButton.Content = index == 1 ? "Add child" : "Replace selected";
        if (CountriesGrid.IsLoaded) CountriesGrid.Scroll.ChangeView(0, 0, null, true);
    }
    private static TemplateColumnItem CreateTemplateItem(int index) => new()
    {
        Name = $"Item {index:000}", Type = $"Type {(char)('A' + index % 4)}",
        Details = $"Details for item {index:000}", IsFlagged = index % 3 == 0,
    };
    private void OnScenarioChanged(object sender, SelectionChangedEventArgs e) => ShowScenario(Scenarios.SelectedIndex);
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
