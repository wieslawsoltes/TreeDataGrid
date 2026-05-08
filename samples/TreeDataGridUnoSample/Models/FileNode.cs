#nullable enable

using System.Collections.ObjectModel;
using System.ComponentModel;
using Uno.Controls.Models;

namespace TreeDataGridUnoSample.Models
{
    public sealed class FileNode : NotifyingBase, IEditableObject
    {
        private string _name;
        private string _kind;
        private long? _size;
        private bool _isChecked;
        private bool _isExpanded;
        private Snapshot? _snapshot;

        public FileNode(string name, string kind, long? size = null, params FileNode[] children)
        {
            _name = name;
            _kind = kind;
            _size = size;
            Children = new ObservableCollection<FileNode>(children);
        }

        public string Name
        {
            get => _name;
            set => RaiseAndSetIfChanged(ref _name, value);
        }

        public string Kind
        {
            get => _kind;
            set => RaiseAndSetIfChanged(ref _kind, value);
        }

        public long? Size
        {
            get => _size;
            set => RaiseAndSetIfChanged(ref _size, value);
        }

        public bool IsChecked
        {
            get => _isChecked;
            set => RaiseAndSetIfChanged(ref _isChecked, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => RaiseAndSetIfChanged(ref _isExpanded, value);
        }

        public ObservableCollection<FileNode> Children { get; }

        public bool HasChildren => Children.Count > 0;

        public void BeginEdit()
        {
            _snapshot = new Snapshot(_name, _kind, _size, _isChecked);
        }

        public void CancelEdit()
        {
            if (_snapshot is null)
                return;

            Name = _snapshot.Name;
            Kind = _snapshot.Kind;
            Size = _snapshot.Size;
            IsChecked = _snapshot.IsChecked;
            _snapshot = null;
        }

        public void EndEdit()
        {
            _snapshot = null;
        }

        private sealed record Snapshot(string Name, string Kind, long? Size, bool IsChecked);
    }
}
