using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TreeDataGridDemo.Models;
using Windows.Storage;

namespace TreeDataGridUnoSample;

/// <summary>Image completion means decoded pixels or a reported failure, without timing sleeps.</summary>
internal static class WikipediaImageCompletionRuntimeChecks
{
    public static async Task RunAsync()
    {
        var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/file.png"));
        using var stream = await file.OpenStreamForReadAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        var requests = 0;
        using var client = WikipediaViewModel.CreateClient(new ImageHandler(request =>
        {
            ++requests;
            Check(request.Headers.UserAgent.ToString().Contains("TreeDataGridUnoSample/1.0", StringComparison.Ordinal),
                "The image completion fixture lost the identified HTTP client.");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(request.RequestUri!.AbsolutePath == "/invalid.png" ? [1, 2, 3, 4] : bytes),
            };
        }));
        var articles = Enumerable.Range(0, 16).Select(index => new OnThisDayArticle
        {
            Titles = new() { Normalized = $"Decode {index}" },
            Thumbnail = new() { Source = $"https://example.invalid/{index}.png" },
            ImageHttpClient = client,
        }).ToArray();
        Check(requests == 0 && articles.All(article => !article.HasCreatedImage), "Images were not lazy.");
        var images = articles.Select(article => article.Image!).ToArray();
        // No Image controls consume these bitmaps. SetSourceAsync and our task
        // must provide a useful completion contract after row recycling too.
        await Task.WhenAll(articles.Select(article => article.ImageLoadingTask)).WaitAsync(TimeSpan.FromSeconds(30));
        for (var index = 0; index < articles.Length; ++index)
        {
            var article = articles[index];
            var image = images[index];
            Check(article.ImageLoadError is null && image.PixelWidth > 0 && image.PixelHeight > 0,
                $"ImageLoadingTask completed without decoded pixels: item={index}, size={image.PixelWidth}x{image.PixelHeight}, error={article.ImageLoadError}.");
            Check(ReferenceEquals(image, article.Image), "A completion changed model-specific image identity.");
            Check(images.Take(index).All(other => !ReferenceEquals(image, other)), "Different models shared a mutable image identity.");
        }
        Check(requests == articles.Length, "Reading an already loaded image downloaded it again.");

        var invalid = new OnThisDayArticle
        {
            Thumbnail = new() { Source = "https://example.invalid/invalid.png" },
            ImageHttpClient = client,
        };
        var invalidImage = invalid.Image!;
        await invalid.ImageLoadingTask.WaitAsync(TimeSpan.FromSeconds(30));
        Check(invalid.ImageLoadError is { Length: > 0 } &&
            !invalid.ImageLoadError.Contains(nameof(TimeoutException), StringComparison.Ordinal),
            $"Invalid image data did not report its actual decoder failure: {invalid.ImageLoadError}.");
        Check(invalidImage.PixelWidth == 0 && invalidImage.PixelHeight == 0, "Invalid image bytes produced pixels.");
        Check(requests == articles.Length + 1, "Invalid image completion silently retried a failed download.");
        Console.WriteLine("UNO_RUNTIME_IMAGE_COMPLETION_PASSED: lazy requests, sixteen concurrent native decodes, stable independent identities, immediate post-task pixels and invalid-payload failure");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ImageHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(send(request));
        }
    }
}
