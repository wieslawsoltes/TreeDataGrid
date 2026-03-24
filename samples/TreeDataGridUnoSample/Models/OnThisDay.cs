using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Avalonia.Controls.Models;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace TreeDataGridDemo.Models;

internal class OnThisDay
{
    public OnThisDayEvent[]? Selected { get; set; }
}

internal class OnThisDayEvent
{
    public string? Text { get; set; }
    public int Year { get; set; }
    public OnThisDayArticle[]? Pages { get; set; }
}

internal class OnThisDayArticle : NotifyingBase
{
    private const string UserAgent = "AvaloniaTreeDataGridUnoSample/1.0 (https://avaloniaui.net; team@avaloniaui.net)";
    private bool _loadedImage;
    private ImageSource? _image;

    public string? Type { get; set; }
    public OnThisDayTitles? Titles { get; set; }
    public OnThisDayImage? Thumbnail { get; set; }
    public string? Description { get; set; }
    public string? Extract { get; set; }

    public ImageSource? Image
    {
        get
        {
            if (_image is null && !_loadedImage)
                _ = LoadImageAsync();

            return _image;
        }
        private set => RaiseAndSetIfChanged(ref _image, value);
    }

    private async Task LoadImageAsync()
    {
        _loadedImage = true;

        if (Thumbnail?.Source is null)
            return;

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

            var bytes = await client.GetByteArrayAsync(Thumbnail.Source);
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            Image = bitmap;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"Wikipedia image request failed: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            Debug.WriteLine($"Wikipedia image request timed out: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            Debug.WriteLine($"Wikipedia image was invalid: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"Wikipedia image could not be decoded: {ex.Message}");
        }
    }
}

internal class OnThisDayTitles
{
    public string? Normalized { get; set; }
}

internal class OnThisDayImage
{
    public string? Source { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
