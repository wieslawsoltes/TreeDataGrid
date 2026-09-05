using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridDemo.Models;

namespace TreeDataGridUnoSample;

internal static class FilesAndFindRuntimeChecks
{
    public static async Task RunAsync(MainPage page, Func<UIElement, string, Task> capture)
    {
        await CheckFilesAsync(page, capture);
        page.ShowScenario(7);
        await Task.Delay(200);
        var grid = page.Grid;
        var model = page.FindCountry;
        var selected = model.AllCountries.First(x => x.Name == "Poland");
        ((ListView)page.FindName("FindCountryList")).SelectedItem = selected;
        await Task.Delay(150);
        CheckFoundCountry(page, selected);
        var initialRow = model.DisplayedRow;
        model.Source.SortBy(model.Source.Columns[0], System.ComponentModel.ListSortDirection.Descending);
        await Task.Delay(150);
        CheckFoundCountry(page, selected);
        Check(initialRow != model.DisplayedRow, "Find Country did not remap its sorted displayed index.");
        var filter = (TextBox)page.FindName("FindFilter");
        filter.Text = "afghanistan";
        await Task.Delay(100);
        Check(model.DisplayedRow == -1 && model.Status.Contains("not displayed", StringComparison.Ordinal), "Find Country did not report a filtered-out model.");
        Check(model.Source.Rows.Count == 1 && ReferenceEquals(model.SelectedCountry, selected), "Filtering lost the complete-catalog selection.");
        filter.Text = "Europe";
        await Task.Delay(150);
        CheckFoundCountry(page, selected);
        await capture(page, "find-country");
        filter.Text = "";
        model.Source.ClearSort();
        await Task.Delay(150);
        CheckFoundCountry(page, selected);
        page.ShowScenario(0);
        await Task.Delay(100);
        Console.WriteLine("UNO_RUNTIME_FILES_FIND_PASSED: shared file nodes, lazy expansion, watcher updates/disposal, checkbox state, flat/tree identity, directory-first sorting, native catalog/filter selection, sorted displayed-row mapping");
    }

    private static async Task CheckFilesAsync(MainPage page, Func<UIElement, string, Task> capture)
    {
        var fixture = Directory.CreateTempSubdirectory("TreeDataGridUno-runtime-files-");
        var pathBox = (TextBox)page.FindName("FolderPath");
        var previousPath = pathBox.Text;
        try
        {
            await Task.Run(() =>
            {
                Directory.CreateDirectory(Path.Combine(fixture.FullName, "empty"));
                Directory.CreateDirectory(Path.Combine(fixture.FullName, "nested"));
                File.WriteAllText(Path.Combine(fixture.FullName, "nested", "child.txt"), "child");
                for (var i = 0; i < 180; ++i) File.WriteAllText(Path.Combine(fixture.FullName, $"file-{i:000}.txt"), $"Fixture {i:000}");
            });
            pathBox.Text = fixture.FullName;
            page.ShowScenario(5);
            await page.Files.LoadingTask;
            await Task.Delay(200);
            var grid = page.Grid;
            var source = page.Files.Source ?? throw new InvalidOperationException(page.Files.Status);
            var root = page.Files.Root!;
            Check(ReferenceEquals(grid.Presentation!.Rows, source.Rows), "Files did not expose actual Core rows.");
            Check(source.Rows.Count == 183 && root.IsWatching, "File root did not load its immediate entries and watcher.");
            var folder = root.Children.Single(x => x.Name == "nested");
            Check(!folder.HasLoadedChildren && !folder.IsWatching, "Rendering an expander eagerly opened a nested directory.");
            Check(grid.RowsPresenter.RealizedCells.Count < 200, "Files did not virtualize its cells.");
            source.SortBy(source.Columns[1], System.ComponentModel.ListSortDirection.Ascending);
            var folderRow = FindRow(source, folder);
            grid.BringCellIntoView(folderRow, 1);
            await Task.Delay(100);
            folder.IsExpanded = true;
            await WaitAsync(() => source.Rows.Count == 184);
            Check(folder.IsWatching && folder.Children.Count == 1, "Expanding did not lazily load the shared folder.");
            await File.WriteAllTextAsync(Path.Combine(folder.Path, "added.txt"), "added");
            await WaitAsync(() => source.Rows.Count == 185);
            Check(Enumerable.Range(0, source.Rows.Count).Any(i => ((FileTreeNodeModel)source.Rows[i].Model!).Name == "added.txt"),
                "A watcher notification did not update the expanded Core hierarchy.");
            grid.Scroll.ChangeView(0, 0, null, true);
            await Task.Delay(100);
            await capture(page, "files-tree");
            var file = root.Children.First(x => !x.IsDirectory);
            var fileRow = FindRow(source, file);
            grid.BringCellIntoView(fileRow, 0);
            await Task.Delay(100);
            var checkBox = ShowcaseRuntimeChecks.Descendants(grid.RowsPresenter.RealizedCells.Single(c => c.RowIndex == fileRow && c.ColumnIndex == 0)).OfType<CheckBox>().Single();
            checkBox.IsChecked = true;
            Check(file.IsChecked, "Files checkbox did not update its shared model.");
            page.ShowScenario(6);
            await Task.Delay(150);
            Check(ReferenceEquals(root, page.Files.Root), "Flat mode recreated the file models.");
            var flat = page.Files.Source!;
            Check(flat.Rows.Count == 182 && FindRow(flat, file) >= 0, "Flat mode did not share immediate file entries.");
            flat.SortBy(flat.Columns[1], System.ComponentModel.ListSortDirection.Descending);
            await Task.Delay(100);
            Check(((FileTreeNodeModel)flat.Rows[0].Model!).IsDirectory, "Descending file-name sort moved files ahead of directories.");
            await capture(page, "files-flat");
            page.ShowScenario(0);
            Check(!root.IsWatching && !folder.IsWatching && page.Files.Source is null, "Leaving Files retained directory watchers or a source.");
        }
        finally
        {
            page.ShowScenario(0);
            page.Files.Close();
            pathBox.Text = previousPath;
            fixture.Delete(recursive: true);
        }
    }
    private static void CheckFoundCountry(MainPage page, Country selected)
    {
        var row = page.FindCountry.DisplayedRow;
        Check(row >= 0 && ReferenceEquals(page.FindCountry.Source.Rows[row].Model, selected), "Find Country returned a model index instead of the displayed row.");
        Check(page.Grid.RowsPresenter.RealizedCells.Any(c => c.RowIndex == row && ReferenceEquals(c.RowModel, selected)), "Find Country did not bring the selected model into view.");
        Check(ReferenceEquals(page.FindCountry.Source.RowSelection!.SelectedItem, selected), "Find Country selected another Core model.");
    }
    private static int FindRow(ITreeDataGridSource source, object model)
    {
        for (var i = 0; i < source.Rows.Count; ++i) if (ReferenceEquals(source.Rows[i].Model, model)) return i;
        return -1;
    }
    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); ++i) await Task.Delay(25);
        Check(condition(), "Timed out waiting for native file-system state.");
        await Task.Delay(100);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
