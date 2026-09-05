using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Presentation;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using ReactiveUI;
using TreeDataGridDemo.Models;
using Core = TreeDataGridCore;
using CoreModels = TreeDataGridCore.Models;
using UiModels = Avalonia.Controls.Models.TreeDataGrid;

namespace TreeDataGridCoreDemo;

internal sealed class CoreDemoViewModel : ReactiveObject
{
    private readonly ObservableCollection<Country> _countries;
    private readonly ObservableCollection<Country> _visibleCountries;
    private readonly ObservableCollection<OnThisDayArticle> _wikipediaItems = new();
    private Core.ITreeDataGridSource<FileTreeNodeModel> _filesSource;
    private string _countryStatus = "";
    private string _filesStatus = "";
    private string _findStatus = "No country selected.";
    private string _wikipediaStatus = "Loading Wikipedia's On This Day feed…";
    private bool _filesFlat;
    private readonly Core.HierarchicalTreeDataGridSource<FileTreeNodeModel> _fileTreeSource;
    private Core.FlatTreeDataGridSource<FileTreeNodeModel>? _fileFlatSource;

    public CoreDemoViewModel(bool loadRemoteContent = true)
    {
        _countries = new ObservableCollection<Country>(Countries.All);
        _visibleCountries = new ObservableCollection<Country>(_countries);
        CountriesSource = CreateCountrySource(_visibleCountries);
        FindSource = CreateCountrySource(_countries);
        CountryItems = _countries;

        VariableCountryItems = new ObservableCollection<Country>(CreateVariableCountries());
        VariableCountriesSource = CreateCountrySource(VariableCountryItems);

        var rootPath = Directory.GetCurrentDirectory();
        var fileRoot = new FileTreeNodeModel(rootPath, isDirectory: true, isRoot: true);
        _fileTreeSource = CreateFileTreeSource(fileRoot);
        _filesSource = _fileTreeSource;
        FilesOptions = CreateFilesOptions();
        FilesStatus = rootPath;

        WikipediaSource = CreateWikipediaSource(_wikipediaItems);
        WikipediaOptions = CreateWikipediaOptions();
        if (loadRemoteContent)
            LoadingTask = LoadWikipediaAsync();
        else
        {
            AddWikipediaFixture();
            WikipediaStatus = "Deterministic offline data for visual validation.";
            LoadingTask = Task.CompletedTask;
        }

        DragDropSource = CreateDragDropSource();
        PeopleSource = CreatePeopleSource();
        TemplateSource = CreateTemplateSource();
        TemplateOptions = CreateTemplateOptions();
        CountryStatus = $"{_visibleCountries.Count} rows through TreeDataGridCore";
    }

    public ObservableCollection<Country> CountryItems { get; }
    public ObservableCollection<Country> VariableCountryItems { get; }
    public Core.FlatTreeDataGridSource<Country> CountriesSource { get; }
    public Core.FlatTreeDataGridSource<Country> FindSource { get; }
    public Core.FlatTreeDataGridSource<Country> VariableCountriesSource { get; }
    public Core.ITreeDataGridSource<FileTreeNodeModel> FilesSource
    {
        get => _filesSource;
        private set => this.RaiseAndSetIfChanged(ref _filesSource, value);
    }
    public TreeDataGridPresentationOptions<FileTreeNodeModel> FilesOptions { get; }
    public Core.FlatTreeDataGridSource<OnThisDayArticle> WikipediaSource { get; }
    public TreeDataGridPresentationOptions<OnThisDayArticle> WikipediaOptions { get; }
    public Core.HierarchicalTreeDataGridSource<DragDropItem> DragDropSource { get; }
    public Core.HierarchicalTreeDataGridSource<DemoPerson> PeopleSource { get; }
    public Core.FlatTreeDataGridSource<DemoTemplateItem> TemplateSource { get; }
    public TreeDataGridPresentationOptions<DemoTemplateItem> TemplateOptions { get; }
    public Task LoadingTask { get; }

    public bool FilesFlat
    {
        get => _filesFlat;
        set
        {
            if (_filesFlat == value)
                return;
            this.RaiseAndSetIfChanged(ref _filesFlat, value);
            FilesSource = value
                ? _fileFlatSource ??= CreateFileFlatSource(_fileTreeSource.Items.First().Children)
                : _fileTreeSource;
        }
    }

    public string CountryStatus
    {
        get => _countryStatus;
        private set => this.RaiseAndSetIfChanged(ref _countryStatus, value);
    }

    public string FilesStatus
    {
        get => _filesStatus;
        private set => this.RaiseAndSetIfChanged(ref _filesStatus, value);
    }

    public string FindStatus
    {
        get => _findStatus;
        set => this.RaiseAndSetIfChanged(ref _findStatus, value);
    }

    public string WikipediaStatus
    {
        get => _wikipediaStatus;
        private set => this.RaiseAndSetIfChanged(ref _wikipediaStatus, value);
    }

    public void ApplyCountryFilter(string? text)
    {
        var selected = CountriesSource.RowSelection?.SelectedItem;
        _visibleCountries.Clear();
        foreach (var country in _countries.Where(x =>
            string.IsNullOrWhiteSpace(text) ||
            (x.Name?.Contains(text, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
            x.Region.Contains(text, StringComparison.CurrentCultureIgnoreCase)))
        {
            _visibleCountries.Add(country);
        }

        if (selected is not null)
        {
            var index = _visibleCountries.IndexOf(selected);
            if (index >= 0)
                CountriesSource.RowSelection!.SelectedIndex = new Core.IndexPath(index);
        }

        CountryStatus = $"{_visibleCountries.Count} of {_countries.Count} rows through TreeDataGridCore";
    }

    public void AddCountry()
    {
        var country = new Country("Sealand", "WESTERN EUROPE", 2, 1, 2, 100, null, null, 600000, null, null, null, null);
        _countries.Add(country);
        _visibleCountries.Add(country);
        CountryStatus = $"{_visibleCountries.Count} rows; Sealand added through the model collection";
    }

    public void RemoveSelectedCountry()
    {
        var selected = CountriesSource.RowSelection?.SelectedItem;
        if (selected is null)
            return;
        _visibleCountries.Remove(selected);
        _countries.Remove(selected);
        CountryStatus = $"{_visibleCountries.Count} rows; selected model removed";
    }

    private static Core.FlatTreeDataGridSource<Country> CreateCountrySource(IEnumerable<Country> items)
    {
        var source = new Core.FlatTreeDataGridSource<Country>(items);
        source.Columns.Add(new CoreModels.TextColumn<Country, string?>("Country", x => x.Name,
            (x, value) => x.Name = value, new Core.GridLength(3, Core.GridUnitType.Star)));
        source.Columns.Add(new CoreModels.TextColumn<Country, string>("Region", x => x.Region,
            new Core.GridLength(2, Core.GridUnitType.Star)));
        source.Columns.Add(new CoreModels.TextColumn<Country, int>("Population", x => x.Population,
            new Core.GridLength(1, Core.GridUnitType.Star)));
        source.Columns.Add(new CoreModels.TextColumn<Country, int>("Area", x => x.Area,
            new Core.GridLength(1, Core.GridUnitType.Star)));
        source.Columns.Add(new CoreModels.TextColumn<Country, int>("GDP", x => x.GDP,
            new Core.GridLength(1, Core.GridUnitType.Star)));
        source.RowSelection!.SingleSelect = false;
        return source;
    }

    private static IEnumerable<Country> CreateVariableCountries()
    {
        var random = new Random(42);
        foreach (var country in Countries.All)
        {
            var lines = random.Next(1, 5);
            yield return new Country(
                string.Join(Environment.NewLine, Enumerable.Repeat(country.Name, lines)),
                country.Region, country.Population, country.Area, country.PopulationDensity,
                country.CoastLine, country.NetMigration, country.InfantMortality, country.GDP,
                country.LiteracyPercent, country.Phones, country.BirthRate, country.DeathRate);
        }
    }

    private static Core.HierarchicalTreeDataGridSource<FileTreeNodeModel> CreateFileTreeSource(FileTreeNodeModel root)
    {
        var source = new Core.HierarchicalTreeDataGridSource<FileTreeNodeModel>(new[] { root });
        source.Columns.Add(new CoreModels.CheckBoxColumn<FileTreeNodeModel>("✓", x => x.IsChecked,
            (x, value) => x.IsChecked = value, new Core.GridLength(44)));
        var name = new CoreModels.TemplateColumn<FileTreeNodeModel>("Name", "file-name",
            new Core.GridLength(3, Core.GridUnitType.Star), FileOptions(x => x.Name));
        source.Columns.Add(new CoreModels.HierarchicalExpanderColumn<FileTreeNodeModel>(
            name, x => x.Children, x => x.HasChildren, x => x.IsExpanded,
            (x, value) => x.IsExpanded = value));
        source.Columns.Add(new CoreModels.TextColumn<FileTreeNodeModel, long?>("Size", x => x.Size,
            new Core.GridLength(1, Core.GridUnitType.Star), FileOptions(x => x.Size)));
        source.Columns.Add(new CoreModels.TextColumn<FileTreeNodeModel, DateTimeOffset?>("Modified", x => x.Modified,
            new Core.GridLength(2, Core.GridUnitType.Star), FileOptions(x => x.Modified)));
        return source;
    }

    private static Core.FlatTreeDataGridSource<FileTreeNodeModel> CreateFileFlatSource(IEnumerable<FileTreeNodeModel> items)
    {
        var source = new Core.FlatTreeDataGridSource<FileTreeNodeModel>(items);
        source.Columns.Add(new CoreModels.CheckBoxColumn<FileTreeNodeModel>("✓", x => x.IsChecked,
            (x, value) => x.IsChecked = value, new Core.GridLength(44)));
        source.Columns.Add(new CoreModels.TemplateColumn<FileTreeNodeModel>("Name", "file-name",
            new Core.GridLength(3, Core.GridUnitType.Star), FileOptions(x => x.Name)));
        source.Columns.Add(new CoreModels.TextColumn<FileTreeNodeModel, long?>("Size", x => x.Size,
            new Core.GridLength(1, Core.GridUnitType.Star), FileOptions(x => x.Size)));
        source.Columns.Add(new CoreModels.TextColumn<FileTreeNodeModel, DateTimeOffset?>("Modified", x => x.Modified,
            new Core.GridLength(2, Core.GridUnitType.Star), FileOptions(x => x.Modified)));
        return source;
    }

    private static CoreModels.ColumnOptions<FileTreeNodeModel> FileOptions<T>(Func<FileTreeNodeModel, T> selector) =>
        new()
        {
            CompareAscending = FileTreeNodeModel.SortAscending(selector),
            CompareDescending = FileTreeNodeModel.SortDescending(selector),
        };

    private static TreeDataGridPresentationOptions<FileTreeNodeModel> CreateFilesOptions()
    {
        var options = new TreeDataGridPresentationOptions<FileTreeNodeModel>();
        options.Columns.Add("file-name", column => new UiModels.TemplateColumn<FileTreeNodeModel>(
            column.Header,
            new FuncDataTemplate<FileTreeNodeModel>((model, _) =>
            {
                var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
                panel.Children.Add(new TextBlock { Text = model?.IsDirectory == true ? "📁" : "📄" });
                var name = new TextBlock();
                name.Bind(TextBlock.TextProperty, new Binding(nameof(FileTreeNodeModel.Name)));
                panel.Children.Add(name);
                return panel;
            })));
        return options;
    }

    private static Core.FlatTreeDataGridSource<OnThisDayArticle> CreateWikipediaSource(
        ObservableCollection<OnThisDayArticle> items)
    {
        var source = new Core.FlatTreeDataGridSource<OnThisDayArticle>(items);
        source.Columns.Add(new CoreModels.TemplateColumn<OnThisDayArticle>("Image", "wikipedia-image",
            new Core.GridLength(110)));
        source.Columns.Add(new CoreModels.TextColumn<OnThisDayArticle, string?>("Title",
            x => x.Titles!.Normalized, new Core.GridLength(2, Core.GridUnitType.Star)));
        source.Columns.Add(new CoreModels.TextColumn<OnThisDayArticle, string?>("Extract",
            x => x.Extract, new Core.GridLength(4, Core.GridUnitType.Star)));
        return source;
    }

    private static TreeDataGridPresentationOptions<OnThisDayArticle> CreateWikipediaOptions()
    {
        var options = new TreeDataGridPresentationOptions<OnThisDayArticle>();
        options.Columns.Add("wikipedia-image", column => new UiModels.TemplateColumn<OnThisDayArticle>(
            column.Header,
            new FuncDataTemplate<OnThisDayArticle>((_, _) =>
            {
                var image = new Image { Width = 96, Height = 64, Stretch = Avalonia.Media.Stretch.UniformToFill };
                image.Bind(Image.SourceProperty, new Binding(nameof(OnThisDayArticle.Image)));
                return image;
            })));
        return options;
    }

    private async Task LoadWikipediaAsync()
    {
        try
        {
            var now = DateTimeOffset.Now;
            var uri = $"https://api.wikimedia.org/feed/v1/wikipedia/en/onthisday/all/{now.Month:00}/{now.Day:00}";
            var json = await WikipediaHttpClient.Shared.GetStringAsync(uri);
            var data = JsonSerializer.Deserialize(json, OnThisDayJsonSerializerContext.Default.OnThisDay);
            foreach (var article in data?.Selected?.SelectMany(x => x.Pages ?? Array.Empty<OnThisDayArticle>())
                ?? Enumerable.Empty<OnThisDayArticle>())
            {
                _wikipediaItems.Add(article);
            }
            WikipediaStatus = $"{_wikipediaItems.Count} virtualized Wikipedia articles loaded";
        }
        catch (Exception e)
        {
            AddWikipediaFixture();
            WikipediaStatus = $"Network unavailable; showing offline fixture. {e.Message}";
        }
    }

    private void AddWikipediaFixture()
    {
        _wikipediaItems.Add(new OnThisDayArticle
        {
            Titles = new OnThisDayTitles { Normalized = "TreeDataGrid Core sample" },
            Extract = "A deterministic row confirms that the virtualized Core source renders without a legacy adapter."
        });
    }

    private static Core.HierarchicalTreeDataGridSource<DragDropItem> CreateDragDropSource()
    {
        var source = new Core.HierarchicalTreeDataGridSource<DragDropItem>(DragDropItem.CreateRandomItems());
        source.Columns.Add(new CoreModels.HierarchicalExpanderColumn<DragDropItem>(
            new CoreModels.TextColumn<DragDropItem, string>("Name", x => x.Name,
                new Core.GridLength(2, Core.GridUnitType.Star)),
            x => x.Children));
        source.Columns.Add(new CoreModels.CheckBoxColumn<DragDropItem>("Allow Drag", x => x.AllowDrag,
            (x, value) => x.AllowDrag = value));
        source.Columns.Add(new CoreModels.CheckBoxColumn<DragDropItem>("Allow Drop", x => x.AllowDrop,
            (x, value) => x.AllowDrop = value));
        source.RowSelection!.SingleSelect = false;
        return source;
    }

    private static Core.HierarchicalTreeDataGridSource<DemoPerson> CreatePeopleSource()
    {
        var people = DemoPerson.Create();
        var source = new Core.HierarchicalTreeDataGridSource<DemoPerson>(people);
        source.Columns.Add(new CoreModels.HierarchicalExpanderColumn<DemoPerson>(
            new CoreModels.TextColumn<DemoPerson, string>("Name", x => x.Name,
                new Core.GridLength(2, Core.GridUnitType.Star)),
            x => x.Children, x => x.Children.Count > 0, x => x.Expansion.IsExpanded,
            (x, value) => x.Expansion.IsExpanded = value));
        source.Columns.Add(new CoreModels.TextColumn<DemoPerson, string>("Title", x => x.Title,
            new Core.GridLength(2, Core.GridUnitType.Star)));
        source.Columns.Add(new CoreModels.TextColumn<DemoPerson, int>("Age", x => x.Age));
        source.Columns.Add(new CoreModels.CheckBoxColumn<DemoPerson>("Active", x => x.IsActive,
            (x, value) => x.IsActive = value));
        return source;
    }

    private static Core.FlatTreeDataGridSource<DemoTemplateItem> CreateTemplateSource()
    {
        var items = new ObservableCollection<DemoTemplateItem>(Enumerable.Range(1, 200).Select(index =>
            new DemoTemplateItem(index % 3 == 0, $"Item {index:000}", $"Type {(char)('A' + index % 4)}",
                $"Details for item {index:000}")));
        var source = new Core.FlatTreeDataGridSource<DemoTemplateItem>(items);
        source.Columns.Add(new CoreModels.TemplateColumn<DemoTemplateItem>("Status", "status",
            new Core.GridLength(70), ItemOptions(x => x.IsFlagged)));
        source.Columns.Add(new CoreModels.TextColumn<DemoTemplateItem, string>("Name", x => x.Name,
            Core.GridLength.Star));
        source.Columns.Add(new CoreModels.TextColumn<DemoTemplateItem, string>("Type", x => x.Type,
            Core.GridLength.Star));
        source.Columns.Add(new CoreModels.TemplateColumn<DemoTemplateItem>("Details", "details",
            new Core.GridLength(2, Core.GridUnitType.Star), ItemOptions(x => x.Details)));
        return source;
    }

    private static CoreModels.ColumnOptions<DemoTemplateItem> ItemOptions<T>(Func<DemoTemplateItem, T> selector) =>
        new()
        {
            CompareAscending = (x, y) => Compare(x, y, selector),
            CompareDescending = (x, y) => Compare(y, x, selector),
        };

    private static int Compare<T>(DemoTemplateItem? x, DemoTemplateItem? y, Func<DemoTemplateItem, T> selector)
    {
        if (x is null) return y is null ? 0 : -1;
        if (y is null) return 1;
        return Comparer<T>.Default.Compare(selector(x), selector(y));
    }

    private static TreeDataGridPresentationOptions<DemoTemplateItem> CreateTemplateOptions()
    {
        var options = new TreeDataGridPresentationOptions<DemoTemplateItem>();
        options.Columns.Add("status", column => new UiModels.TemplateColumn<DemoTemplateItem>(
            column.Header, new FuncDataTemplate<DemoTemplateItem>((item, _) =>
                new CheckBox { IsChecked = item?.IsFlagged, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center })));
        options.Columns.Add("details", column => new UiModels.TemplateColumn<DemoTemplateItem>(
            column.Header, new FuncDataTemplate<DemoTemplateItem>((item, _) =>
                new Border
                {
                    Background = Avalonia.Media.Brushes.SteelBlue,
                    CornerRadius = new Avalonia.CornerRadius(8),
                    Padding = new Avalonia.Thickness(8, 2),
                    Child = new TextBlock { Text = item?.Details, Foreground = Avalonia.Media.Brushes.White }
                })));
        return options;
    }
}

internal sealed record DemoTemplateItem(bool IsFlagged, string Name, string Type, string Details);

internal sealed class DemoPerson : ReactiveObject
{
    private bool _isActive;
    public string Name { get; init; } = "";
    public string Title { get; init; } = "";
    public int Age { get; init; }
    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }
    public ExpansionState Expansion { get; } = new();
    public ObservableCollection<DemoPerson> Children { get; } = new();

    public static ObservableCollection<DemoPerson> Create()
    {
        var lead = new DemoPerson { Name = "Eleanor Pope", Title = "Engineering Manager", Age = 32, IsActive = true };
        lead.Children.Add(new DemoPerson { Name = "Marcel Gutierrez", Title = "Intern", Age = 19, IsActive = true });
        var director = new DemoPerson { Name = "Jeremy Navarro", Title = "Director", Age = 47, IsActive = true };
        var engineer = new DemoPerson { Name = "Jane Navarro", Title = "Staff Engineer", Age = 42, IsActive = true };
        engineer.Children.Add(new DemoPerson { Name = "Lailah Velazquez", Title = "Product Designer", Age = 28 });
        director.Children.Add(engineer);
        return new ObservableCollection<DemoPerson> { lead, director,
            new() { Name = "Jazmine Schroeder", Title = "Support Lead", Age = 36 } };
    }
}

internal sealed class ExpansionState : ReactiveObject
{
    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }
}
