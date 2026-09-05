using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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

    internal Task ImageLoadingTask { get; private set; } = Task.CompletedTask;
    internal string? ImageLoadError { get; private set; }
    internal HttpClient? ImageHttpClient { get; set; }

    private async Task LoadRemoteImageAsync(BitmapImage image, Uri uri)
    {
        try
        {
            // Wikimedia rejects the default native downloader's empty User-Agent.
            // Download with the sample's identified client, then decode on the UI
            // context. The model-specific BitmapImage remains stable across await.
            var bytes = await (ImageHttpClient ?? ImageClient).GetByteArrayAsync(uri);
            using var stream = new MemoryStream(bytes);
            using var randomAccess = stream.AsRandomAccessStream();
            await image.SetSourceAsync(randomAccess);
        }
        catch (Exception error) { ImageLoadError = error.Message; }
    }
}
