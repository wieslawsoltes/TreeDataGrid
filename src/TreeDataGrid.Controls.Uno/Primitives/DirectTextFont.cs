#if !WINDOWS
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarfBuzzSharp;
using Microsoft.UI.Text;
using SkiaSharp;
using Windows.UI.Text;
using HbBuffer = HarfBuzzSharp.Buffer;
using HbFont = HarfBuzzSharp.Font;

namespace Uno.Controls.Primitives;

/// <summary>
/// The font a Skia <see cref="Microsoft.UI.Xaml.Controls.TextBlock"/> shapes and draws with,
/// obtained from Uno's own font cache so that direct text rendering uses the same typeface,
/// the same Skia font settings and the same HarfBuzz shaping font.
/// </summary>
internal sealed class DirectTextFont
{
    private const string FontCacheType = "Microsoft.UI.Xaml.Documents.TextFormatting.FontDetailsCache";
    private const string FontDetailsType = "Microsoft.UI.Xaml.Documents.TextFormatting.FontDetails";
    private const string CoreServicesType = "Uno.UI.Xaml.Core.CoreServices";
    private const int FirstCharacter = 0x20;
    private const int LastCharacter = 0x17F;

    private static readonly Dictionary<Key, DirectTextFont?> s_fonts = new();
    private static readonly MethodInfo? s_getFont;
    private static readonly PropertyInfo? s_taskResult;
    private static readonly PropertyInfo? s_skFont;
    private static readonly PropertyInfo? s_shapingFont;
    private static readonly PropertyInfo? s_coreServices;
    private static readonly PropertyInfo? s_fontScale;
    [ThreadStatic] private static HbBuffer? s_buffer;

    private readonly ulong[] _supported = new ulong[(LastCharacter - FirstCharacter + 64) / 64];

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicMethods, FontCacheType, "Uno.UI")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicProperties, FontDetailsType, "Uno.UI")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicProperties, CoreServicesType, "Uno.UI")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The members are preserved by DynamicDependency.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The members are preserved by DynamicDependency.")]
    [UnconditionalSuppressMessage("Trimming", "IL2065", Justification = "Task<FontDetails>.Result is used by Uno itself.")]
    static DirectTextFont()
    {
        try
        {
            var assembly = typeof(Microsoft.UI.Xaml.Controls.TextBlock).Assembly;
            var cache = assembly.GetType(FontCacheType);
            var details = assembly.GetType(FontDetailsType);
            var core = assembly.GetType(CoreServicesType);
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic;
            s_getFont = cache?.GetMethod("GetFont", any | BindingFlags.Static,
                new[] { typeof(string), typeof(float), typeof(FontWeight), typeof(FontStretch), typeof(FontStyle) });
            // GetFont returns (FontDetails details, Task<FontDetails> loadedTask).
            s_taskResult = s_getFont?.ReturnType.GetGenericArguments()[1].GetProperty("Result");
            s_skFont = details?.GetProperty("SKFont", any | BindingFlags.Instance);
            s_shapingFont = details?.GetProperty("Font", any | BindingFlags.Instance);
            s_coreServices = core?.GetProperty("Instance", any | BindingFlags.Static);
            s_fontScale = core?.GetProperty("FontScale", any | BindingFlags.Instance);
        }
        catch (Exception)
        {
            s_getFont = null;
        }
    }

    private DirectTextFont(SKFont font, HbFont shaper)
    {
        Font = font;
        Shaper = shaper;
        shaper.GetScale(out var xScale, out _);
        // FontDetails.TextScale: HarfBuzz units (scale 512) to pixels.
        ScaleX = font.Size * font.ScaleX / xScale;
        var metrics = font.Metrics;
        Ascent = metrics.Ascent;
        LineHeight = metrics.Descent - metrics.Ascent;
        for (var c = FirstCharacter; c <= LastCharacter; ++c)
            if (font.ContainsGlyph(c)) _supported[(c - FirstCharacter) >> 6] |= 1UL << ((c - FirstCharacter) & 63);
        if (font.ContainsGlyph('…'))
        {
            var buffer = Buffer;
            buffer.AddUtf16("…");
            buffer.GuessSegmentProperties();
            shaper.Shape(buffer);
            var infos = buffer.GetGlyphInfoSpan();
            var positions = buffer.GetGlyphPositionSpan();
            EllipsisGlyphs = new ushort[infos.Length];
            EllipsisOffsets = new SKPoint[infos.Length];
            var x = 0f;
            for (var i = 0; i < infos.Length; ++i)
            {
                EllipsisGlyphs[i] = (ushort)infos[i].Codepoint;
                EllipsisOffsets[i] = new SKPoint(x + positions[i].XOffset * ScaleX, positions[i].YOffset * ScaleX);
                x += positions[i].XAdvance * ScaleX;
            }
            EllipsisWidth = x;
            buffer.ClearContents();
        }
    }

    public SKFont Font { get; }
    public HbFont Shaper { get; }
    public float ScaleX { get; }
    public float Ascent { get; }
    public float LineHeight { get; }
    public ushort[]? EllipsisGlyphs { get; }
    public SKPoint[]? EllipsisOffsets { get; }
    public float EllipsisWidth { get; }

    public static bool IsAvailable => s_getFont is not null && s_taskResult is not null && s_skFont is not null && s_shapingFont is not null;

    /// <summary>A reusable shaping buffer; shaping only happens on the UI thread.</summary>
    public static HbBuffer Buffer => s_buffer ??= new HbBuffer();

    public bool Supports(char c) =>
        c >= FirstCharacter && c <= LastCharacter &&
        (_supported[(c - FirstCharacter) >> 6] & (1UL << ((c - FirstCharacter) & 63))) != 0;

    /// <summary>
    /// Returns the loaded font, or null while it is still loading or when it cannot be
    /// obtained; the caller then renders with a text block, which handles loading itself.
    /// </summary>
    public static DirectTextFont? Get(string? family, double fontSize, bool scaleEnabled, FontWeight weight, FontStretch stretch, FontStyle style)
    {
        if (!IsAvailable) return null;
        var size = (float)ScaledFontSize(fontSize, scaleEnabled);
        var key = new Key(family, size, weight.Weight, stretch, style);
        if (s_fonts.TryGetValue(key, out var font)) return font;
        try
        {
            var result = (ITuple)s_getFont!.Invoke(null, new object?[] { family, size, weight, stretch, style })!;
            if (result[1] is not Task { IsCompletedSuccessfully: true } task) return null;
            var details = s_taskResult!.GetValue(task);
            font = details is null ? null : new DirectTextFont((SKFont)s_skFont!.GetValue(details)!, (HbFont)s_shapingFont!.GetValue(details)!);
        }
        catch (Exception)
        {
            font = null;
        }
        s_fonts[key] = font;
        return font;
    }

    private static double ScaledFontSize(double fontSize, bool scaleEnabled)
    {
        // TextScaleHelper.GetScaledFontSize with CoreServices.FontScale.
        if (!scaleEnabled || global::Uno.UI.FeatureConfiguration.Font.IgnoreTextScaleFactor || fontSize <= 0) return fontSize;
        double scale = 1;
        try
        {
            if (s_coreServices?.GetValue(null) is { } core && s_fontScale?.GetValue(core) is double value) scale = value;
        }
        catch (Exception)
        {
            scale = 1;
        }
        if (scale <= 1) return fontSize;
        var size = Math.Max(fontSize, 1);
        return size + Math.Max(-Math.E * Math.Log(size) + 18, 0) * (scale - 1);
    }

    private readonly record struct Key(string? Family, float Size, ushort Weight, FontStretch Stretch, FontStyle Style);
}
#endif
