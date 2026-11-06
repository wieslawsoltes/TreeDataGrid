using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using TreeDataGridDemo.Models;
using Uno.Controls;
using GridLength = Microsoft.UI.Xaml.GridLength;
using GridUnitType = Microsoft.UI.Xaml.GridUnitType;

namespace TreeDataGridUnoSample.Demo.ViewModels
{
    public partial class FilesPageViewModel : NotifyingBase
    {
        private readonly HierarchicalTreeDataGridSource<FileTreeNodeModel>? _treeSource;
        private FlatTreeDataGridSource<FileTreeNodeModel>? _flatSource;
        private ITreeDataGridSource<FileTreeNodeModel> _source;
        private bool _cellSelection;
        private FileTreeNodeModel? _root;
        private string _selectedDrive;
        private string? _selectedPath;

        public FilesPageViewModel()
        {
            Drives = DriveInfo.GetDrives().Select(x => x.Name).ToList();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _selectedDrive = "C:\\";
            }
            else
            {
                _selectedDrive = Drives.FirstOrDefault() ?? "/";
            }

            _source = _treeSource = CreateTreeSource();
            OnSelectedDriveChanged();
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
                        Source.Selection = new TreeDataGridCellSelectionModel<FileTreeNodeModel>(Source) { SingleSelect = false };
                    else
                        Source.Selection = new TreeDataGridRowSelectionModel<FileTreeNodeModel>(Source) { SingleSelect = false };
                    RaisePropertyChanged();
                }
            }
        }

        public IList<string> Drives { get; }

        public bool FlatList
        {
            get => Source != _treeSource;
            set
            {
                if (value != FlatList)
                {
                    Source = value ? _flatSource ??= CreateFlatSource() : _treeSource!;
                    RaisePropertyChanged();
                }
            }
        }

        public string SelectedDrive
        {
            get => _selectedDrive;
            set
            {
                if (value is not null && RaiseAndSetIfChanged(ref _selectedDrive, value))
                    OnSelectedDriveChanged();
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

        private void OnSelectedDriveChanged()
        {
            _root = new FileTreeNodeModel(_selectedDrive, isDirectory: true, isRoot: true);

            if (_treeSource is not null)
                _treeSource.Items = new[] { _root };
            if (_flatSource is not null)
                _flatSource.Items = _root.Children;
        }

        private FlatTreeDataGridSource<FileTreeNodeModel> CreateFlatSource()
        {
            var result = new FlatTreeDataGridSource<FileTreeNodeModel>(_root!.Children)
                .WithCheckBoxColumn(null, x => x.IsChecked, o =>
                {
                    o.CanUserResize = false;
                })
                .WithTemplateColumnFromResourceKeys("Name", "FileNameCell", "FileNameEditCell", o =>
                {
                    o.Width = new GridLength(1, GridUnitType.Star);
                    o.CompareAscending = Compare(FileTreeNodeModel.SortAscending(x => x.Name));
                    o.CompareDescending = Compare(FileTreeNodeModel.SortDescending(x => x.Name));
                    o.TextSearchBinding = new Binding { Path = new PropertyPath(nameof(FileTreeNodeModel.Name)) };
                })
                .WithTextColumn("Size", x => x.Size, o =>
                {
                    o.CompareAscending = Compare(FileTreeNodeModel.SortAscending(x => x.Size));
                    o.CompareDescending = Compare(FileTreeNodeModel.SortDescending(x => x.Size));
                })
                .WithTextColumn("Modified", x => x.Modified, o =>
                {
                    o.CompareAscending = Compare(FileTreeNodeModel.SortAscending(x => x.Modified));
                    o.CompareDescending = Compare(FileTreeNodeModel.SortDescending(x => x.Modified));
                });

            result.RowSelection!.SingleSelect = false;
            result.RowSelection.SelectionChanged += SelectionChanged;
            return result;
        }

        private HierarchicalTreeDataGridSource<FileTreeNodeModel> CreateTreeSource()
        {
            var result = new HierarchicalTreeDataGridSource<FileTreeNodeModel>(Array.Empty<FileTreeNodeModel>())
                .WithCheckBoxColumn(null, x => x.IsChecked, o =>
                {
                    o.CanUserResize = false;
                })
                .WithHierarchicalExpanderColumn(
                    "Name",
                    new TreeDataGridTemplateColumn(null, "FileNameCell", "FileNameEditCell")
                    {
                        CompareAscending = Compare(FileTreeNodeModel.SortAscending(x => x.Name)),
                        CompareDescending = Compare(FileTreeNodeModel.SortDescending(x => x.Name)),
                        TextSearchBinding = new Binding { Path = new PropertyPath(nameof(FileTreeNodeModel.Name)) },
                    },
                    x => x.Children,
                    o =>
                    {
                        o.Width = new GridLength(1, GridUnitType.Star);
                        o.HasChildren = x => x.HasChildren;
                        o.IsExpanded = x => x.IsExpanded;
                    })
                .WithTextColumn("Size", x => x.Size, o =>
                {
                    o.CompareAscending = Compare(FileTreeNodeModel.SortAscending(x => x.Size));
                    o.CompareDescending = Compare(FileTreeNodeModel.SortDescending(x => x.Size));
                })
                .WithTextColumn("Modified", x => x.Modified, o =>
                {
                    o.CompareAscending = Compare(FileTreeNodeModel.SortAscending(x => x.Modified));
                    o.CompareDescending = Compare(FileTreeNodeModel.SortDescending(x => x.Modified));
                });

            result.RowSelection!.SingleSelect = false;
            result.RowSelection.SelectionChanged += SelectionChanged;
            return result;
        }

        private void SetSelectedPath(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                GetRowSelection(Source).Clear();
                return;
            }

            var path = value;
            var components = new Stack<string>();
            DirectoryInfo? d = null;

            if (File.Exists(path))
            {
                var f = new FileInfo(path);
                components.Push(f.Name);
                d = f.Directory;
            }
            else if (Directory.Exists(path))
            {
                d = new DirectoryInfo(path);
            }

            while (d is not null)
            {
                components.Push(d.Name);
                d = d.Parent;
            }

            var index = IndexPath.Unselected;

            if (components.Count > 0)
            {
                var drive = components.Pop();
                var driveIndex = Drives.ToList().FindIndex(x => string.Equals(x, drive, StringComparison.OrdinalIgnoreCase));

                if (driveIndex >= 0)
                    SelectedDrive = Drives[driveIndex];

                FileTreeNodeModel? node = _root;
                index = new IndexPath(0);

                while (node is not null && components.Count > 0)
                {
                    node.IsExpanded = true;

                    var component = components.Pop();
                    var i = node.Children.ToList().FindIndex(x => string.Equals(x.Name, component, StringComparison.OrdinalIgnoreCase));
                    node = i >= 0 ? node.Children[i] : null;
                    index = i >= 0 ? index.Append(i) : default;
                }
            }

            GetRowSelection(Source).SelectedIndex = index;
        }

        private ITreeDataGridRowSelectionModel<FileTreeNodeModel> GetRowSelection(ITreeDataGridSource source)
        {
            return source.Selection as ITreeDataGridRowSelectionModel<FileTreeNodeModel> ??
                throw new InvalidOperationException("Expected a row selection model.");
        }

        private void SelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileTreeNodeModel> e)
        {
            var selectedPath = GetRowSelection(Source).SelectedItem?.Path;
            RaiseAndSetIfChanged(ref _selectedPath, selectedPath, nameof(SelectedPath));

            foreach (var i in e.DeselectedItems)
                System.Diagnostics.Trace.WriteLine($"Deselected '{i?.Path}'");
            foreach (var i in e.SelectedItems)
                System.Diagnostics.Trace.WriteLine($"Selected '{i?.Path}'");
        }

        private static Comparison<object?> Compare(Comparison<FileTreeNodeModel?> comparison)
        {
            return (x, y) => comparison((FileTreeNodeModel?)x, (FileTreeNodeModel?)y);
        }
    }
}
