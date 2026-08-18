namespace TreeDataGridDemo.ViewModels;

internal class MainWindowViewModel
{
    private PeopleXamlPageViewModel? _peopleXaml;
    private CountriesPageViewModel? _countries;
    private FilesPageViewModel? _files;
    private WikipediaPageViewModel? _wikipedia;
    private DragDropPageViewModel? _dragDrop;

    public PeopleXamlPageViewModel PeopleXaml => _peopleXaml ??= new PeopleXamlPageViewModel();

    public CountriesPageViewModel Countries => _countries ??= new CountriesPageViewModel();

    public FilesPageViewModel Files => _files ??= new FilesPageViewModel();

    public WikipediaPageViewModel Wikipedia => _wikipedia ??= new WikipediaPageViewModel();

    public DragDropPageViewModel DragDrop => _dragDrop ??= new DragDropPageViewModel();
}
