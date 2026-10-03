using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;

namespace TreeDataGridUnoSample;

/// <summary>Compare active native states with the pinned Border/Grid reference layout.</summary>
public sealed partial class TextTemplateParityView : UserControl
{
    public TextTemplateParityView() => InitializeComponent();

    internal static async Task RunAsync(MainPage page)
    {
        await NativeTextAssignmentRuntimeChecks.RunAsync(page);
        var previous = page.Content;
        var view = new TextTemplateParityView();
        try
        {
            page.Content = view;
            await Task.Delay(100);
            view.UpdateLayout();
            await view.VerifyAsync();
        }
        finally { page.Content = previous; }
    }

    private async Task VerifyAsync()
    {
        const string text = "Zażółć gęślą jaźń — 日本語 🌲 and a longer wrapping line";
        var background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(180, 25, 61, 93));
        var border = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(170, 129, 173, 42));
        var foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(230, 188, 221, 241));
        Reference.ApplyTemplate();
        Candidate.ApplyTemplate();
        // The flat Grid candidate changed two-stage border/alignment rounding.
        // Keep the native Border/Grid contract rather than masking a pixel shift
        // with a tolerance or a per-example positioning correction.
        Check(Find<FrameworkElement>(Reference, "CellBorder") is Border &&
            Find<FrameworkElement>(Candidate, "CellBorder") is Border,
            "The retained template does not preserve the reference border layout.");
        var comparedStates = 0;
        var pixelStates = 0;
        foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark })
        foreach (var thick in new[] { false, true })
        foreach (var state in new[] { 0, 1, 2, 3 })
        {
            RequestedTheme = theme;
            foreach (var cell in new[] { Reference, Candidate })
            {
                cell.Background = background;
                cell.BorderBrush = border;
                cell.Foreground = foreground;
                cell.BorderThickness = thick ? new Thickness(3, 5, 4, 7) : new Thickness(0);
                cell.CornerRadius = thick ? new CornerRadius(9, 3, 7, 5) : new CornerRadius(0);
                cell.Padding = thick ? new Thickness(7, 3, 9, 4) : new Thickness(4, 2, 4, 2);
                cell.FontSize = state == 3 ? 28 : 14;
                cell.Width = state == 2 ? 96 : 280;
                cell.TextWrapping = state == 3 ? TextWrapping.Wrap : TextWrapping.NoWrap;
                cell.TextAlignment = state == 2 ? TextAlignment.Right : TextAlignment.Left;
                cell.Value = text;
                cell.IsSelected = (state & 1) != 0;
                cell.IsCurrent = state >= 2;
                Check(VisualStateManager.GoToState(cell, state == 3 ? "Invalid" : "Valid", false),
                    $"Validation state is not attached to {cell.Name}'s native template root.");
            }
            UpdateLayout();
            await Task.Delay(10);
            Check(Reference.DesiredSize == Candidate.DesiredSize && Reference.ActualWidth == Candidate.ActualWidth &&
                Reference.ActualHeight == Candidate.ActualHeight, "The native template changed cell size.");
            var expectedText = Find<TextBlock>(Reference, "PART_Text");
            var actualText = Find<TextBlock>(Candidate, "PART_Text");
            Check(expectedText.Text == actualText.Text && actualText.Text == text, "The default text payload changed.");
            var expectedBounds = Bounds(expectedText, Reference);
            var actualBounds = Bounds(actualText, Candidate);
            Console.WriteLine($"UNO_TEMPLATE_GEOMETRY: theme={theme}; thick={thick}; state={state}; " +
                $"reference={Describe(expectedBounds)}; candidate={Describe(actualBounds)}; " +
                $"desired={expectedText.DesiredSize}/{actualText.DesiredSize}; " +
                $"font={expectedText.FontSize:R}/{actualText.FontSize:R}; " +
                $"margin={expectedText.Margin}/{actualText.Margin}; " +
                $"slot={Microsoft.UI.Xaml.Controls.Primitives.LayoutInformation.GetLayoutSlot(expectedText)}/{Microsoft.UI.Xaml.Controls.Primitives.LayoutInformation.GetLayoutSlot(actualText)}; " +
                $"actual={expectedText.ActualWidth},{expectedText.ActualHeight}/{actualText.ActualWidth},{actualText.ActualHeight}; " +
                $"align={expectedText.VerticalAlignment}/{actualText.VerticalAlignment}; " +
                $"parent={Describe(Bounds((FrameworkElement)VisualTreeHelper.GetParent(expectedText), Reference))}/{Describe(Bounds((FrameworkElement)VisualTreeHelper.GetParent(actualText), Candidate))}; " +
                $"parentDesired={((UIElement)VisualTreeHelper.GetParent(expectedText)).DesiredSize}/{((UIElement)VisualTreeHelper.GetParent(actualText)).DesiredSize}");
            Check(expectedBounds == actualBounds,
                $"The native template changed text padding, border reservation or alignment: theme={theme}, thick={thick}, state={state}; " +
                $"reference={Describe(expectedBounds)}; candidate={Describe(actualBounds)}.");
            foreach (var name in new[] { "SelectionBackground", "CurrentBorder", "ValidationBorder" })
            {
                var expected = Find<Border>(Reference, name);
                var actual = Find<Border>(Candidate, name);
                var opacity = name switch
                {
                    "SelectionBackground" => (state & 1) != 0 ? 1d : 0d,
                    "CurrentBorder" => state >= 2 ? 1d : 0d,
                    _ => state == 3 ? 1d : 0d,
                };
                Check(expected.Opacity == opacity && actual.Opacity == opacity,
                    "An intended visual state is inactive: " + name);
                Check(Bounds(expected, Reference) == Bounds(actual, Candidate),
                    "The native template changed independent overlay layout: " + name);
            }
            Check(Find<FrameworkElement>(Candidate, "CellBorder").ReadLocalValue(DataContextProperty) is null,
                "The default template's isolated data context was removed.");
            ++comparedStates;
            // Exact native raster comparison is additional to geometry, not a
            // substitute or a tolerance allowing a failing geometry check.
#if !__WASM__
            var expectedPixels = await CaptureAsync(Reference);
            var actualPixels = await CaptureAsync(Candidate);
            Check(expectedPixels.Width == actualPixels.Width && expectedPixels.Height == actualPixels.Height,
                $"Native template raster dimensions differ: {expectedPixels.Width}x{expectedPixels.Height} and " +
                $"{actualPixels.Width}x{actualPixels.Height} for {Describe(Bounds(Reference, this))} and {Describe(Bounds(Candidate, this))}.");
            Check(expectedPixels.Width > 0 && expectedPixels.Height > 0 &&
                expectedPixels.Pixels.Length == checked(expectedPixels.Width * expectedPixels.Height * 4),
                "Native reference raster is missing or incomplete.");
            Check(expectedPixels.Pixels.AsSpan().SequenceEqual(actualPixels.Pixels),
                $"Native text template pixels differ: theme={theme}, border={thick}, state={state}; " +
                $"reference={Convert.ToHexString(SHA256.HashData(expectedPixels.Pixels))}; " +
                $"candidate={Convert.ToHexString(SHA256.HashData(actualPixels.Pixels))}");
            ++pixelStates;
#endif
        }
        var expectedCount = ShowcaseRuntimeChecks.Descendants(Reference).Count();
        var actualCount = ShowcaseRuntimeChecks.Descendants(Candidate).Count();
        Check(actualCount == expectedCount, "The retained template changed the reference visual topology.");
        Console.WriteLine($"UNO_RUNTIME_TEXT_TEMPLATE_PARITY_PASSED: states={comparedStates}; exactNativePixelStates={pixelStates}; referenceVisuals={expectedCount}; candidateVisuals={actualCount}; active states, border/padding, text, independent translucent overlays, themes and width/font changes; rejected flattening not retained");
    }

    private static string Describe(Rect value) => $"[{value.X:R},{value.Y:R},{value.Width:R},{value.Height:R}]";
    private static Rect Bounds(FrameworkElement element, UIElement relative) =>
        element.TransformToVisual(relative).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
    private static T Find<T>(DependencyObject owner, string name) where T : FrameworkElement =>
        ShowcaseRuntimeChecks.Descendants(owner).OfType<T>().Single(element => element.Name == name);
#if !__WASM__
    private static async Task<(int Width, int Height, byte[] Pixels)> CaptureAsync(FrameworkElement element)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element);
        var pixels = (await bitmap.GetPixelsAsync()).ToArray();
#if WINDOWS
        return Crop(element, bitmap.PixelWidth, bitmap.PixelHeight, pixels);
#else
        return (bitmap.PixelWidth, bitmap.PixelHeight, pixels);
#endif
    }
#if WINDOWS
    // WinUI sizes the bitmap to cover the layout slots of descendants as well. The cell panel
    // arranges its text in a slot extending below the cell, so crop to the element itself and
    // require that nothing was drawn outside it.
    private static (int Width, int Height, byte[] Pixels) Crop(FrameworkElement element, int pixelWidth, int pixelHeight, byte[] pixels)
    {
        var scale = element.XamlRoot?.RasterizationScale ?? 1;
        var width = Math.Min(pixelWidth, (int)Math.Round(element.ActualWidth * scale));
        var height = Math.Min(pixelHeight, (int)Math.Round(element.ActualHeight * scale));
        if (width == pixelWidth && height == pixelHeight) return (pixelWidth, pixelHeight, pixels);
        var cropped = new byte[width * height * 4];
        for (var y = 0; y < pixelHeight; ++y)
        {
            var row = pixels.AsSpan(y * pixelWidth * 4, pixelWidth * 4);
            if (y < height) row[..(width * 4)].CopyTo(cropped.AsSpan(y * width * 4));
            Check((y < height ? row[(width * 4)..] : row).IndexOfAnyExcept((byte)0) < 0,
                "The native template drew outside its own bounds.");
        }
        return (width, height, cropped);
    }
#endif
#endif
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
