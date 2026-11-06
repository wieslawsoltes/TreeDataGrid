#if WINDOWS
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using Windows.Foundation;

namespace TreeDataGridUnoActivityMonitor.Controls;

/// <summary>WinUI composition bridge for the same chart drawing code used by Uno Skia.</summary>
public abstract class MetricChartSurface : Grid
{
    private readonly Image _image = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private WriteableBitmap? _bitmap;
    private SKBitmap? _pixels;
    private byte[]? _copy;
    private XamlRoot? _observedRoot;
    protected MetricChartSurface()
    {
        // Unlike SKCanvasElement's native hit-test override, an empty WinUI Grid
        // needs a transparent background to receive chart-hover input.
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        Children.Add(_image);
        SizeChanged += (_, _) => Invalidate();
        Loaded += (_, _) =>
        {
            if (_observedRoot is not null) _observedRoot.Changed -= OnRootChanged;
            _observedRoot = XamlRoot;
            if (_observedRoot is not null) _observedRoot.Changed += OnRootChanged;
            Invalidate();
        };
        Unloaded += (_, _) =>
        {
            if (_observedRoot is not null) _observedRoot.Changed -= OnRootChanged;
            _observedRoot = null;
            _image.Source = null;
            _pixels?.Dispose();
            _pixels = null;
            _bitmap = null;
            _copy = null;
        };
    }
    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => Invalidate();
    public void Invalidate()
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0) return;
        var scale = XamlRoot?.RasterizationScale ?? 1;
        var width = Math.Max(1, checked((int)Math.Ceiling(ActualWidth * scale)));
        var height = Math.Max(1, checked((int)Math.Ceiling(ActualHeight * scale)));
        _image.Width = ActualWidth;
        _image.Height = ActualHeight;
        if (_pixels is null || _pixels.Width != width || _pixels.Height != height)
        {
            _pixels?.Dispose();
            _pixels = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            _bitmap = new WriteableBitmap(width, height);
            _copy = new byte[checked(width * height * 4)];
            _image.Source = _bitmap;
        }
        using (var canvas = new SKCanvas(_pixels))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale((float)scale);
            RenderOverride(canvas, new(ActualWidth, ActualHeight));
        }
        System.Runtime.InteropServices.Marshal.Copy(_pixels.GetPixels(), _copy!, 0, _copy!.Length);
        using (var stream = _bitmap!.PixelBuffer.AsStream()) stream.Write(_copy, 0, _copy.Length);
        _bitmap.Invalidate();
    }
    protected abstract void RenderOverride(SKCanvas canvas, Size area);
}
#else
namespace TreeDataGridUnoActivityMonitor.Controls;

/// <summary>Desktop/browser Skia use the native zero-copy composition surface.</summary>
public abstract class MetricChartSurface : global::Uno.WinUI.Graphics2DSK.SKCanvasElement { }
#endif
