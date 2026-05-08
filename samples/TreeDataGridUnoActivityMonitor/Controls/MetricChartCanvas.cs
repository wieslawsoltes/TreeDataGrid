using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using TreeDataGridUnoActivityMonitor.Models;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Color = Windows.UI.Color;

namespace TreeDataGridUnoActivityMonitor.Controls;

public sealed class MetricChartCanvas : SKCanvasElement
{
    public static readonly DependencyProperty SeriesProperty =
        DependencyProperty.Register(
            nameof(Series),
            typeof(MetricSeries),
            typeof(MetricChartCanvas),
            new PropertyMetadata(null, OnInvalidatePropertyChanged));

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(
            nameof(AccentColor),
            typeof(Color),
            typeof(MetricChartCanvas),
            new PropertyMetadata(ColorHelper.FromArgb(0xFF, 0x5E, 0xD4, 0xFF), OnInvalidatePropertyChanged));

    private int _hoverIndex = -1;

    public MetricChartCanvas()
    {
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
    }

    public MetricSeries? Series
    {
        get => (MetricSeries?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        canvas.Clear(SKColors.Transparent);

        var width = Math.Max(0, (float)area.Width);
        var height = Math.Max(0, (float)area.Height);
        var rect = new SKRect(0, 0, width, height);
        var inset = new SKRect(rect.Left + 8, rect.Top + 8, rect.Right - 8, rect.Bottom - 8);

        DrawGrid(canvas, inset);

        if (Series?.Samples.IsDefaultOrEmpty != false)
        {
            using var font = new SKFont(SKTypeface.FromFamilyName("SF Pro Display"), 14);
            using var placeholder = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 110),
                IsAntialias = true,
            };

            canvas.DrawText("Waiting for telemetry", inset.Left + 2, inset.MidY, SKTextAlign.Left, font, placeholder);
            return;
        }

        var accent = ToSkColor(AccentColor);
        DrawThresholds(canvas, inset);
        DrawSeries(canvas, inset, accent);
    }

    private static void OnInvalidatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MetricChartCanvas)d).Invalidate();
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (Series?.Samples.IsDefaultOrEmpty != false)
            return;

        var x = e.GetCurrentPoint(this).Position.X;
        var segment = Math.Max(1, ActualWidth / Math.Max(Series.Samples.Length - 1, 1));
        _hoverIndex = Math.Clamp((int)Math.Round(x / segment), 0, Series.Samples.Length - 1);
        Invalidate();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hoverIndex = -1;
        Invalidate();
    }

    private void DrawSeries(SKCanvas canvas, SKRect rect, SKColor accent)
    {
        var series = Series!;
        var values = series.Samples;
        var ceiling = Math.Max(series.CeilingValue, 1);
        var count = Math.Max(values.Length - 1, 1);
        var points = new SKPoint[values.Length];

        for (var i = 0; i < values.Length; ++i)
        {
            var x = rect.Left + rect.Width * i / count;
            var normalized = Math.Clamp(values[i] / ceiling, 0, 1);
            var y = rect.Bottom - (float)(normalized * rect.Height);
            points[i] = new SKPoint(x, y);
        }

        using var areaPath = new SKPath();
        areaPath.MoveTo(points[0]);

        foreach (var point in points)
            areaPath.LineTo(point);

        areaPath.LineTo(rect.Right, rect.Bottom);
        areaPath.LineTo(rect.Left, rect.Bottom);
        areaPath.Close();

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Left, rect.Bottom),
                new[]
                {
                    accent.WithAlpha(170),
                    accent.WithAlpha(18),
                },
                null,
                SKShaderTileMode.Clamp),
        };

        using var linePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            Color = accent,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };

        using var linePath = new SKPath();
        linePath.MoveTo(points[0]);

        for (var i = 1; i < points.Length; ++i)
            linePath.LineTo(points[i]);

        canvas.DrawPath(areaPath, fillPaint);
        canvas.DrawPath(linePath, linePaint);

        var markerIndex = _hoverIndex >= 0 ? _hoverIndex : values.Length - 1;
        var marker = points[markerIndex];

        using var markerStroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            Color = accent.WithAlpha(110),
            PathEffect = SKPathEffect.CreateDash([4, 4], 0),
        };

        using var markerFill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = accent,
        };

        using var markerHalo = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = accent.WithAlpha(60),
        };

        canvas.DrawLine(marker.X, rect.Top, marker.X, rect.Bottom, markerStroke);
        canvas.DrawCircle(marker, 8, markerHalo);
        canvas.DrawCircle(marker, 4, markerFill);
    }

    private void DrawThresholds(SKCanvas canvas, SKRect rect)
    {
        var series = Series!;

        if (series.WarningValue is null && series.CriticalValue is null)
            return;

        if (series.CriticalValue is double critical)
        {
            var y = rect.Bottom - (float)(Math.Clamp(critical / series.CeilingValue, 0, 1) * rect.Height);
            using var paint = new SKPaint
            {
                Color = new SKColor(255, 117, 107, 38),
                Style = SKPaintStyle.Fill,
            };

            canvas.DrawRect(new SKRect(rect.Left, rect.Top, rect.Right, y), paint);
        }

        if (series.WarningValue is double warning)
        {
            var y = rect.Bottom - (float)(Math.Clamp(warning / series.CeilingValue, 0, 1) * rect.Height);
            using var paint = new SKPaint
            {
                Color = new SKColor(255, 200, 87, 24),
                Style = SKPaintStyle.Fill,
            };

            canvas.DrawRect(new SKRect(rect.Left, y, rect.Right, rect.Bottom), paint);
        }
    }

    private static void DrawGrid(SKCanvas canvas, SKRect rect)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            Color = new SKColor(255, 255, 255, 24),
        };

        for (var i = 0; i < 4; ++i)
        {
            var y = rect.Top + rect.Height * i / 3f;
            canvas.DrawLine(rect.Left, y, rect.Right, y, paint);
        }
    }

    private static SKColor ToSkColor(Color color) => new(color.R, color.G, color.B, color.A);
}
