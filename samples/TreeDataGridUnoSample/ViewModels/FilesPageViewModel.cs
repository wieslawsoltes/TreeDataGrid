using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using TreeDataGridDemo.Models;

namespace TreeDataGridDemo.ViewModels;

public class FilesPageViewModel : NotifyingBase
{
    private readonly HierarchicalTreeDataGridSource<FileTreeNodeModel> _treeSource;
    private FlatTreeDataGridSource<FileTreeNodeModel>? _flatSource;
    private ITreeDataGridSource<FileTreeNodeModel> _source;
    private bool _cellSelection;
    private FileTreeNodeModel? _root;
    private string _selectedDrive;
    private string? _selectedPath;

    public FilesPageViewModel()
    {
        Drives = CreateDrives();
        _selectedDrive = Drives.FirstOrDefault() ?? GetFallbackRoot();
        _treeSource = CreateTreeSource();
        _source = _treeSource;
        ApplyRoot();
        ApplySelectionMode();
    }

    public bool CellSelection
    {
        get => _cellSelection;
        set
        {
            if (!RaiseAndSetIfChanged(ref _cellSelection, value))
                return;

            ApplySelectionMode();
        }
    }

    public IList<string> Drives { get; }

    public bool FlatList
    {
        get => Source != _treeSource;
        set
        {
            if (value == FlatList)
                return;

            Source = value ? _flatSource ??= CreateFlatSource() : _treeSource;
            ApplyRoot();
            ApplySelectionMode();
        }
    }

    public string SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            if (!RaiseAndSetIfChanged(ref _selectedDrive, value))
                return;

            ApplyRoot();
        }
    }

    public string? SelectedPath
    {
        get => _selectedPath;
        set => SetSelectedPath(value);
    }

    public ITreeDataGridSource<FileTreeNodeModel> Source
    {
        get => _source;
        private set => RaiseAndSetIfChanged(ref _source, value);
    }

    private FlatTreeDataGridSource<FileTreeNodeModel> CreateFlatSource()
    {
        var initialItems = _root is not null ? _root.Children : Enumerable.Empty<FileTreeNodeModel>();
        var result = new FlatTreeDataGridSource<FileTreeNodeModel>(initialItems)
        {
            Columns =
            {
                new CheckBoxColumn<FileTreeNodeModel>(
                    null,
                    x => x.IsChecked,
                    (model, value) => model.IsChecked = value,
                    options: new CheckBoxColumnOptions<FileTreeNodeModel>
                    {
                        CanUserResizeColumn = false,
                    }),
                new TemplateColumn<FileTreeNodeModel>(
                    "Name",
                    "FileNameCell",
                    "FileNameEditCell",
                    new GridLength(1, GridUnitType.Star),
                    new TemplateColumnOptions<FileTreeNodeModel>
                    {
                        CompareAscending = FileTreeNodeModel.SortAscending(x => x.Name),
                        CompareDescending = FileTreeNodeModel.SortDescending(x => x.Name),
                    }),
                new TextColumn<FileTreeNodeModel, long?>(
                    "Size",
                    x => x.Size,
                    options: new TextColumnOptions<FileTreeNodeModel>
                    {
                        CompareAscending = FileTreeNodeModel.SortAscending(x => x.Size),
                        CompareDescending = FileTreeNodeModel.SortDescending(x => x.Size),
                    }),
                new TextColumn<FileTreeNodeModel, DateTimeOffset?>(
                    "Modified",
                    x => x.Modified,
                    options: new TextColumnOptions<FileTreeNodeModel>
                    {
                        CompareAscending = FileTreeNodeModel.SortAscending(x => x.Modified),
                        CompareDescending = FileTreeNodeModel.SortDescending(x => x.Modified),
                    }),
            }
        };

        return result;
    }

    private HierarchicalTreeDataGridSource<FileTreeNodeModel> CreateTreeSource()
    {
        return new HierarchicalTreeDataGridSource<FileTreeNodeModel>(Array.Empty<FileTreeNodeModel>())
        {
            Columns =
            {
                new CheckBoxColumn<FileTreeNodeModel>(
                    null,
                    x => x.IsChecked,
                    (model, value) => model.IsChecked = value,
                    options: new CheckBoxColumnOptions<FileTreeNodeModel>
                    {
                        CanUserResizeColumn = false,
                    }),
                new HierarchicalExpanderColumn<FileTreeNodeModel>(
                    new TemplateColumn<FileTreeNodeModel>(
                        "Name",
                        "FileNameCell",
                        "FileNameEditCell",
                        new GridLength(1, GridUnitType.Star),
                        new TemplateColumnOptions<FileTreeNodeModel>
                        {
                            CompareAscending = FileTreeNodeModel.SortAscending(x => x.Name),
                            CompareDescending = FileTreeNodeModel.SortDescending(x => x.Name),
                        }),
                    x => x.Children,
                    x => x.HasChildren,
                    x => x.IsExpanded),
                new TextColumn<FileTreeNodeModel, long?>(
                    "Size",
                    x => x.Size,
                    options: new TextColumnOptions<FileTreeNodeModel>
                    {
                        CompareAscending = FileTreeNodeModel.SortAscending(x => x.Size),
                        CompareDescending = FileTreeNodeModel.SortDescending(x => x.Size),
                    }),
                new TextColumn<FileTreeNodeModel, DateTimeOffset?>(
                    "Modified",
                    x => x.Modified,
                    options: new TextColumnOptions<FileTreeNodeModel>
                    {
                        CompareAscending = FileTreeNodeModel.SortAscending(x => x.Modified),
                        CompareDescending = FileTreeNodeModel.SortDescending(x => x.Modified),
                    }),
            }
        };
    }

    private void ApplyRoot()
    {
        _root = new FileTreeNodeModel(_selectedDrive, isDirectory: true, isRoot: true);

        _treeSource.Items = new[] { _root };
        if (_flatSource is not null)
            _flatSource.Items = _root.Children;

        SetSelectedPath(_selectedPath, allowSelectionModeChange: false);
    }

    private void ApplySelectionMode()
    {
        var rowSelection = new TreeDataGridRowSelectionModel<FileTreeNodeModel>(Source)
        {
            SingleSelect = false,
        };
        rowSelection.SelectionChanged += SelectionChanged;
        Source.Selection = rowSelection;
    }

    private void SetSelectedPath(string? value, bool allowSelectionModeChange = true)
    {
        if (_selectedPath == value && string.IsNullOrWhiteSpace(value))
            return;

        if (string.IsNullOrWhiteSpace(value))
        {
            _selectedPath = value;
            RaisePropertyChanged(nameof(SelectedPath));
            return;
        }

        if (allowSelectionModeChange && CellSelection)
            CellSelection = false;

        if (Source.Selection is not ITreeDataGridRowSelectionModel<FileTreeNodeModel> rowSelection)
            return;

        var path = value!.Trim();
        _selectedPath = path;
        RaisePropertyChanged(nameof(SelectedPath));

        var matchingDrive = Drives
            .OrderByDescending(x => x.Length)
            .FirstOrDefault(x => path.StartsWith(x, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(matchingDrive) && !string.Equals(_selectedDrive, matchingDrive, StringComparison.OrdinalIgnoreCase))
        {
            SelectedDrive = matchingDrive;
        }

        if (_root is null)
            return;

        var relativePath = System.IO.Path.GetRelativePath(_selectedDrive, path);
        var components = relativePath == "."
            ? Array.Empty<string>()
            : relativePath.Split(new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        var currentNode = _root;
        var index = new IndexPath(0);

        foreach (var component in components)
        {
            currentNode.IsExpanded = true;
            var childIndex = currentNode.Children
                .Select((item, childIndex) => new { item, childIndex })
                .FirstOrDefault(x => string.Equals(x.item.Name, component, StringComparison.OrdinalIgnoreCase))?.childIndex ?? -1;

            if (childIndex < 0)
            {
                index = IndexPath.Unselected;
                break;
            }

            currentNode = currentNode.Children[childIndex];
            index = index.Append(childIndex);
        }

        rowSelection.SelectedIndex = index;
    }

    private void SelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileTreeNodeModel> e)
    {
        if (Source.Selection is not ITreeDataGridRowSelectionModel<FileTreeNodeModel> rowSelection)
            return;

        var selectedPath = rowSelection.SelectedItem?.Path;
        if (RaiseAndSetIfChanged(ref _selectedPath, selectedPath, nameof(SelectedPath)))
            RaisePropertyChanged(nameof(SelectedPath));
    }

    private static IList<string> CreateDrives()
    {
        var drives = DriveInfo.GetDrives()
            .Where(x => x.IsReady)
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (drives.Count == 0)
            drives.Add(GetFallbackRoot());

        return drives;
    }

    private static string GetFallbackRoot()
    {
        return System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? "/";
    }
}
