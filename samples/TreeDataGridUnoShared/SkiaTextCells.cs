// Custom-drawn text cells built only on public APIs: a TreeDataGridElementFactory subclass
// (the same extension point as Avalonia's TreeDataGridElementFactory) creates a
// TreeDataGridTextCell subclass whose text is drawn by an Uno SKCanvasElement with SkiaSharp.
// Shared by the sample's custom cell rendering page and the native parity benchmark host.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Uno.Controls.Primitives;
#if !WINDOWS
using SkiaSharp;
using Windows.Foundation;
using Windows.Storage;
#endif

namespace TreeDataGridUnoSamples;

/// <summary>Creates <see cref="SkiaTextCell"/> wherever the default factory creates a text cell.</summary>
public sealed class SkiaTextCellElementFactory : TreeDataGridElementFactory
{
    private static readonly string TextCellKey = typeof(TreeDataGridTextCell).FullName!;
    private static readonly string SkiaCellKey = typeof(SkiaTextCell).FullName!;

    /// <summary>Whether custom drawing is available (Skia renderer targets).</summary>
    public static bool IsSupported =>
#if WINDOWS
        false;
#else
        Uno.WinUI.Graphics2DSK.SKCanvasElement.IsSupportedOnCurrentPlatform();
#endif

    protected override Control CreateElement(object? data) =>
        IsSupported && base.GetDataRecycleKey(data) == TextCellKey ? new SkiaTextCell() : base.CreateElement(data);

    protected override string GetDataRecycleKey(object? data)
    {
        var key = base.GetDataRecycleKey(data);
        return IsSupported && key == TextCellKey ? SkiaCellKey : key;
    }
}

/// <summary>
/// A text cell with a minimal template (one panel) that draws its value directly with Skia:
/// single line, character ellipsis, cell text alignment, cell padding and selection colours.
/// Editing and wrapping are not supported by this sample cell.
/// </summary>
public sealed partial class SkiaTextCell : TreeDataGridTextCell
{
    private const string TemplateXaml =
        "<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
        "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><Grid x:Name=\"Root\" /></ControlTemplate>";
    private static ControlTemplate? s_template;
    private readonly Border _selection = new() { Opacity = 0, IsHitTestVisible = false };
#if !WINDOWS
    private readonly SkiaTextLayer _layer = new();
#endif

    public SkiaTextCell()
    {
        Template = s_template ??= (ControlTemplate)XamlReader.Load(TemplateXaml);
#if !WINDOWS
        Loaded += (_, _) => SkiaCellFonts.Loaded += _layer.Refresh;
        Unloaded += (_, _) => SkiaCellFonts.Loaded -= _layer.Refresh;
#endif
    }

    /// <summary>The text this cell currently draws.</summary>
    public string RenderedText =>
#if WINDOWS
        Value ?? string.Empty;
#else
        _layer.Text;
#endif

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("Root") is not Grid root) return;
        if (_selection.Parent is null)
        {
            // As the default theme: the accent colour at 40% opacity.
            var accent = Application.Current.Resources.TryGetValue("SystemAccentColor", out var value) && value is Windows.UI.Color color
                ? color : Microsoft.UI.Colors.DodgerBlue;
            _selection.Background = new SolidColorBrush(accent) { Opacity = 0.4 };
            root.Children.Add(_selection);
        }
#if !WINDOWS
        if (_layer.Parent is null) root.Children.Add(_layer);
        _layer.Margin = Padding;
        UpdateLayer();
#endif
    }

    protected override void UpdateValue()
    {
        base.UpdateValue();
#if !WINDOWS
        UpdateLayer();
#endif
    }

    protected override void UpdateState()
    {
        base.UpdateState();
        _selection.Opacity = IsSelected ? 1 : 0;
#if !WINDOWS
        UpdateLayer();
#endif
    }

#if !WINDOWS
    private void UpdateLayer()
    {
        // The default theme keeps the cell foreground for selected cells in both themes.
        var color = Foreground is SolidColorBrush solid ? solid.Color : Microsoft.UI.Colors.Black;
        _layer.Update(Value ?? string.Empty, FontFamily?.Source, color, (float)FontSize, FontWeight.Weight >= FontWeights.SemiBold.Weight, TextAlignment);
    }
#endif
}

#if !WINDOWS
/// <summary>The default Uno font (Open Sans) for the custom cells, loaded once from the app package.</summary>
public static class SkiaCellFonts
{
    private static Task? s_loading;
    public static SKTypeface Regular { get; private set; } = SKTypeface.Default;
    public static SKTypeface Bold { get; private set; } = SKTypeface.FromFamilyName(null, SKFontStyle.Bold) ?? SKTypeface.Default;

    /// <summary>Raised on the UI thread once the package fonts replace the fallback typefaces.</summary>
    public static event Action? Loaded;

    private static readonly Dictionary<(string, bool), SKTypeface> s_families = new();

    /// <summary>The typeface for a font family name; the Uno default font for an unnamed family.</summary>
    public static SKTypeface Get(string? family, bool bold)
    {
        if (string.IsNullOrEmpty(family) || family == "XamlAutoFontFamily" || family.StartsWith("ms-appx:", StringComparison.Ordinal))
            return bold ? Bold : Regular;
        lock (s_families)
        {
            if (!s_families.TryGetValue((family, bold), out var typeface))
                s_families[(family, bold)] = typeface = SKTypeface.FromFamilyName(family, bold ? SKFontStyle.Bold : SKFontStyle.Normal)
                    ?? (bold ? Bold : Regular);
            return typeface;
        }
    }

    public static void EnsureLoading()
    {
        if (s_loading is not null) return;
        s_loading = LoadAsync();
    }

    private static async Task LoadAsync()
    {
        try
        {
            Regular = await LoadAsync("OpenSans-Regular.ttf") ?? Regular;
            Bold = await LoadAsync("OpenSans-Bold.ttf") ?? Bold;
            Loaded?.Invoke();
        }
        catch (Exception)
        {
            // Keep the fallback typefaces when the package font is not available.
        }
    }

    private static async Task<SKTypeface?> LoadAsync(string file)
    {
        var storage = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Uno.Fonts.OpenSans/Fonts/" + file));
        using var stream = await storage.OpenStreamForReadAsync();
        return SKTypeface.FromStream(stream);
    }
}

/// <summary>Draws one line of text; rendering may run off the UI thread, so it reads an immutable snapshot.</summary>
internal sealed class SkiaTextLayer : Uno.WinUI.Graphics2DSK.SKCanvasElement
{
    private const string Ellipsis = "…";
    [ThreadStatic] private static SKPaint? s_paint;
    private Snapshot _snapshot = new(string.Empty, null, Microsoft.UI.Colors.Black, 14, false, TextAlignment.Left);

    public SkiaTextLayer()
    {
        IsHitTestVisible = false;
        SkiaCellFonts.EnsureLoading();
    }

    public string Text => _snapshot.Text;

    public void Update(string text, string? family, Windows.UI.Color color, float size, bool bold, TextAlignment alignment)
    {
        var next = new Snapshot(text, family, color, size, bold, alignment);
        if (next == _snapshot) return;
        var remeasure = next.Text != _snapshot.Text || next.Family != _snapshot.Family || next.Size != _snapshot.Size || next.Bold != _snapshot.Bold;
        _snapshot = next;
        if (remeasure) InvalidateMeasure();
        Invalidate();
    }

    public void Refresh()
    {
        InvalidateMeasure();
        Invalidate();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var snapshot = _snapshot;
        var font = GetFont(snapshot);
        var metrics = font.Metrics;
        var width = snapshot.Text.Length == 0 ? 0 : font.MeasureText(snapshot.Text);
        return new Size(Math.Min(width, availableSize.Width), Math.Ceiling(metrics.Descent - metrics.Ascent));
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        var snapshot = _snapshot;
        if (snapshot.Text.Length == 0) return;
        var font = GetFont(snapshot);
        var paint = s_paint ??= new SKPaint { IsAntialias = true };
        paint.Color = new SKColor(snapshot.Color.R, snapshot.Color.G, snapshot.Color.B, snapshot.Color.A);
        var available = (float)area.Width;
        var text = snapshot.Text;
        var width = font.MeasureText(text);
        if (width > available)
        {
            // Character ellipsis: the longest prefix that fits together with "…".
            var ellipsis = font.MeasureText(Ellipsis);
            int low = 0, high = text.Length;
            while (low < high)
            {
                var mid = (low + high + 1) / 2;
                if (font.MeasureText(text.AsSpan(0, mid)) + ellipsis <= available) low = mid; else high = mid - 1;
            }
            text = text[..low].TrimEnd() + Ellipsis;
            width = font.MeasureText(text);
        }
        var x = snapshot.Alignment switch
        {
            TextAlignment.Center => Math.Max(0, (available - width) / 2),
            TextAlignment.Right => Math.Max(0, available - width),
            _ => 0f,
        };
        var metrics = font.Metrics;
        var y = ((float)area.Height - (metrics.Descent - metrics.Ascent)) / 2 - metrics.Ascent;
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
    }

    // Fonts are created once per typeface and size; one cache per thread (layout and rendering).
    [ThreadStatic] private static Dictionary<(SKTypeface, float), SKFont>? s_fonts;

    private static SKFont GetFont(Snapshot snapshot)
    {
        var key = (SkiaCellFonts.Get(snapshot.Family, snapshot.Bold), snapshot.Size);
        var fonts = s_fonts ??= new();
        if (!fonts.TryGetValue(key, out var font))
            fonts[key] = font = new SKFont(key.Item1, key.Item2) { Edging = SKFontEdging.SubpixelAntialias, Subpixel = true };
        return font;
    }

    private readonly record struct Snapshot(string Text, string? Family, Windows.UI.Color Color, float Size, bool Bold, TextAlignment Alignment);
}
#endif
