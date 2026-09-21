using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridDemo.Models;
using TreeDataGridDemo.ViewModels;
using Uno.Controls.Presentation;
using Uno.Controls;
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
    private readonly FilesViewModel _files;
    private readonly FindCountryViewModel _find = new();
    private readonly DragDropViewModel _dragDrop = new();
    private bool _ready;
    private int _newPerson;
    private readonly Dictionary<IColumn, GridLength> _originalWidths = new();
    private readonly HashSet<IColumn> _fileColumns = new();
    private readonly HashSet<IColumn> _declarativeColumns = new();
    public MainPage()
    {
        InitializeComponent();
        // Preserve live-region metadata on implemented native heads without
        // invoking the generated Uno stub on unsupported renderers.
        if (Windows.Foundation.Metadata.ApiInformation.IsMethodPresent(
            "Microsoft.UI.Xaml.Automation.AutomationProperties", "SetLiveSetting"))
        {
#pragma warning disable Uno0001 // The implemented capability is checked above.
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(
                DragStatus, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
#pragma warning restore Uno0001
        }
        var queue = DispatcherQueue;
        _files = new(action => queue.TryEnqueue(() => action()));
        _files.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(FilesViewModel.Source)) return;
            if (_files.Source is null)
            {
                foreach (var column in _fileColumns) _originalWidths.Remove(column);
                _fileColumns.Clear();
            }
            if (Scenarios.SelectedIndex is 5 or 6)
            {
                CountriesGrid.Model = _files.Source;
                if (_files.Source is { } source)
                    foreach (var column in source.Columns) { _originalWidths.TryAdd(column, column.Width); _fileColumns.Add(column); }
                ApplySizingMode();
            }
        };
        FolderPath.Text = BrowserFileSample.InitialDirectory();
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
        CountriesGrid.CellTemplates["FileName"] = (DataTemplate)Resources["FileNameTemplate"];
        WikipediaStatus.SetBinding(TextBlock.TextProperty, new Microsoft.UI.Xaml.Data.Binding { Source = _wikipedia, Path = new PropertyPath(nameof(WikipediaViewModel.Status)) });
        FileStatus.SetBinding(TextBlock.TextProperty, new Microsoft.UI.Xaml.Data.Binding { Source = _files, Path = new PropertyPath(nameof(FilesViewModel.Status)) });
        FindCountryList.ItemsSource = _find.AllCountries;
        FindStatus.SetBinding(TextBlock.TextProperty, new Microsoft.UI.Xaml.Data.Binding { Source = _find, Path = new PropertyPath(nameof(FindCountryViewModel.Status)) });
        _find.LocationChanged += BringFoundCountryIntoView;
        Unloaded += (_, _) => { _wikipedia.CancelLoad(); _files.Close(); };
        foreach (var source in new ITreeDataGridSource[] { _source, _peopleSource, _templateSource, _variableSource, _wikipedia.Source, _find.Source, _dragDrop.Source })
            foreach (var column in source.Columns) _originalWidths.Add(column, column.Width);
        _ready = true;
        ShowScenario(0);
    }
    public Uno.Controls.TreeDataGrid Grid => CountriesGrid;
    internal HierarchicalTreeDataGridSource<Person> PeopleSource => _peopleSource;
    internal FlatTreeDataGridSource<TemplateColumnItem> TemplateSource => _templateSource;
    internal ObservableCollection<TemplateColumnItem> TemplateItems => _templateItems;
    internal WikipediaViewModel Wikipedia => _wikipedia;
    internal FilesViewModel Files => _files;
    internal FindCountryViewModel FindCountry => _find;
    internal DragDropViewModel DragDrop => _dragDrop;
    internal void ShowScenario(int index)
    {
        if (!_ready) return;
        if (Scenarios.SelectedIndex != index) { Scenarios.SelectedIndex = index; return; }
        CountriesGrid.CancelEdit();
        CountriesGrid.RowDragStarted -= OnRowDragStarted;
        CountriesGrid.RowDragOver -= OnRowDragOver;
        CountriesGrid.RowDragFailed -= OnRowDragFailed;
        CountriesGrid.DropCompleted -= OnRowDropCompleted;
        CountriesGrid.AutoDragDropRows = index == 8;
        if (index == 8)
        {
            CountriesGrid.RowDragStarted += OnRowDragStarted;
            CountriesGrid.RowDragOver += OnRowDragOver;
            CountriesGrid.RowDragFailed += OnRowDragFailed;
            CountriesGrid.DropCompleted += OnRowDropCompleted;
        }
        if (index != 4) _wikipedia.CancelLoad();
        if (index is not (5 or 6)) _files.Close();
        else _files.FlatMode = index == 6;
        foreach (var column in _declarativeColumns) _originalWidths.Remove(column);
        _declarativeColumns.Clear();
        CountriesGrid.ItemsSource = index == 9 ? _people.People : null;
        CountriesGrid.Model = index switch { 1 => _peopleSource, 2 => _templateSource, 3 => _variableSource, 4 => _wikipedia.Source, 5 or 6 => _files.Source, 7 => _find.Source, 8 => _dragDrop.Source, 9 => null, _ => _source };
        if (index == 9 && CountriesGrid.Presentation is { } declarative)
            foreach (var column in declarative.Model.Columns)
            { _originalWidths[column] = column.Width; _declarativeColumns.Add(column); }
        ApplySizingMode();
        ScenarioDescription.Text = index switch
        {
            1 => "People · shared Avalonia sample models · expand, edit and mutate the hierarchy",
            2 => "Templates · 200 shared-model rows · sort, scroll and replace selected rows",
            3 => "Variable row countries · shared Country data · multi-line names and measured row heights",
            4 => "Wikipedia · shared feed models · async data, images and virtualized wrapping rows",
            5 => _files.WatchChanges ? "Files · shared file-system model · lazy hierarchy and live directory notifications"
                : "Files · shared file-system model · lazy snapshot hierarchy · browser paths are sandbox-only",
            6 => "Files · flat view of the same shared directory entries · folders sort first",
            7 => "Find country · complete model list · map the selected model to its filtered/sorted displayed row",
            8 => "Drag and drop · shared Avalonia model · automatic Core row movement with per-row permissions",
            9 => "Declarative People · native XAML column definitions and bindings · actual Core hierarchy over the shared Person models",
            _ => "Countries · shared Core source · click column headers to sort",
        };
        MutateButton.Visibility = RemoveButton.Visibility = index is 1 or 2 or 9 ? Visibility.Visible : Visibility.Collapsed;
        EditButton.Visibility = index is 1 or 9 ? Visibility.Visible : Visibility.Collapsed;
        WikipediaActions.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
        FileActions.Visibility = index is 5 or 6 ? Visibility.Visible : Visibility.Collapsed;
        FindActions.Visibility = FindCatalog.Visibility = index == 7 ? Visibility.Visible : Visibility.Collapsed;
        DragActions.Visibility = index == 8 ? Visibility.Visible : Visibility.Collapsed;
        MutateButton.Content = index is 1 or 9 ? "Add child" : "Replace selected";
        if (CountriesGrid.IsLoaded) CountriesGrid.Scroll!.ChangeView(0, 0, null, true);
        if (index == 4 && !_wikipedia.Source.Items.Any() && !_wikipedia.IsLoading)
        {
            if (TreeDataGridUnoSamples.SampleRunContext.HasArgument("--smoke") || TreeDataGridUnoSamples.SampleRunContext.HasArgument("--offline")) _wikipedia.ShowOffline();
            else _ = _wikipedia.ReloadAsync();
        }
        if (index is 5 or 6 && _files.Root is null) _ = _files.OpenAsync(FolderPath.Text);
        if (index == 7) BringFoundCountryIntoView();
    }
    private void OnFindCountryChanged(object sender, SelectionChangedEventArgs e) => _find.SelectedCountry = FindCountryList.SelectedItem as Country;
    private void OnClearDragSort(object sender, RoutedEventArgs e)
    {
        _dragDrop.Source.ClearSort();
        DragStatus.Text = "Sort cleared. Rows can be moved.";
    }
    private void OnResetDragData(object sender, RoutedEventArgs e)
    {
        _dragDrop.Reset();
        DragStatus.Text = "Drag/drop data reset.";
    }
    private void OnRowDragStarted(object? sender, TreeDataGridRowDragStartedEventArgs e)
    {
        if (e.Models.OfType<DragDropItem>().Any(model => !model.AllowDrag))
        {
            e.AllowedEffects = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            DragStatus.Text = "A selected row does not allow dragging.";
        }
        else if (_dragDrop.Source.IsSorted)
        {
            e.Cancel = true;
            DragStatus.Text = "Clear sorting before moving rows.";
        }
        else DragStatus.Text = $"Dragging {e.Models.Count} selected row(s).";
    }
    private void OnRowDragOver(object? sender, TreeDataGridRowDragEventArgs e)
    {
        if (e.Position == TreeDataGridRowDropPosition.Inside && e.TargetRow?.Model is DragDropItem { AllowDrop: false })
            e.Inner.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
    }
    private void OnRowDragFailed(object? sender, TreeDataGridRowDragFailedEventArgs e) => DragStatus.Text = e.Error.Message;
    private void OnRowDropCompleted(UIElement sender, DropCompletedEventArgs e) => DragStatus.Text = e.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move
        ? "Rows moved." : "Drag ended without moving rows.";
    private void OnFindFilterChanged(object sender, TextChangedEventArgs e) { if (_ready) _find.FilterText = FindFilter.Text; }
    private void OnClearFindSort(object sender, RoutedEventArgs e) => _find.Source.ClearSort();
    private void BringFoundCountryIntoView()
    {
        if (!_ready || Scenarios.SelectedIndex != 7 || _find.DisplayedRow < 0) return;
        CountriesGrid.SelectCell(_find.DisplayedRow, 0);
        CountriesGrid.BringCellIntoView(_find.DisplayedRow, 0);
    }
    private async void OnOpenFolder(object sender, RoutedEventArgs e) => await _files.OpenAsync(FolderPath.Text);
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
        if (!_ready || CountriesGrid.Presentation?.Model is not { } source) return;
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
        if (Scenarios.SelectedIndex is 1 or 9)
        {
            var source = CountriesGrid.Presentation!.Model;
            var person = new Person { Name = $"New person {++_newPerson}", Title = "Team member", Age = 25, IsActive = true };
            if ((uint)row < (uint)source.Rows.Count)
            {
                var parent = (Person)source.Rows[row].Model!;
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
        if (Scenarios.SelectedIndex is 1 or 9 && CountriesGrid.Presentation?.Model is { } source && (uint)row < (uint)source.Rows.Count)
        {
            var path = source.Rows.RowIndexToModelIndex(row);
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
        if (CountriesGrid.Presentation is not { } presentation || !ReferenceEquals(presentation.Model.Rows, _source.Rows) ||
            presentation.Rows.Count != _source.Rows.Count || (_source.Rows.Count > 0 && !ReferenceEquals(presentation.Rows[0], _source.Rows[0])))
            throw new InvalidOperationException("Uno must expose the actual Core source and row objects through its view facade.");
        if (!CountriesGrid.RowsPresenter!.RealizedCells.Any(cell => cell.ActualWidth > 0 && cell.ActualHeight > 0))
            throw new InvalidOperationException("No cells were rendered.");
        if (CountriesGrid.RowsPresenter!.RealizedCells.Count >= _source.Rows.Count * _source.Columns.Count)
            throw new InvalidOperationException("The sample did not virtualize its rows.");
        Console.WriteLine($"Initial realized cells: {CountriesGrid.RowsPresenter!.RealizedCells.Count}");
    }
    public void VerifyScrolledRender()
    {
        if (!CountriesGrid.RowsPresenter!.RealizedCells.Any(cell => cell.RowIndex > 0))
            throw new InvalidOperationException("Scrolling did not realize later rows.");
        foreach (var cell in CountriesGrid.RowsPresenter!.RealizedCells)
            if (!ReferenceEquals(cell.RowModel, _source.Rows[cell.RowIndex].Model))
                throw new InvalidOperationException("A recycled cell retained an old Core row.");
        Console.WriteLine($"Scrolled realized cells: {CountriesGrid.RowsPresenter!.RealizedCells.Count}");
    }
}
