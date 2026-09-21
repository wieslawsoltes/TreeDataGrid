using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using TreeDataGridCore.Selection;
using TreeDataGridUnoActivityMonitor.Models;
using TreeDataGridUnoActivityMonitor.Services;
using TreeDataGridUnoActivityMonitor.ViewModels;

namespace TreeDataGridUnoActivityMonitor;

internal static class ActivityMonitorRuntimeChecks
{
    public static async Task RunAsync(MainPage page)
    {
        await VerifyLifetimeAsync();
        for (var i = 0; i < 16; ++i) await RefreshAsync(page.ViewModel);
        foreach (var kind in Enum.GetValues<MetricKind>())
        {
            page.ViewModel.SelectMetric(kind);
            await SettleAsync(page);
            var section = page.ViewModel.CurrentSection;
            var source = section.Source;
            Check(ReferenceEquals(page.Table.Model, source), "The table does not use the shared Core source.");
            Check(source.Rows.Count > 1 && source.Columns.Count == 5, $"{kind} has no complete table.");
            Check(section.TrendSeries.Samples.Length >= 16, "The chart did not receive refreshes.");
            var selection = (ITreeDataGridRowSelectionModel)source.Selection!;
            page.Table.SelectCell(1, 0);
            var selected = (MonitorRowBase)selection.SelectedItem!;
            Check(section.InspectorTitle == selected.Name, "Selection did not update the inspector.");
            Check(source.SortBy(source.Columns[1], ListSortDirection.Descending), "Numeric sort is missing.");
            await SettleAsync(page);
            var identity = page.Table.RowsPresenter!.RealizedCells.First(x => x.ColumnIndex == 0);
            var parent = VisualTreeHelper.GetParent(identity);
            var text = Descendants(identity).OfType<TextBlock>().First(x => x.Text == ((MonitorRowBase)identity.RowModel!).Name);
            var textParent = VisualTreeHelper.GetParent(text);
            var loads = 0;
            var unloads = 0;
            RoutedEventHandler loaded = (_, _) => ++loads;
            RoutedEventHandler unloaded = (_, _) => ++unloads;
            identity.Loaded += loaded;
            identity.Unloaded += unloaded;
            await RefreshAsync(page.ViewModel);
            await SettleAsync(page);
            identity.Loaded -= loaded;
            identity.Unloaded -= unloaded;
            Check(loads == 0 && unloads == 0 && ReferenceEquals(parent, VisualTreeHelper.GetParent(identity)),
                "Snapshot replacement detached/re-attached a retained native cell.");
            Check(ReferenceEquals(textParent, VisualTreeHelper.GetParent(text)), "Snapshot replacement discarded template content.");
            Check(text.Text == ((MonitorRowBase)identity.RowModel!).Name, "Retained identity template did not bind the new row.");
            foreach (var cell in page.Table.RowsPresenter!.RealizedCells)
                Check(ReferenceEquals(cell.RowModel, source.Rows[cell.RowIndex].Model), "Snapshot retained an obsolete row model.");
            var remainsVisible = source.Rows.Any(x => ((MonitorRowBase)x.Model!).StableId == selected.StableId);
            if (remainsVisible)
                Check(((MonitorRowBase)selection.SelectedItem!).StableId == selected.StableId,
                    "A sorted telemetry refresh lost stable-key selection.");
            else
            {
                // Live processes can exit or leave the top-280 snapshot between
                // captures. That is different from losing an existing row key.
                Check(page.ViewModel.ModeLabel == "Live native metrics", "A deterministic demo row disappeared.");
                Check(selection.SelectedItem is MonitorRowBase fallback &&
                    source.Rows.Any(x => ReferenceEquals(x.Model, fallback)) && section.InspectorTitle == fallback.Name,
                    "An expired process did not produce a valid fallback selection.");
                Console.WriteLine($"UNO_ACTIVITY_PROCESS_EXPIRED: {kind}");
            }
            Check(!ReferenceEquals(selected, selection.SelectedItem), "Telemetry did not replace row objects.");
            selected = (MonitorRowBase)selection.SelectedItem!;
            var numeric = page.Table.RowsPresenter!.RealizedCells.First(x => x.ColumnIndex == 1);
            Check(Descendants(numeric).OfType<TextBlock>().Any(x => x.TextAlignment == TextAlignment.Right && x.Text.Length > 0),
                "Numeric text was not right-aligned by the Uno presentation.");
            await CaptureAsync(page, "activity-" + kind.ToString().ToLowerInvariant());

            var count = source.Rows.Count;
            section.SearchText = "___no_matching_process___";
            await SettleAsync(page);
            Check(source.Rows.Count == 0 && selection.SelectedItem is null, "The empty filter retained a selection.");
            section.SearchText = selected.Name;
            await SettleAsync(page);
            Check(source.Rows.Count > 0 && source.Rows.Count <= count, "The search filter did not restore matching rows.");
            Check(source.Rows.All(x => ((MonitorRowBase)x.Model!).SearchText.Contains(selected.Name, StringComparison.OrdinalIgnoreCase)),
                "The filter retained unrelated rows.");
            section.SearchText = string.Empty;
            await SettleAsync(page);
            Check(source.Rows.Count == count, "Clearing the filter lost rows.");
            page.Table.BringCellIntoView(count - 1, 0);
            await SettleAsync(page);
            Check(page.Table.RowsPresenter!.RealizedCells.Any(x => x.RowIndex == count - 1), "Last process cannot be brought into view.");
            Check(page.Table.RowsPresenter!.RealizedCells.Count < 150, "Activity Monitor realized an unbounded table.");
            page.Table.Scroll!.ChangeView(0, 0, null, true);
            Console.WriteLine($"UNO_ACTIVITY_SECTION_PASSED: {kind}, rows={count}, samples={section.TrendSeries.Samples.Length}");
        }
        page.Stop();
        page.Stop();
        Check(page.Table.Model is null && page.ViewModel.IsDisposed, "Activity Monitor did not release its grid/model.");
    }

    private static async Task VerifyLifetimeAsync()
    {
        var provider = new DelayedProvider();
        var shell = new MonitorShellViewModel(provider);
        var refresh = shell.RefreshAsync();
        await shell.RefreshAsync();
        Check(provider.Calls == 1, "Refreshes overlapped.");
        shell.Dispose();
        shell.Dispose();
        Check(provider.Token.IsCancellationRequested && provider.Disposals == 0, "Provider was disposed during capture.");
        using var demo = new DemoTelemetryProvider();
        provider.Completion.SetResult(await demo.CaptureAsync(CancellationToken.None));
        await refresh;
        Check(provider.Disposals == 1 && shell.CpuSection.Source.Rows.Count == 0 && shell.HostName == "Waiting",
            "A late capture updated a disposed shell or disposal was repeated.");
        await shell.RefreshAsync();
        Check(provider.Calls == 1, "Disposed shell captured again.");

        using var failing = new MonitorShellViewModel(new FailingProvider());
        await failing.RefreshAsync();
        Check(failing.RefreshError is InvalidOperationException && failing.TimestampDetail.Contains("Refresh failed"),
            "Provider failure was not reported.");
        await failing.RefreshAsync();
        Check(failing.RefreshError is null && failing.CpuSection.Source.Rows.Count == 20, "Refresh did not recover after failure.");
        Console.WriteLine("UNO_ACTIVITY_LIFETIME_PASSED");
    }

    private static async Task RefreshAsync(MonitorShellViewModel model)
    {
        await model.RefreshAsync();
        if (model.RefreshError is { } error) throw new InvalidOperationException("Telemetry capture failed.", error);
    }
    private static async Task SettleAsync(MainPage page) { page.UpdateLayout(); await Task.Delay(100); }
    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); ++i)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static async Task CaptureAsync(UIElement element, string name)
    {
        var args = TreeDataGridUnoSamples.SampleRunContext.Arguments;
        var index = Array.IndexOf(args, "--screenshot-dir");
        if (index < 0 || index + 1 >= args.Length) return;
        var directory = Path.GetFullPath(args[index + 1]);
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element);
        using var image = SKImage.FromPixelCopy(new SKImageInfo(bitmap.PixelWidth, bitmap.PixelHeight,
            SKColorType.Bgra8888, SKAlphaType.Premul), (await bitmap.GetPixelsAsync()).ToArray());
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var path = Path.Combine(directory, name + ".png");
        File.WriteAllBytes(path, png.ToArray());
        Console.WriteLine($"UNO_ACTIVITY_SCREENSHOT: {path} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class DelayedProvider : IMonitorTelemetryProvider
    {
        public bool IsDemoMode => true;
        public TaskCompletionSource<MonitorSnapshot> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public int Disposals { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<MonitorSnapshot> CaptureAsync(CancellationToken token) { ++Calls; Token = token; return Completion.Task; }
        public void Dispose() => ++Disposals;
    }
    private sealed class FailingProvider : IMonitorTelemetryProvider
    {
        private readonly DemoTelemetryProvider _demo = new();
        private bool _failed;
        public bool IsDemoMode => true;
        public Task<MonitorSnapshot> CaptureAsync(CancellationToken token)
        {
            if (!_failed) { _failed = true; throw new InvalidOperationException("Injected provider failure"); }
            return _demo.CaptureAsync(token);
        }
        public void Dispose() => _demo.Dispose();
    }
}
