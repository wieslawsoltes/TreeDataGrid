using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TreeDataGridUnoSample;

namespace TreeDataGridDemo.Models;

internal partial class OnThisDayArticle
{
    private static readonly HttpClient ImageClient = WikipediaViewModel.CreateClient();
    private BitmapImage? _image;

    // Native image loading is lazy: only realized template rows access Image.
    // Each model owns its image identity, so a recycled Image cannot receive
    // another row's delayed download through a cell-owned completion callback.
    [JsonIgnore]
    public BitmapImage? Image
    {
        get
        {
            if (_image is null && Uri.TryCreate(Thumbnail?.Source, UriKind.Absolute, out var uri))
            {
                _image = new BitmapImage();
                if (uri.Scheme is "https" or "http") ImageLoadingTask = LoadRemoteImageAsync(_image, uri);
                else if (uri.Scheme == "ms-appx") _image.UriSource = uri;
            }
            return _image;
        }
    }

    [JsonIgnore]
    internal bool HasCreatedImage => _image is not null;

    // Completion includes the native decode outcome, not just HTTP/stream
    // delivery. A native failure is recorded in ImageLoadError for the view.
    internal Task ImageLoadingTask { get; private set; } = Task.CompletedTask;
    internal string? ImageLoadError { get; private set; }
    internal HttpClient? ImageHttpClient { get; set; }

    private async Task LoadRemoteImageAsync(BitmapImage image, Uri uri)
    {
        try
        {
            var bytes = await (ImageHttpClient ?? ImageClient).GetByteArrayAsync(uri);
            using var stream = new MemoryStream(bytes, writable: false);
            using var randomAccess = stream.AsRandomAccessStream();
            var decoded = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            ImageBrush? decodeConsumer = null;
            void Opened(object sender, RoutedEventArgs args) => decoded.TrySetResult(null);
            void Failed(object sender, ExceptionRoutedEventArgs args) => decoded.TrySetResult(args.ErrorMessage);
            image.ImageOpened += Opened;
            image.ImageFailed += Failed;
            try
            {
#if __SKIA__
                // Uno's ForceLoad subscribes (which may start a decode), then
                // invalidates immediately (starting another and cancelling the
                // first). A cancelled decode can overwrite successful dimensions.
                // Set the stream once, then keep a public, non-visual consumer
                // until decoding finishes. Existing template consumers reuse the
                // same operation; a recycled-away bitmap still decodes exactly once.
                image.SetSource(randomAccess);
                decodeConsumer = new ImageBrush { ImageSource = image };
#else
                await image.SetSourceAsync(randomAccess);
#endif
                var error = await decoded.Task.WaitAsync(TimeSpan.FromSeconds(15));
                if (error is not null)
                    throw new InvalidDataException($"Image decode failed for '{uri}': {error}");
                if (image.PixelWidth <= 0 || image.PixelHeight <= 0)
                    throw new InvalidDataException($"Image decode for '{uri}' produced no pixels.");
            }
            finally
            {
                decodeConsumer?.ClearValue(ImageBrush.ImageSourceProperty);
                image.ImageOpened -= Opened;
                image.ImageFailed -= Failed;
            }
        }
        catch (Exception error) { ImageLoadError = error.ToString(); }
    }
}
