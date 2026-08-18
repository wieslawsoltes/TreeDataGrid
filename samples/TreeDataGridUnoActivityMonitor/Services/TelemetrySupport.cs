using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using TreeDataGridUnoActivityMonitor.Models;
using Color = Windows.UI.Color;

namespace TreeDataGridUnoActivityMonitor.Services;

internal static class MonitorTelemetryProviderFactory
{
    public static IMonitorTelemetryProvider Create()
    {
#if __ANDROID__ || __IOS__ || __WASM__
        return new DemoTelemetryProvider();
#else
        return OperatingSystem.IsMacOS()
            ? new MacOsMonitorTelemetryProvider()
            : new DemoTelemetryProvider();
#endif
    }
}

internal sealed class MetricSeriesBuffer
{
    private readonly int _capacity;
    private readonly Queue<double> _samples;

    public MetricSeriesBuffer(int capacity = 90)
    {
        _capacity = capacity;
        _samples = new Queue<double>(capacity);
    }

    public void Add(double value)
    {
        if (_samples.Count == _capacity)
            _samples.Dequeue();

        _samples.Enqueue(Math.Max(0, value));
    }

    public MetricSeries Snapshot(
        string label,
        string unit,
        double ceiling,
        double? warning = null,
        double? critical = null)
    {
        var values = _samples.ToImmutableArray();
        var peak = values.Length == 0 ? 0 : values.Max();
        var average = values.Length == 0 ? 0 : values.Average();
        var current = values.Length == 0 ? 0 : values[^1];

        return new MetricSeries
        {
            Label = label,
            Unit = unit,
            Samples = values,
            CurrentValue = current,
            AverageValue = average,
            PeakValue = peak,
            CeilingValue = Math.Max(Math.Max(ceiling, peak * 1.08), 1),
            WarningValue = warning,
            CriticalValue = critical,
        };
    }
}

internal static class MonitorFormatters
{
    private static readonly string[] s_units = ["B", "KB", "MB", "GB", "TB"];

    public static string FormatCompactNumber(double value)
    {
        if (value >= 1_000_000_000)
            return $"{value / 1_000_000_000d:0.0}B";
        if (value >= 1_000_000)
            return $"{value / 1_000_000d:0.0}M";
        if (value >= 1_000)
            return $"{value / 1_000d:0.0}K";
        return value.ToString("0", CultureInfo.InvariantCulture);
    }

    public static string FormatCompactNumber(ulong value) => FormatCompactNumber((double)value);

    public static string FormatBytes(ulong value) => FormatBytes((double)value);

    public static string FormatBytes(double value)
    {
        var index = 0;
        var scaled = Math.Max(0, value);

        while (scaled >= 1024 && index < s_units.Length - 1)
        {
            scaled /= 1024;
            index++;
        }

        return $"{scaled:0.#} {s_units[index]}";
    }

    public static string FormatRate(double bytesPerSecond) => $"{FormatBytes(bytesPerSecond)}/s";

    public static string FormatPercent(double value) => $"{value:0.#}%";

    public static string FormatEnergyRate(double nanoJoulesPerSecond)
    {
        var millijoules = nanoJoulesPerSecond / 1_000_000d;

        if (millijoules >= 1000)
            return $"{millijoules / 1000d:0.##} J/s";

        return $"{millijoules:0.#} mJ/s";
    }

    public static string FormatTimestamp(DateTimeOffset value) => value.ToLocalTime().ToString("HH:mm:ss");

    public static string BuildProcessSubtitle(int pid, int threads, string? suffix = null)
    {
        var prefix = $"PID {pid} · {threads} threads";
        return string.IsNullOrWhiteSpace(suffix) ? prefix : $"{prefix} · {suffix}";
    }

    public static SolidColorBrush CreateAccentBrush(Color color) => new(color);
}
