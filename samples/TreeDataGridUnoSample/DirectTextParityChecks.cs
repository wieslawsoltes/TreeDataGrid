using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Uno.Controls.Primitives;
using Windows.UI.Text;

namespace TreeDataGridUnoSample;

/// <summary>
/// Compares direct Skia cell text with the native text block it replaces: two default text
/// cells, one of which is forced onto its <c>PART_Text</c> block (text selection makes a text
/// block ineligible for direct rendering without changing unfocused pixels), must have the
/// same size and exactly the same pixels.
/// </summary>
internal static class DirectTextParityChecks
{
    private const string PresenterType = "TreeDataGridTextPresenter";

    internal static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        var direct = new TreeDataGridTextCell { Height = 32 };
        var native = new TreeDataGridTextCell { Height = 32 };
        var host = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        host.Children.Add(direct);
        host.Children.Add(native);
        var root = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        root.Children.Add(host);
        try
        {
            page.Content = root;
            await Task.Delay(100);
            direct.ApplyTemplate();
            native.ApplyTemplate();
            Find<TextBlock>(native, "PART_Text").IsTextSelectionEnabled = true;
            var translucent = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(179, 226, 244, 177));
            string[] texts =
            {
                "Alpha measuring text", "Zoë Ångström façade", "1,234.56 kg", "  spaced  out", "trailing   ",
                "", "W", "AVAYA To Wa fi ffl", "Łódź żółw", "x—y",
            };
            var compared = 0;
            var directStates = 0;
            foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark })
            foreach (var text in texts)
            foreach (var width in new[] { 280d, 96, 61, 23 })
            foreach (var alignment in new[] { TextAlignment.Left, TextAlignment.Center, TextAlignment.Right })
            {
                var variant = compared % 5;
                root.RequestedTheme = theme;
                foreach (var cell in new[] { direct, native })
                {
                    cell.Width = width;
                    cell.Value = text;
                    cell.TextAlignment = alignment;
                    cell.TextTrimming = variant == 4 ? TextTrimming.None : TextTrimming.CharacterEllipsis;
                    cell.FontSize = variant switch { 1 => 12, 2 => 17.5, 3 => 23, _ => 14 };
                    cell.FontWeight = variant == 2 ? FontWeights.Bold : FontWeights.Normal;
                    cell.FontStyle = variant == 3 ? FontStyle.Italic : FontStyle.Normal;
                    cell.Padding = variant == 1 ? new Thickness(3, 1, 5, 2) : new Thickness(4, 2, 4, 2);
                    if (variant == 2) cell.Foreground = translucent; else cell.ClearValue(Control.ForegroundProperty);
                    cell.IsSelected = variant == 3;
                }
                root.UpdateLayout();
                await Task.Delay(5);
                var usesDirect = Displayed(direct);
                Check(!Displayed(native), "The reference cell did not render with its text block.");
                var nativeText = Find<TextBlock>(native, "PART_Text");
                Check(nativeText.Visibility == Visibility.Visible, "The reference text block is hidden.");
                if (usesDirect) ++directStates;
                Check(direct.DesiredSize == native.DesiredSize && direct.ActualWidth == native.ActualWidth &&
                    direct.ActualHeight == native.ActualHeight,
                    $"Direct text changed the cell size: text='{text}', width={width}, alignment={alignment}, variant={variant}.");
#if !__WASM__
                var expected = await CaptureAsync(native);
                var actual = await CaptureAsync(direct);
                Check(expected.Width == actual.Width && expected.Height == actual.Height && expected.Width > 0,
                    "Direct text raster dimensions differ.");
                if (!expected.Pixels.AsSpan().SequenceEqual(actual.Pixels))
                {
                    if (Environment.GetEnvironmentVariable("TDG_DIRECT_TEXT_DUMP") is { Length: > 0 } dump)
                    {
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dump, $"expected-{expected.Width}x{expected.Height}.bgra"), expected.Pixels);
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dump, $"actual-{actual.Width}x{actual.Height}.bgra"), actual.Pixels);
                    }
                    var differing = 0;
                    for (var i = 0; i < expected.Pixels.Length; ++i) if (expected.Pixels[i] != actual.Pixels[i]) ++differing;
                    throw new InvalidOperationException(
                        $"Direct text pixels differ: theme={theme}, text='{text}', width={width}, alignment={alignment}, " +
                        $"variant={variant}, direct={usesDirect}, differingBytes={differing}.");
                }
#endif
                ++compared;
            }
            Check(directStates > compared / 2, $"Direct text rendering was not used: {directStates}/{compared}.");
            Console.WriteLine($"UNO_RUNTIME_DIRECT_TEXT_PARITY_PASSED: states={compared}; directStates={directStates}; " +
                "exact pixels and sizes against the native text block: themes, fonts, weights, italics, trimming widths, alignments, " +
                "padding, translucent and selected foregrounds, empty, whitespace and fallback text");
        }
        finally { page.Content = previous; }
    }

    private static bool Displayed(TreeDataGridCell cell) =>
        ShowcaseRuntimeChecks.Descendants(cell).OfType<FrameworkElement>()
            .Any(element => element.GetType().Name == PresenterType && element.Visibility == Visibility.Visible);

    private static T Find<T>(DependencyObject owner, string name) where T : FrameworkElement =>
        ShowcaseRuntimeChecks.Descendants(owner).OfType<T>().Single(element => element.Name == name);

#if !__WASM__
    private static async Task<(int Width, int Height, byte[] Pixels)> CaptureAsync(UIElement element)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element);
        return (bitmap.PixelWidth, bitmap.PixelHeight, (await bitmap.GetPixelsAsync()).ToArray());
    }
#endif

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
