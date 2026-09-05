using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TreeDataGridDemo.Models;
using Xunit;

namespace TreeDataGridUnoSample.Tests;

public class FilesViewModelTests
{
    [Fact]
    public async Task Shared_nodes_report_create_change_rename_delete_and_release_watchers()
    {
        using var fixture = new DirectoryFixture();
        var queue = new ConcurrentQueue<Action>();
        using var root = new FileTreeNodeModel(fixture.Path, true, dispatch: queue.Enqueue);
        var children = root.Children;
        Assert.False(root.HasChildren);
        Assert.True(root.IsWatching);
        var path = System.IO.Path.Combine(fixture.Path, "new.txt");
        await File.WriteAllTextAsync(path, "one");
        await WaitAsync(queue, () => children.Count == 1);
        var child = children[0];
        Assert.True(root.HasChildren);
        await File.WriteAllTextAsync(path, "longer contents");
        await WaitAsync(queue, () => child.Size == 15);
        var renamed = System.IO.Path.Combine(fixture.Path, "renamed.txt");
        File.Move(path, renamed);
        await WaitAsync(queue, () => children.Any(x => x.Name == "renamed.txt"));
        File.Delete(renamed);
        await WaitAsync(queue, () => children.Count == 0);
        Assert.False(root.HasChildren);
        root.Dispose();
        Assert.False(root.IsWatching);
        Assert.Empty(root.Children);
    }

    [Fact]
    public async Task Loaded_directory_rename_updates_descendants_and_watcher_paths()
    {
        using var fixture = new DirectoryFixture();
        var directory = Directory.CreateDirectory(System.IO.Path.Combine(fixture.Path, "before"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(directory.FullName, "child.txt"), "a");
        var queue = new ConcurrentQueue<Action>();
        using var root = new FileTreeNodeModel(fixture.Path, true, dispatch: queue.Enqueue);
        var folder = Assert.Single(root.Children);
        var children = folder.Children;
        var renamed = System.IO.Path.Combine(fixture.Path, "after");
        Directory.Move(directory.FullName, renamed);
        await WaitAsync(queue, () => root.Children.Any(x => x.Name == "after"));
        folder = root.Children.Single();
        Assert.Equal(renamed, folder.Path);
        Assert.All(folder.Children, x => Assert.StartsWith(renamed + System.IO.Path.DirectorySeparatorChar, x.Path));
        await File.WriteAllTextAsync(System.IO.Path.Combine(renamed, "later.txt"), "later");
        await WaitAsync(queue, () => folder.Children.Count == 2);
        root.Dispose();
        Assert.False(folder.IsWatching);
    }

    [Fact]
    public async Task Tree_and_flat_modes_share_nodes_and_failed_open_preserves_working_source()
    {
        using var fixture = new DirectoryFixture();
        Directory.CreateDirectory(System.IO.Path.Combine(fixture.Path, "folder"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(fixture.Path, "file.txt"), "a");
        var queue = new ConcurrentQueue<Action>();
        using var model = new FilesViewModel(queue.Enqueue);
        await model.OpenAsync(fixture.Path);
        var root = model.Root!;
        var tree = model.Source!;
        Assert.Equal(3, tree.Rows.Count);
        Assert.False(root.Children.Single(x => x.IsDirectory).HasLoadedChildren);
        model.FlatMode = true;
        Assert.Equal(2, model.Source!.Rows.Count);
        Assert.All(model.Source.Items, x => Assert.Contains(x, root.Children));
        var flat = model.Source;
        await model.OpenAsync(System.IO.Path.Combine(fixture.Path, "missing"));
        Assert.Same(flat, model.Source);
        Assert.StartsWith("Unable to open folder:", model.Status);
        model.FlatMode = false;
        Assert.Same(tree, model.Source);
        model.Close();
        Assert.Null(model.Source);
        Assert.False(root.IsWatching);
    }

    [Fact]
    public async Task Close_during_load_prevents_late_source_publication()
    {
        using var fixture = new DirectoryFixture();
        var queue = new ConcurrentQueue<Action>();
        using var model = new FilesViewModel(queue.Enqueue);
        var loading = model.OpenAsync(fixture.Path);
        model.Close();
        await loading;
        Assert.Null(model.Root);
        Assert.Null(model.Source);
    }

    [Fact]
    public async Task Disposal_ignores_already_queued_notifications()
    {
        using var fixture = new DirectoryFixture();
        var queue = new ConcurrentQueue<Action>();
        var root = new FileTreeNodeModel(fixture.Path, true, dispatch: queue.Enqueue);
        var children = root.Children;
        await File.WriteAllTextAsync(System.IO.Path.Combine(fixture.Path, "queued.txt"), "data");
        for (var i = 0; queue.IsEmpty && i < 100; ++i) await Task.Delay(20);
        Assert.False(queue.IsEmpty);
        root.Dispose();
        while (queue.TryDequeue(out var action)) action();
        Assert.Empty(children);
        Assert.False(root.IsWatching);
    }

    private static async Task WaitAsync(ConcurrentQueue<Action> queue, Func<bool> condition)
    {
        for (var i = 0; i < 200; ++i)
        {
            while (queue.TryDequeue(out var action)) action();
            if (condition()) return;
            await Task.Delay(20);
        }
        Assert.True(condition(), "File-system notification did not reach the shared model.");
    }
    private sealed class DirectoryFixture : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("TreeDataGridUno-files-");
        public string Path => _directory.FullName;
        public void Dispose() => _directory.Delete(recursive: true);
    }
}
