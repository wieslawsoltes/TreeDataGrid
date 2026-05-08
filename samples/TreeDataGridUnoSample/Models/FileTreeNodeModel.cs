using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Uno.Controls.Models;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace TreeDataGridDemo.Models;

public class FileTreeNodeModel : NotifyingBase, IEditableObject
{
    private static readonly ImageSource s_fileIcon = CreateAssetImage("ms-appx:///Assets/file.png");
    private static readonly ImageSource s_folderIcon = CreateAssetImage("ms-appx:///Assets/folder.png");
    private static readonly ImageSource s_folderOpenIcon = CreateAssetImage("ms-appx:///Assets/folder-open.png");

    private string _path;
    private string _name;
    private string? _undoName;
    private long? _size;
    private DateTimeOffset? _modified;
    private ObservableCollection<FileTreeNodeModel>? _children;
    private bool _hasChildren = true;
    private bool _isExpanded;
    private bool _isChecked;

    public FileTreeNodeModel(string path, bool isDirectory, bool isRoot = false)
    {
        _path = path;
        _name = isRoot ? path : GetDisplayName(path);
        _isExpanded = isRoot;
        IsDirectory = isDirectory;
        _hasChildren = isDirectory;

        if (!isDirectory && File.Exists(path))
        {
            var info = new FileInfo(path);
            _size = info.Length;
            _modified = info.LastWriteTimeUtc;
        }
    }

    public string Path
    {
        get => _path;
        private set => RaiseAndSetIfChanged(ref _path, value);
    }

    public string Name
    {
        get => _name;
        set => RaiseAndSetIfChanged(ref _name, value);
    }

    public long? Size
    {
        get => _size;
        private set => RaiseAndSetIfChanged(ref _size, value);
    }

    public DateTimeOffset? Modified
    {
        get => _modified;
        private set => RaiseAndSetIfChanged(ref _modified, value);
    }

    public bool HasChildren
    {
        get => _hasChildren;
        private set => RaiseAndSetIfChanged(ref _hasChildren, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (RaiseAndSetIfChanged(ref _isExpanded, value))
                RaisePropertyChanged(nameof(IconSource));
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set => RaiseAndSetIfChanged(ref _isChecked, value);
    }

    public bool IsDirectory { get; }

    public ImageSource IconSource => IsDirectory
        ? (IsExpanded ? s_folderOpenIcon : s_folderIcon)
        : s_fileIcon;

    public ObservableCollection<FileTreeNodeModel> Children => _children ??= LoadChildren();

    public static Comparison<FileTreeNodeModel?> SortAscending<T>(Func<FileTreeNodeModel, T?> selector)
    {
        return (x, y) => CompareNodes(x, y, selector, descending: false);
    }

    public static Comparison<FileTreeNodeModel?> SortDescending<T>(Func<FileTreeNodeModel, T?> selector)
    {
        return (x, y) => CompareNodes(x, y, selector, descending: true);
    }

    void IEditableObject.BeginEdit() => _undoName = Name;

    void IEditableObject.CancelEdit()
    {
        if (_undoName is not null)
            Name = _undoName;
    }

    void IEditableObject.EndEdit() => _undoName = null;

    private ObservableCollection<FileTreeNodeModel> LoadChildren()
    {
        if (!IsDirectory)
            throw new NotSupportedException();

        var result = new ObservableCollection<FileTreeNodeModel>();
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
        };

        foreach (var directory in Directory.EnumerateDirectories(Path, "*", options))
            result.Add(new FileTreeNodeModel(directory, isDirectory: true));

        foreach (var file in Directory.EnumerateFiles(Path, "*", options))
            result.Add(new FileTreeNodeModel(file, isDirectory: false));

        HasChildren = result.Count > 0;
        return result;
    }

    private static int CompareNodes<T>(FileTreeNodeModel? x, FileTreeNodeModel? y, Func<FileTreeNodeModel, T?> selector, bool descending)
    {
        if (x is null && y is null)
            return 0;
        if (x is null)
            return descending ? 1 : -1;
        if (y is null)
            return descending ? -1 : 1;
        if (x.IsDirectory != y.IsDirectory)
            return x.IsDirectory ? -1 : 1;

        return descending
            ? Comparer<T?>.Default.Compare(selector(y), selector(x))
            : Comparer<T?>.Default.Compare(selector(x), selector(y));
    }

    private static string GetDisplayName(string path)
    {
        var trimmed = System.IO.Path.TrimEndingDirectorySeparator(path);
        if (string.IsNullOrEmpty(trimmed))
            return path;

        var fileName = System.IO.Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(fileName) ? path : fileName;
    }

    private static ImageSource CreateAssetImage(string uri)
    {
        return new BitmapImage
        {
            UriSource = new Uri(uri),
        };
    }
}
