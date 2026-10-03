using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.Models;
using Avalonia.Media.Imaging;

namespace TreeDataGridDemo.Models;

internal partial class OnThisDayArticle : NotifyingBase
{
    private bool _loadedImage;
    private Bitmap? _image;

    public Bitmap? Image
    {
        get
        {
            if (_image is null && !_loadedImage) _ = LoadImageAsync();
            return _image;
        }
        private set => RaiseAndSetIfChanged(ref _image, value);
    }

    private async Task LoadImageAsync()
    {
        _loadedImage = true;
        if (Thumbnail?.Source is null) return;
        try
        {
            var bytes = await WikipediaHttpClient.Shared.GetByteArrayAsync(Thumbnail.Source);
            using var stream = new MemoryStream(bytes);
            Image = new Bitmap(stream);
        }
        catch { }
    }
}
