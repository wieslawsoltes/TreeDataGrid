namespace TreeDataGridDemo.ViewModels
{
    internal class MainWindowViewModel
    {
        private CountriesPageViewModel? _countries;
        private CountriesPageViewModel? _findDisplayedRowIndex;
        private CountriesPageViewModel? _bringIntoViewNonUniformRows;
        private FilesPageViewModel? _files;
        private WikipediaPageViewModel? _wikipedia;
        private DragDropPageViewModel? _dragDrop;
        private PeopleXamlPageViewModel? _peopleXaml;
        private TemplateColumnBugPageViewModel? _templateColumnBug;

        public CountriesPageViewModel Countries
        {
            get => _countries ??= new CountriesPageViewModel();
        }

        public CountriesPageViewModel FindDisplayedRowIndex
        {
            get => _findDisplayedRowIndex ??= new CountriesPageViewModel();
        }

        public CountriesPageViewModel BringIntoViewNonUniformRows
        {
            get => _bringIntoViewNonUniformRows ??= new CountriesPageViewModel(useVariableHeightRows: true);
        }

        public PeopleXamlPageViewModel PeopleXaml
        {
            get => _peopleXaml ??= new PeopleXamlPageViewModel();
        }

        public TemplateColumnBugPageViewModel TemplateColumnBug
        {
            get => _templateColumnBug ??= new TemplateColumnBugPageViewModel();
        }

        public FilesPageViewModel Files
        {
            get => _files ??= new FilesPageViewModel();
        }

        public WikipediaPageViewModel Wikipedia
        {
            get => _wikipedia ??= new WikipediaPageViewModel();
        }

        public DragDropPageViewModel DragDrop
        {
            get => _dragDrop ??= new DragDropPageViewModel();
        }
    }
}
