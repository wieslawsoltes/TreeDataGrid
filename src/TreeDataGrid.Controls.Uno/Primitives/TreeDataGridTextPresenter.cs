#if !WINDOWS
using System;
using System.Collections.Generic;
using HarfBuzzSharp;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Uno.Controls.Primitives;

/// <summary>
/// Draws a cell's <c>PART_Text</c> directly with Skia, as Avalonia's text cell draws its
/// text layout, instead of creating a text block layout for every value.
/// </summary>
/// <remarks>
/// <para>
/// <c>PART_Text</c> stays in the template and remains the source of the text's font,
/// foreground, alignment, trimming, margin and padding, including visual-state setters. This
/// element reproduces what that text block draws for single-line left-to-right text: the
/// same font objects from Uno's font cache, the same HarfBuzz shaping runs, glyph positions,
/// line height, character ellipsis and alignment, and the same layout rounding. Anything
/// else (wrapping, other scripts, missing glyphs, tabs, line breaks, non-solid brushes,
/// character spacing, decorations, selection, explicit sizes, opacity) is rendered by the
/// text block itself.
/// </para>
/// <para>
/// Direct rendering can be disabled with the <c>Uno.Controls.TreeDataGrid.DirectTextRendering</c>
/// <see cref="AppContext"/> switch or the <c>TDG_DIRECT_TEXT=0</c> environment variable.
/// </para>
/// </remarks>
internal sealed class TreeDataGridTextPresenter : Panel
{
    /// <summary>
    /// Extra canvas space around the text box. A text block does not clip ink outside its
    /// line box (side bearings, italics, accents) unless layout clips it, but a Skia canvas
    /// element clips to its bounds; so this panel has the text block's layout, and with it
    /// the same layout clip, while its canvas child extends past it.
    /// </summary>
    private const double Bleed = 4;

    private static readonly DependencyProperty[] s_sourceProperties =
    {
        TextBlock.ForegroundProperty, TextBlock.FontSizeProperty, TextBlock.FontFamilyProperty,
        TextBlock.FontWeightProperty, TextBlock.FontStyleProperty, TextBlock.FontStretchProperty,
        TextBlock.TextAlignmentProperty, TextBlock.TextWrappingProperty, TextBlock.TextTrimmingProperty,
        TextBlock.PaddingProperty, TextBlock.CharacterSpacingProperty, TextBlock.LineHeightProperty,
        TextBlock.LineStackingStrategyProperty, TextBlock.TextDecorationsProperty,
        TextBlock.IsTextScaleFactorEnabledProperty, TextBlock.IsTextSelectionEnabledProperty,
        FrameworkElement.MarginProperty, FrameworkElement.FlowDirectionProperty,
        FrameworkElement.HorizontalAlignmentProperty, FrameworkElement.VerticalAlignmentProperty,
        FrameworkElement.WidthProperty, FrameworkElement.HeightProperty,
        FrameworkElement.MinWidthProperty, FrameworkElement.MinHeightProperty,
        FrameworkElement.MaxWidthProperty, FrameworkElement.MaxHeightProperty,
        UIElement.OpacityProperty,
    };

    private readonly TextBlock _source;
    private readonly long[] _tokens = new long[s_sourceProperties.Length];
    private readonly DependencyPropertyChangedCallback _sourceChanged;
    private readonly DependencyPropertyChangedCallback _brushChanged;
    private SolidColorBrush? _brush;
    private long _brushColorToken;
    private long _brushOpacityToken;

    private string _text = string.Empty;
    private bool _shown;
    private bool? _textSupported;
    private bool _styleSupported;
    private bool _solidForeground;
    private DirectTextFont? _font;
    private Thickness _padding;
    private TextAlignment _alignment;
    private bool _ellipsis;
    private float _lineHeight;

    // Shaped text: glyphs and clusters, independent of the available width.
    private string? _shapedText;
    private DirectTextFont? _shapedFont;
    private ShapedText? _shaped;
    private ushort[] _glyphs = Array.Empty<ushort>();
    private float[] _advances = Array.Empty<float>();
    private SKPoint[] _offsets = Array.Empty<SKPoint>();
    private Cluster[] _clusters = Array.Empty<Cluster>();
    private int _glyphCount;
    private int _clusterCount;
    private float _width;
    private float _widthWithoutTrailingSpaces;

    private readonly TextCanvas _canvas = new();
    // The arranged glyph run depends only on the text, style, font and arranged width.
    private int _version;
    private int _arrangedVersion = -1;
    private double _arrangedWidth = double.NaN;

    public TreeDataGridTextPresenter(TextBlock source)
    {
        _source = source;
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
        Children.Add(_canvas);
        _sourceChanged = OnSourceChanged;
        _brushChanged = OnBrushChanged;
        for (var i = 0; i < s_sourceProperties.Length; ++i)
            _tokens[i] = source.RegisterPropertyChangedCallback(s_sourceProperties[i], _sourceChanged);
        RefreshStyle();
    }

    public static bool IsEnabled { get; } = GetIsEnabled();

    protected override AutomationPeer OnCreateAutomationPeer() => new PresenterAutomationPeer(this);

    public TextBlock Source => _source;

    /// <summary>The displayed text, which the text block holds only while it displays it.</summary>
    public string Text => _text;

    /// <summary>Whether this element, rather than the text block, currently shows the text.</summary>
    public bool IsRendering => _shown && CanRender;

    private bool CanRender => _styleSupported && _font is not null && (_textSupported ??= IsSupported(_text, _font));

    private static bool GetIsEnabled()
    {
        if (Environment.GetEnvironmentVariable("TDG_DIRECT_TEXT") == "0") return false;
        if (AppContext.TryGetSwitch("Uno.Controls.TreeDataGrid.DirectTextRendering", out var enabled) && !enabled) return false;
        try { return SKCanvasElement.IsSupportedOnCurrentPlatform() && DirectTextFont.IsAvailable; }
        catch (Exception) { return false; }
    }

    public void Detach()
    {
        for (var i = 0; i < s_sourceProperties.Length; ++i)
            _source.UnregisterPropertyChangedCallback(s_sourceProperties[i], _tokens[i]);
        SetBrush(null);
        if (Parent is Panel panel) panel.Children.Remove(this);
        if (!string.Equals(_source.Text, _text, StringComparison.Ordinal)) _source.Text = _text;
        if (_shown) SetVisibility(_source, Visibility.Visible);
    }

    /// <summary>Shows or hides the text; the text block shows it when this element cannot.</summary>
    public void SetShown(bool shown)
    {
        _shown = shown;
        UpdateVisibility();
    }

    /// <summary>
    /// Publishes a cell value. While the text block displays the text it is assigned exactly
    /// as before (local value, string instance, binding replacement and callbacks); while
    /// this element draws it, the text block is left alone, since assigning its text
    /// re-creates its inlines and requests a frame, and it is synchronized when it is shown.
    /// </summary>
    public void SetText(string text)
    {
        var changed = !string.Equals(_text, text, StringComparison.Ordinal);
        _text = text;
        if (changed)
        {
            _textSupported = null;
            ++_version;
            // A font that was still loading (for example in the browser) may be ready now.
            if (_font is null && _styleSupported) UpdateFont();
        }
        UpdateVisibility(publish: true);
        if (changed) InvalidateMeasure();
    }

    private void UpdateVisibility(bool publish = false)
    {
        var direct = IsRendering;
        var native = _shown && !direct;
        if (native && (publish || !ReferenceEquals(_source.Text, _text))) _source.Text = _text;
        SetVisibility(this, direct ? Visibility.Visible : Visibility.Collapsed);
        SetVisibility(_source, native ? Visibility.Visible : Visibility.Collapsed);
    }

    private static void SetVisibility(UIElement element, Visibility value)
    {
        // As TreeDataGridCell.SetContentVisibility: an owned local value, written only on change.
        if (element.Visibility != value || element.ReadLocalValue(VisibilityProperty) is not Visibility local || local != value)
            element.Visibility = value;
    }

    private void OnSourceChanged(DependencyObject sender, DependencyProperty property)
    {
        if (property == TextBlock.ForegroundProperty && _source.Foreground is null or SolidColorBrush == _solidForeground)
        {
            UpdateBrush();
            return;
        }
        RefreshStyle();
        UpdateVisibility();
    }

    private void RefreshStyle()
    {
        var source = _source;
        var trimming = source.TextTrimming;
        var alignment = source.VerticalAlignment;
        _styleSupported =
            source.TextWrapping == TextWrapping.NoWrap &&
            trimming is TextTrimming.None or TextTrimming.CharacterEllipsis or TextTrimming.Clip &&
            source.CharacterSpacing == 0 &&
            source.TextDecorations == Windows.UI.Text.TextDecorations.None &&
            source.FlowDirection == FlowDirection.LeftToRight &&
            (source.LineHeight <= 0 || source.LineStackingStrategy == LineStackingStrategy.MaxHeight) &&
            !source.IsTextSelectionEnabled &&
            source.Opacity == 1 &&
            double.IsNaN(source.Width) && double.IsNaN(source.Height) &&
            source.MinWidth == 0 && source.MinHeight == 0 &&
            double.IsPositiveInfinity(source.MaxWidth) && double.IsPositiveInfinity(source.MaxHeight) &&
            (_solidForeground = source.Foreground is null or SolidColorBrush);
        _ellipsis = trimming == TextTrimming.CharacterEllipsis;
        _alignment = source.TextAlignment;
        _padding = source.Padding;
        var margin = source.Margin;
        if (Margin != margin) Margin = margin;
        if (HorizontalAlignment != source.HorizontalAlignment) HorizontalAlignment = source.HorizontalAlignment;
        // TreeDataGridCellContentPanel centers a stretched text block, so both mean centered.
        var vertical = alignment == VerticalAlignment.Stretch ? VerticalAlignment.Center : alignment;
        if (VerticalAlignment != vertical) VerticalAlignment = vertical;
        _font = null;
        ++_version;
        if (_styleSupported) UpdateFont();
        UpdateBrush();
        InvalidateMeasure();
    }

    private void UpdateFont()
    {
        var source = _source;
        var font = DirectTextFont.Get(source.FontFamily?.Source, source.FontSize, source.IsTextScaleFactorEnabled,
            source.FontWeight, source.FontStretch, source.FontStyle);
        if (font is not null && _ellipsis && font.EllipsisGlyphs is null) font = null;
        _font = font;
        _lineHeight = font is null ? 0 : Math.Max((float)source.LineHeight, font.LineHeight);
        _textSupported = null;
        ++_version;
    }

    private void UpdateBrush()
    {
        SetBrush(_source.Foreground as SolidColorBrush);
        UpdateColor();
    }

    private void SetBrush(SolidColorBrush? brush)
    {
        if (ReferenceEquals(_brush, brush)) return;
        if (_brush is { } old)
        {
            old.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _brushColorToken);
            old.UnregisterPropertyChangedCallback(Brush.OpacityProperty, _brushOpacityToken);
        }
        _brush = brush;
        if (brush is not null)
        {
            _brushColorToken = brush.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _brushChanged);
            _brushOpacityToken = brush.RegisterPropertyChangedCallback(Brush.OpacityProperty, _brushChanged);
        }
    }

    private void OnBrushChanged(DependencyObject sender, DependencyProperty property) => UpdateColor();

    private void UpdateColor()
    {
        // UnicodeText.BrushToColor with the brush opacity; black without a brush.
        uint color = 0xFF000000;
        if (_brush is { } brush)
        {
            var c = brush.Color;
            var alpha = (byte)(c.A * brush.Opacity);
            color = (uint)alpha << 24 | (uint)c.R << 16 | (uint)c.G << 8 | c.B;
        }
        _canvas.SetColor(color);
    }

    private static bool IsSupported(string text, DirectTextFont font)
    {
        foreach (var c in text)
            if (c is >= '\u007F' and < ' ' || !font.Supports(c)) return false;
        return true;
    }

    // ICU script of the supported characters: Latin letters, everything else Common.
    private static bool IsLatin(char c) =>
        c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or 'ª' or 'º' ||
        c >= 'À' && c != '×' && c != '÷';

    private void Shape(DirectTextFont font)
    {
        if (ReferenceEquals(_shapedFont, font) && string.Equals(_shapedText, _text, StringComparison.Ordinal)) return;
        _shapedFont = font;
        _shapedText = _text;
        var shaped = _shaped = font.GetShapedText(_text, ShapedText.Create);
        _glyphs = shaped.Glyphs;
        _advances = shaped.Advances;
        _offsets = shaped.Offsets;
        _clusters = shaped.Clusters;
        _glyphCount = shaped.Glyphs.Length;
        _clusterCount = shaped.Clusters.Length;
        _width = shaped.Width;
        _widthWithoutTrailingSpaces = shaped.WidthWithoutTrailingSpaces;
    }

    /// <summary>
    /// Immutable shaping of one string with one font: glyphs, positions and clusters, which
    /// do not depend on the available width. Shared by every cell showing the same text.
    /// </summary>
    internal sealed class ShapedText
    {
        public static readonly ShapedText Empty = new(Array.Empty<ushort>(), Array.Empty<float>(), Array.Empty<SKPoint>(), Array.Empty<Cluster>(), 0, 0);
        // Scratch buffers; shaping only happens on the UI thread.
        [ThreadStatic] private static ushort[]? s_glyphs;
        [ThreadStatic] private static float[]? s_advances;
        [ThreadStatic] private static SKPoint[]? s_offsets;
        [ThreadStatic] private static Cluster[]? s_clusters;

        private ShapedText(ushort[] glyphs, float[] advances, SKPoint[] offsets, Cluster[] clusters, float width, float widthWithoutTrailingSpaces)
        {
            Glyphs = glyphs;
            Advances = advances;
            Offsets = offsets;
            Clusters = clusters;
            Width = width;
            WidthWithoutTrailingSpaces = widthWithoutTrailingSpaces;
        }

        public ushort[] Glyphs { get; }
        public float[] Advances { get; }
        public SKPoint[] Offsets { get; }
        public Cluster[] Clusters { get; }
        public float Width { get; }
        public float WidthWithoutTrailingSpaces { get; }
        /// <summary>The last arranged glyph run of this text, and what it was arranged for.</summary>
        public Frame? Frame { get; set; }
        public FrameKey FrameKey { get; set; }

        public static ShapedText Create(string text, DirectTextFont font)
        {
            if (text.Length == 0) return Empty;
            var glyphs = s_glyphs ??= new ushort[64];
            var advances = s_advances ??= new float[64];
            var offsets = s_offsets ??= new SKPoint[64];
            var clusters = s_clusters ??= new Cluster[64];
            var glyphCount = 0;
            var clusterCount = 0;
            var buffer = DirectTextFont.Buffer;
            var scale = font.ScaleX;
            var start = 0;
            // UnicodeText shapes each script run separately, in logical order.
            for (var i = 1; i <= text.Length; ++i)
            {
                if (i < text.Length && IsLatin(text[i]) == IsLatin(text[i - 1])) continue;
                buffer.ClearContents();
                buffer.AddUtf16(text.AsSpan(start, i - start));
                buffer.GuessSegmentProperties();
                buffer.Direction = Direction.LeftToRight;
                font.Shaper.Shape(buffer);
                var infos = buffer.GetGlyphInfoSpan();
                var positions = buffer.GetGlyphPositionSpan();
                if (glyphs.Length < glyphCount + infos.Length)
                {
                    var size = Math.Max(glyphCount + infos.Length, glyphs.Length * 2);
                    Array.Resize(ref glyphs, size);
                    Array.Resize(ref advances, size);
                    Array.Resize(ref offsets, size);
                    s_glyphs = glyphs;
                    s_advances = advances;
                    s_offsets = offsets;
                }
                var clusterStart = glyphCount;
                var clusterText = clusterCount == 0 ? 0 : clusters[clusterCount - 1].End;
                for (var g = 0; g < infos.Length; ++g)
                {
                    if (g > 0 && infos[g].Cluster != infos[g - 1].Cluster)
                    {
                        AddCluster(text, clusterText, start + (int)infos[g].Cluster, clusterStart, glyphCount);
                        clusterText = start + (int)infos[g].Cluster;
                        clusterStart = glyphCount;
                    }
                    glyphs[glyphCount] = (ushort)infos[g].Codepoint;
                    advances[glyphCount] = positions[g].XAdvance * scale;
                    offsets[glyphCount] = new SKPoint(positions[g].XOffset * scale, positions[g].YOffset * scale);
                    ++glyphCount;
                }
                if (glyphCount > clusterStart) AddCluster(text, clusterText, i, clusterStart, glyphCount);
                start = i;
            }
            buffer.ClearContents();
            float width = 0, trailing = 0;
            for (var i = 0; i < clusterCount; ++i)
            {
                ref readonly var cluster = ref clusters[i];
                width += cluster.Width;
                trailing = cluster.Whitespace ? trailing + cluster.Width : 0;
            }
            return new(glyphs.AsSpan(0, glyphCount).ToArray(), advances.AsSpan(0, glyphCount).ToArray(),
                offsets.AsSpan(0, glyphCount).ToArray(), clusters.AsSpan(0, clusterCount).ToArray(),
                width, trailing == width ? 0 : width - trailing);

            void AddCluster(string value, int from, int end, int firstGlyph, int endGlyph)
            {
                var whitespace = true;
                for (var i = from; i < end; ++i) whitespace &= char.IsWhiteSpace(value[i]);
                float clusterWidth = 0;
                for (var g = firstGlyph; g < endGlyph; ++g) clusterWidth += advances[g];
                if (clusterCount == clusters.Length)
                {
                    Array.Resize(ref clusters, clusters.Length * 2);
                    s_clusters = clusters;
                }
                clusters[clusterCount++] = new Cluster(end, firstGlyph, endGlyph - firstGlyph, clusterWidth, whitespace);
            }
        }
    }

    /// <summary>UnicodeText's single-line layout: the visible clusters, ellipsis and width.</summary>
    private Line Layout(double available)
    {
        var width = _width;
        var without = _widthWithoutTrailingSpaces;
        var count = _clusterCount;
        if (!_ellipsis || count == 0 || !(without > available)) return new Line(count - 1, false, without);
        // CharacterEllipsis: candidates are the clusters from the last to the second.
        var next = count - 1;
        var current = 0;
        var more = MoveNext(ref next, ref current);
        var last = count - 1;
        var prefix = width;
        var ellipsis = _font!.EllipsisWidth;
        while (more)
        {
            var candidate = current;
            more = MoveNext(ref next, ref current);
            while (_clusters[candidate].Whitespace && candidate > 0)
            {
                --candidate;
                if (more && current == candidate) more = MoveNext(ref next, ref current);
            }
            while (last != candidate) prefix -= _clusters[last--].Width;
            if (prefix + ellipsis <= available || !more) return new Line(candidate, true, prefix + ellipsis);
        }
        return new Line(count - 1, false, without);

        static bool MoveNext(ref int next, ref int current)
        {
            if (next < 1) return false;
            current = next--;
            return true;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_font is not { } font) return default;
        Shape(font);
        var padding = _padding;
        _canvas.Measure(default);
        var available = Math.Max(0, availableSize.Width - padding.Left - padding.Right);
        var line = Layout(available);
        var width = (_text.Length == 0 ? 0 : line.Width) + padding.Left + padding.Right;
        var height = _lineHeight + padding.Top + padding.Bottom;
        if (UseLayoutRounding)
        {
            // TextBlock.MeasureOverride rounds its size up to whole device pixels.
            var scale = (float)(XamlRoot?.RasterizationScale ?? 1);
            width = (float)(int)Math.Ceiling(width * scale) / scale;
            height = (float)(int)Math.Ceiling(height * scale) / scale;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _canvas.Arrange(new Rect(-Bleed, -Bleed, finalSize.Width + 2 * Bleed, finalSize.Height + 2 * Bleed));
        if (_arrangedVersion == _version && _arrangedWidth == finalSize.Width) return finalSize;
        _arrangedVersion = _version;
        _arrangedWidth = finalSize.Width;
        var font = _font;
        if (font is null || _text.Length == 0)
        {
            _canvas.SetFrame(null);
            return finalSize;
        }
        Shape(font);
        var padding = _padding;
        var available = Math.Max(0, finalSize.Width - padding.Left - padding.Right);
        // Cells of one column showing the same value share one immutable glyph run.
        var shaped = _shaped!;
        var key = new FrameKey(available, _alignment, _ellipsis, padding.Left, padding.Top);
        if (shaped.Frame is { } cached && shaped.FrameKey == key)
        {
            _canvas.SetFrame(cached);
            return finalSize;
        }
        var line = Layout(available);
        var visible = line.Ellipsis ? line.Last + 1 : _clusterCount;
        var glyphs = 0;
        for (var i = 0; i < visible; ++i)
            if (!_clusters[i].Whitespace) glyphs += _clusters[i].GlyphCount;
        if (line.Ellipsis) glyphs += font.EllipsisGlyphs!.Length;
        var ids = new ushort[glyphs];
        var points = new SKPoint[glyphs];
        var offset = AlignmentOffset(line.Width, (float)available);
        var baseline = -font.Ascent;
        var n = 0;
        float sum = 0;
        for (var i = 0; i < visible; ++i)
        {
            ref readonly var cluster = ref _clusters[i];
            if (!cluster.Whitespace)
            {
                var x = sum + offset;
                for (var g = cluster.FirstGlyph; g < cluster.FirstGlyph + cluster.GlyphCount; ++g)
                {
                    ids[n] = _glyphs[g];
                    points[n++] = new SKPoint(x + _offsets[g].X, baseline + _offsets[g].Y);
                    x += _advances[g];
                }
            }
            sum += cluster.Width;
        }
        if (line.Ellipsis)
        {
            var x = sum + offset;
            var ellipsis = font.EllipsisOffsets!;
            for (var g = 0; g < ellipsis.Length; ++g)
            {
                ids[n] = font.EllipsisGlyphs![g];
                points[n++] = new SKPoint(x + ellipsis[g].X, baseline + ellipsis[g].Y);
            }
        }
        var frame = new Frame(font.Font, ids, points, (float)(Bleed + padding.Left), (float)(Bleed + padding.Top));
        shaped.Frame = frame;
        shaped.FrameKey = key;
        _canvas.SetFrame(frame);
        return finalSize;
    }

    private float AlignmentOffset(float width, float available)
    {
        // UnicodeText.GetAlignmentOffsetForLine for left-to-right text.
        float offset = _alignment switch
        {
            TextAlignment.Center when width <= available => (available - width) / 2,
            TextAlignment.Right when width <= available => available - width,
            _ => 0,
        };
        return Math.Max(offset, 0);
    }

    /// <summary>Exposes the displayed text as a text block's peer does.</summary>
    private sealed class PresenterAutomationPeer(TreeDataGridTextPresenter owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(TreeDataGridTextPresenter);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
        protected override string GetNameCore() => owner._text;
        protected override IList<AutomationPeer>? GetChildrenCore() => null;
    }

    internal readonly record struct FrameKey(double Available, TextAlignment Alignment, bool Ellipsis, double PaddingLeft, double PaddingTop);

    internal readonly record struct Cluster(int End, int FirstGlyph, int GlyphCount, float Width, bool Whitespace);

    private readonly record struct Line(int Last, bool Ellipsis, float Width);

    internal sealed class Frame(SKFont font, ushort[] glyphs, SKPoint[] points, float x, float y)
    {
        public SKFont Font { get; } = font;
        public ushort[] Glyphs { get; } = glyphs;
        public SKPoint[] Points { get; } = points;
        public float X { get; } = x;
        public float Y { get; } = y;
        public SKTextBlob? Blob { get; set; }

        public bool SameAs(Frame other) =>
            ReferenceEquals(Font, other.Font) && X == other.X && Y == other.Y &&
            Glyphs.AsSpan().SequenceEqual(other.Glyphs) && Points.AsSpan().SequenceEqual(other.Points);
    }

    /// <summary>Draws the arranged glyph run; rendering may happen off the UI thread.</summary>
    private sealed class TextCanvas : SKCanvasElement
    {
        [ThreadStatic] private static SKPaint? s_paint;
        [ThreadStatic] private static SKTextBlobBuilder? s_builder;
        private Frame? _frame;
        private uint _color = 0xFF000000;

        public TextCanvas() => IsHitTestVisible = false;

        public void SetFrame(Frame? frame)
        {
            if (ReferenceEquals(_frame, frame) || frame is not null && _frame is { } old && old.SameAs(frame)) return;
            _frame = frame;
            Invalidate();
        }

        public void SetColor(uint color)
        {
            if (_color == color) return;
            _color = color;
            Invalidate();
        }

        protected override Size MeasureOverride(Size availableSize) => default;

        protected override void RenderOverride(SKCanvas canvas, Size area)
        {
            if (_frame is not { } frame || frame.Glyphs.Length == 0) return;
            var paint = s_paint ??= new SKPaint { IsStroke = false, IsAntialias = true };
            paint.Color = new SKColor(_color);
            var blob = frame.Blob;
            if (blob is null)
            {
                var builder = s_builder ??= new SKTextBlobBuilder();
                builder.AddPositionedRun(frame.Glyphs, frame.Font, frame.Points);
                frame.Blob = blob = builder.Build();
            }
            canvas.DrawText(blob, frame.X, frame.Y, paint);
        }
    }
}
#endif
