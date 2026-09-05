using System;
using System.IO;
using System.Threading.Tasks;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridDemo.Models;

namespace TreeDataGridUnoSample;

internal sealed class FilesViewModel(Action<Action> dispatch) : NotifyingBase, IDisposable
{
    private HierarchicalTreeDataGridSource<FileTreeNodeModel>? _tree;
    private FlatTreeDataGridSource<FileTreeNodeModel>? _flat;
    private ITreeDataGridSource? _source;
    private bool _flatMode;
    private int _generation;
    private bool _disposed;
    private string _status = "Open a folder to inspect it. The sample never modifies files.";
    public FileTreeNodeModel? Root { get; private set; }
    public ITreeDataGridSource? Source { get => _source; private set => RaiseAndSetIfChanged(ref _source, value); }
    public string Status { get => _status; private set => RaiseAndSetIfChanged(ref _status, value); }
    public Task LoadingTask { get; private set; } = Task.CompletedTask;
    public bool FlatMode
    {
        get => _flatMode;
        set { _flatMode = value; Source = value ? _flat : _tree; }
    }
    public Task OpenAsync(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return LoadingTask = LoadAsync(path, ++_generation);
    }
    private async Task LoadAsync(string path, int generation)
    {
        Status = "Loading folder…";
        FileTreeNodeModel? staged = null;
        try
        {
            var fullPath = Path.GetFullPath(path);
            staged = await Task.Run(() =>
            {
                var root = new FileTreeNodeModel(fullPath, true, true, dispatch);
                try { _ = root.Children; return root; }
                catch { root.Dispose(); throw; }
            });
            if (generation != _generation) return;
            var tree = new HierarchicalTreeDataGridSource<FileTreeNodeModel>(new[] { staged });
            var flat = new FlatTreeDataGridSource<FileTreeNodeModel>(staged.Children);
            try
            {
                AddColumns(tree.Columns, hierarchical: true);
                AddColumns(flat.Columns, hierarchical: false);
                ReleaseSources();
                Root = staged;
                staged = null;
                _tree = tree;
                _flat = flat;
                Source = FlatMode ? flat : tree;
                Status = $"{Root.Path} · {Root.Children.Count} immediate entries · live, read-only file-system view";
            }
            catch { tree.Dispose(); flat.Dispose(); throw; }
        }
        catch (Exception error)
        {
            if (generation == _generation) Status = $"Unable to open folder: {error.Message}";
        }
        finally { staged?.Dispose(); }
    }
    private static void AddColumns(ColumnList<FileTreeNodeModel> columns, bool hierarchical)
    {
        columns.Add(new CheckBoxColumn<FileTreeNodeModel>("✓", x => x.IsChecked, (x, value) => x.IsChecked = value, width: new(44)));
        var name = new TemplateColumn<FileTreeNodeModel>("Name", "FileName", width: new(3, GridUnitType.Star), options: Options(x => x.Name));
        columns.Add(hierarchical
            ? new HierarchicalExpanderColumn<FileTreeNodeModel>(name, x => x.IsDirectory ? x.Children : null,
                x => x.HasChildren, x => x.IsExpanded, (x, value) => x.IsExpanded = value)
            : name);
        columns.Add(new TextColumn<FileTreeNodeModel, long?>("Size", x => x.Size, width: new(1, GridUnitType.Star), options: Options(x => x.Size)));
        columns.Add(new TextColumn<FileTreeNodeModel, DateTimeOffset?>("Modified", x => x.Modified, width: new(2, GridUnitType.Star), options: Options(x => x.Modified)));
    }
    private static ColumnOptions<FileTreeNodeModel> Options<T>(Func<FileTreeNodeModel, T> selector) => new()
    {
        CompareAscending = FileTreeNodeModel.SortAscending(selector),
        CompareDescending = FileTreeNodeModel.SortDescending(selector),
    };
    private void ReleaseSources()
    {
        Source = null;
        _tree?.Dispose();
        _flat?.Dispose();
        Root?.Dispose();
        _tree = null;
        _flat = null;
        Root = null;
    }
    public void Close()
    {
        ++_generation;
        ReleaseSources();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}
