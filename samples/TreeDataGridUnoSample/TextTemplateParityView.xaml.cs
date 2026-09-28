using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Uno.Controls.Primitives;
using Windows.Foundation;

namespace TreeDataGridUnoSample;

/// <summary>Compare active native states with the pre-flattening visual layout.</summary>
public sealed partial class TextTemplateParityView : UserControl
{
    public TextTemplateParityView() => InitializeComponent();

    internal static async Task RunAsync(MainPage page)
    {
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
        Check(Find<FrameworkElement>(Reference, "CellBorder") is Border &&
            Find<FrameworkElement>(Candidate, "CellBorder") is Grid,
            "The reference/candidate do not exercise distinct nested/flat roots.");
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
                Reference.ActualHeight == Candidate.ActualHeight, "The flattened template changed native cell size.");
            var expectedText = Find<TextBlock>(Reference, "PART_Text");
            var actualText = Find<TextBlock>(Candidate, "PART_Text");
            Check(expectedText.Text == actualText.Text && actualText.Text == text, "The default text payload changed.");
            Check(Bounds(expectedText, Reference) == Bounds(actualText, Candidate),
                "The flattened template changed text padding, border reservation or alignment.");
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
                    "The flattened template changed independent overlay layout: " + name);
            }
            Check(Find<FrameworkElement>(Candidate, "CellBorder").ReadLocalValue(DataContextProperty) is null,
                "The default template's isolated data context was removed.");
            ++comparedStates;
#if !__WASM__
            var expectedPixels = await CaptureAsync(Reference);
            var actualPixels = await CaptureAsync(Candidate);
            Check(expectedPixels.Width == actualPixels.Width && expectedPixels.Height == actualPixels.Height,
                "Native template raster dimensions differ.");
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
        Check(actualCount == expectedCount - 1, "Flattening did not remove exactly one visual layer.");
        Console.WriteLine($"UNO_RUNTIME_TEXT_TEMPLATE_PARITY_PASSED: states={comparedStates}; exactNativePixelStates={pixelStates}; referenceVisuals={expectedCount}; candidateVisuals={actualCount}; active states, border/padding, text, independent translucent overlays, themes and width/font changes; reference state groups explicitly moved to its root");
    }

    private static Rect Bounds(FrameworkElement element, UIElement relative) =>
        element.TransformToVisual(relative).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

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
