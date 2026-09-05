using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using ReactiveUI;

namespace TreeDataGridDemo.Models
{
    public partial class FileTreeNodeModel : ReactiveObject, IEditableObject, IDisposable
    {
        private string _path;
        private string _name;
        private string? _undoName;
        private long? _size;
        private DateTimeOffset? _modified;
        private FileSystemWatcher? _watcher;
        private ObservableCollection<FileTreeNodeModel>? _children;
        private bool _hasChildren = true;
        private bool _isExpanded;
        private bool _isChecked;
        private bool _disposed;
        private readonly Action<Action> _dispatch;

        public FileTreeNodeModel(
            string path,
            bool isDirectory,
            bool isRoot = false,
            Action<Action>? dispatch = null)
        {
            _dispatch = dispatch ?? CreateDispatcher();
            _path = path;
            _name = isRoot ? path : System.IO.Path.GetFileName(Path);
            _isExpanded = isRoot;
            IsDirectory = isDirectory;
            HasChildren = isDirectory;

            if (!isDirectory)
            {
                var info = new FileInfo(path);
                Size = info.Length;
                Modified = info.LastWriteTimeUtc;
            }
        }

        public string Path 
        {
            get => _path;
            private set => this.RaiseAndSetIfChanged(ref _path, value);
        }

        public string Name 
        {
            get => _name;
            private set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public long? Size 
        {
            get => _size;
            private set => this.RaiseAndSetIfChanged(ref _size, value);
        }

        public DateTimeOffset? Modified 
        {
            get => _modified;
            private set => this.RaiseAndSetIfChanged(ref _modified, value);
        }

        public bool HasChildren
        {
            get => _hasChildren;
            private set => this.RaiseAndSetIfChanged(ref _hasChildren, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
        }

        public bool IsChecked { get => _isChecked; set => this.RaiseAndSetIfChanged(ref _isChecked, value); }
        public bool IsDirectory { get; }
        internal bool HasLoadedChildren => _children is not null;
        internal bool IsWatching => _watcher is not null;
        public IReadOnlyList<FileTreeNodeModel> Children => _disposed ? Array.Empty<FileTreeNodeModel>() : _children ??= LoadChildren();
        private static partial Action<Action> CreateDispatcher();

        private ObservableCollection<FileTreeNodeModel> LoadChildren()
        {
            if (!IsDirectory)
            {
                throw new NotSupportedException();
            }

            var options = new EnumerationOptions { IgnoreInaccessible = true };
            var result = new ObservableCollection<FileTreeNodeModel>();

            foreach (var d in Directory.EnumerateDirectories(Path, "*", options))
            {
                result.Add(new FileTreeNodeModel(d, true, dispatch: _dispatch));
            }

            foreach (var f in Directory.EnumerateFiles(Path, "*", options))
            {
                try { result.Add(new FileTreeNodeModel(f, false, dispatch: _dispatch)); }
                catch (IOException) { } // A directory entry may disappear during enumeration.
                catch (UnauthorizedAccessException) { }
            }

            _watcher = new FileSystemWatcher
            {
                Path = Path,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
            };

            _watcher.Changed += OnChanged;
            _watcher.Created += OnCreated;
            _watcher.Deleted += OnDeleted;
            _watcher.Renamed += OnRenamed;
            _children = result;
            _watcher.EnableRaisingEvents = true;

            if (result.Count == 0)
                HasChildren = false;

            return result;
        }

        public static Comparison<FileTreeNodeModel?> SortAscending<T>(Func<FileTreeNodeModel, T> selector)
        {
            return (x, y) =>
            {
                if (x is null && y is null)
                    return 0;
                else if (x is null)
                    return -1;
                else if (y is null)
                    return 1;
                if (x.IsDirectory == y.IsDirectory)
                    return Comparer<T>.Default.Compare(selector(x), selector(y));
                else if (x.IsDirectory)
                    return -1;
                else
                    return 1;
            };
        }

        public static Comparison<FileTreeNodeModel?> SortDescending<T>(Func<FileTreeNodeModel, T> selector)
        {
            return (x, y) =>
            {
                if (x is null && y is null)
                    return 0;
                else if (x is null)
                    return 1;
                else if (y is null)
                    return -1;
                if (x.IsDirectory == y.IsDirectory)
                    return Comparer<T>.Default.Compare(selector(y), selector(x));
                else if (x.IsDirectory)
                    return -1;
                else
                    return 1;
            };
        }

        void IEditableObject.BeginEdit() => _undoName = _name;
        void IEditableObject.CancelEdit() => _name = _undoName!;
        void IEditableObject.EndEdit() => _undoName = null;

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed && File.Exists(e.FullPath))
            {
                PostChange(() =>
                {
                    foreach (var child in _children!)
                    {
                        if (child.Path == e.FullPath)
                        {
                            if (!child.IsDirectory)
                            {
                                var info = new FileInfo(e.FullPath);
                                child.Size = info.Length;
                                child.Modified = info.LastWriteTimeUtc;
                            }
                            break;
                        }
                    }
                });
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            PostChange(() =>
            {
                if (_children!.Any(x => x.Path == e.FullPath)) return;
                var node = new FileTreeNodeModel(
                    e.FullPath,
                    File.GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory),
                    dispatch: _dispatch);
                _children!.Add(node);
                HasChildren = true;
            });
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            PostChange(() =>
            {
                for (var i = 0; i < _children!.Count; ++i)
                {
                    if (_children[i].Path == e.FullPath)
                    {
                        _children[i].Dispose();
                        _children.RemoveAt(i);
                        HasChildren = _children.Count > 0;
                        break;
                    }
                }
            });
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            PostChange(() =>
            {
                foreach (var child in _children!)
                {
                    if (child.Path == e.OldFullPath)
                    {
                        child.Relocate(e.FullPath);
                        break;
                    }
                }
            });
        }

        private void Relocate(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            if (_watcher is not null) _watcher.Path = path;
            if (_children is not null)
                foreach (var child in _children) child.Relocate(System.IO.Path.Combine(path, child.Name));
        }

        private void PostChange(Action change) => _dispatch(() =>
        {
            if (_disposed || _children is null) return;
            try { change(); }
            catch (IOException) { } // Coalesced watcher notifications can outlive a file.
            catch (UnauthorizedAccessException) { }
        });

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnChanged;
                _watcher.Created -= OnCreated;
                _watcher.Deleted -= OnDeleted;
                _watcher.Renamed -= OnRenamed;
                _watcher.Dispose();
                _watcher = null;
            }
            if (_children is not null)
                foreach (var child in _children) child.Dispose();
            _children = null;
        }
    }
}
