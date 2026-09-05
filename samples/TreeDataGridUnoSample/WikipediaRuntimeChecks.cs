using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TreeDataGridDemo.Models;
using Windows.Storage;

namespace TreeDataGridUnoSample;

internal static class WikipediaRuntimeChecks
{
    public static async Task RunAsync(MainPage page, Func<UIElement, string, Task> capture)
    {
        page.ShowScenario(4);
        await Task.Delay(300);
        var grid = page.Grid;
        var source = page.Wikipedia.Source;
        Check(ReferenceEquals(grid.Presentation!.Rows, source.Rows), "Wikipedia copied the Core rows.");
        Check(source.Rows.Count == 240, "Wikipedia offline fixture is not populated.");
        Check(grid.RowsPresenter.RealizedCells.Count < 100, "Wikipedia realized all rows.");
        Check(grid.RowsPresenter.RealizedCells.Select(c => grid.RowsPresenter.GetRowHeight(c.RowIndex)).Distinct().Count() > 1,
            "Wikipedia extracts did not produce measured wrapping heights.");
        var items = source.Items.ToArray();
        Check(items.Count(x => x.HasCreatedImage) < 40 && !items[200].HasCreatedImage,
            "Wikipedia eagerly created images for unrealized rows.");
        VerifyContent(page);
        var firstImage = ShowcaseRuntimeChecks.Descendants(grid.RowsPresenter.RealizedCells.Single(c => c.RowIndex == 0 && c.ColumnIndex == 0)).OfType<Image>().Single();
        for (var i = 0; i < 30 && (firstImage.Source as BitmapImage)?.PixelWidth is not > 0; ++i) await Task.Delay(50);
        Check((firstImage.Source as BitmapImage)?.PixelWidth > 0, "Wikipedia packaged image did not decode.");
        var parent = VisualTreeHelper.GetParent(firstImage);
        var retainedCell = grid.RowsPresenter.RealizedCells.Single(c => c.RowIndex == 0 && c.ColumnIndex == 0);
        var loads = 0;
        var unloads = 0;
        RoutedEventHandler loaded = (_, _) => ++loads;
        RoutedEventHandler unloaded = (_, _) => ++unloads;
        retainedCell.Loaded += loaded;
        retainedCell.Unloaded += unloaded;
        try
        {
            await capture(page, "wikipedia");
            Check(grid.BringCellIntoView(200, 2), "Wikipedia bring-into-view failed.");
            await Task.Delay(200);
            Check(grid.RowsPresenter.RealizedCells.Any(c => c.RowIndex == 200), "Wikipedia scroll target was not realized.");
            VerifyContent(page);
            Check(loads == 0 && unloads == 0 && ReferenceEquals(parent, VisualTreeHelper.GetParent(firstImage)),
                "Wikipedia scrolling detached retained cells or image templates.");
            Check(grid.RowsPresenter.RealizedCells.Count < 100, "Wikipedia scrolling lost virtualization.");
            source.SortBy(source.Columns[1], System.ComponentModel.ListSortDirection.Descending);
            grid.Scroll.ChangeView(0, 0, null, true);
            await Task.Delay(200);
            VerifyContent(page);
            Check(((OnThisDayArticle)source.Rows[0].Model!).Titles!.Normalized == "Offline article 240", "Wikipedia title sorting failed.");
            page.Wikipedia.ShowOffline();
            await Task.Delay(200);
            VerifyContent(page);
            if (Environment.GetCommandLineArgs().Contains("--wikipedia-live"))
            {
                await page.Wikipedia.ReloadAsync();
                await Task.Delay(300);
                VerifyContent(page);
                Console.WriteLine($"UNO_WIKIPEDIA_LIVE_RESULT: {page.Wikipedia.Status}");
                var articles = grid.RowsPresenter.RealizedCells.Select(x => x.RowModel).OfType<OnThisDayArticle>()
                    .Where(x => x.Thumbnail?.Source?.StartsWith("https:", StringComparison.Ordinal) == true).Distinct().ToArray();
                await Task.WhenAll(articles.Select(x => x.ImageLoadingTask));
                var decoded = articles.Count(x => x.Image?.PixelWidth > 0);
                Console.WriteLine($"UNO_WIKIPEDIA_LIVE_IMAGES: {decoded}/{articles.Length} decoded");
                foreach (var article in articles.Where(x => x.ImageLoadError is not null))
                    Console.WriteLine($"UNO_WIKIPEDIA_LIVE_IMAGE_ERROR: {article.ImageLoadError}");
                Check(articles.Length == 0 || decoded > 0, "None of the live Wikipedia thumbnails decoded.");
                VerifyContent(page);
                await capture(page, "wikipedia-live");
            }
        }
        finally
        {
            retainedCell.Loaded -= loaded;
            retainedCell.Unloaded -= unloaded;
            page.ShowScenario(0);
        }
        await Task.Delay(150);
        await VerifyDelayedImageAsync(page);
        Console.WriteLine("UNO_RUNTIME_WIKIPEDIA_PASSED: shared DTO/Core rows, wrapping, lazy native images, package asset decode, scroll/recycle identity, sort, source reload, delayed remote image/header regression");
    }

    private static async Task VerifyDelayedImageAsync(MainPage page)
    {
        var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/file.png"));
        using var stream = await file.OpenStreamForReadAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requested = false;
        using var client = WikipediaViewModel.CreateClient(new ImageHandler(request =>
        {
            Check(request.Headers.UserAgent.ToString().Contains("TreeDataGridUnoSample/1.0", StringComparison.Ordinal),
                "Remote Wikipedia image request omitted its User-Agent.");
            requested = true;
            return response.Task;
        }));
        var article = new OnThisDayArticle
        {
            Titles = new() { Normalized = "Pending image" },
            Thumbnail = new() { Source = "https://example.invalid/deferred.png" },
            ImageHttpClient = client,
        };
        var replacement = new OnThisDayArticle { Titles = new() { Normalized = "No image" } };
        var items = new ObservableCollection<OnThisDayArticle> { article };
        using var model = new WikipediaViewModel();
        model.Source.Items = items;
        var grid = page.Grid;
        grid.Model = model.Source;
        try
        {
            await Task.Delay(150);
            Check(requested, "Realizing a remote image did not start its lazy download.");
            var image = ShowcaseRuntimeChecks.Descendants(grid.RowsPresenter.RealizedCells.Single(c => c.ColumnIndex == 0)).OfType<Image>().Single();
            var parent = VisualTreeHelper.GetParent(image);
            var oldImage = article.Image;
            items[0] = replacement;
            await Task.Delay(100);
            Check(image.Source is null, "A missing image did not clear the recycled Image source.");
            response.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(buffer.ToArray()) });
            await article.ImageLoadingTask;
            await Task.Delay(100);
            Check(oldImage?.PixelWidth > 0 && article.ImageLoadError is null, "The delayed identified image download did not decode.");
            Check(image.Source is null && ReferenceEquals(image.DataContext, replacement) && ReferenceEquals(parent, VisualTreeHelper.GetParent(image)),
                "An old image completion overwrote or detached a recycled cell's image.");
        }
        finally
        {
            response.TrySetCanceled();
            grid.Model = null;
            page.ShowScenario(0);
        }
        await Task.Delay(100);
    }

    private sealed class ImageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }

    private static void VerifyContent(MainPage page)
    {
        foreach (var cell in page.Grid.RowsPresenter.RealizedCells)
        {
            var article = (OnThisDayArticle)page.Wikipedia.Source.Rows[cell.RowIndex].Model!;
            Check(ReferenceEquals(cell.RowModel, article), "Wikipedia retained a previous article model.");
            var descendants = ShowcaseRuntimeChecks.Descendants(cell).ToArray();
            if (cell.ColumnIndex == 0)
                Check(ReferenceEquals(descendants.OfType<Image>().Single().Source, article.Image), "Recycled Wikipedia image belongs to another article.");
            else
                Check(descendants.OfType<TextBlock>().Any(t => t.Text == (cell.ColumnIndex == 1 ? article.Titles?.Normalized : article.Extract)),
                    "A recycled Wikipedia template retained stale text.");
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
