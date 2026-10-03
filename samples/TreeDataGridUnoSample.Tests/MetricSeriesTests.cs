using System;
using System.Collections.Immutable;
using TreeDataGridUnoActivityMonitor.Models;
using Xunit;

namespace TreeDataGridUnoSample.Tests;

public class MetricSeriesTests
{
    [Fact]
    public void Public_Activation_Produces_A_Valid_Empty_Series()
    {
        // Mirrors the native XamlTypeInfo generated activation expression.
        var series = new MetricSeries();
        Assert.Equal(string.Empty, series.Label);
        Assert.Equal(string.Empty, series.Unit);
        Assert.False(series.Samples.IsDefault);
        Assert.Empty(series.Samples);
        Assert.Equal(0, series.CurrentValue);
        Assert.Equal(0, series.AverageValue);
        Assert.Equal(0, series.PeakValue);
        Assert.Equal(1, series.CeilingValue);
        Assert.Null(series.WarningValue);
        Assert.Null(series.CriticalValue);
    }

    [Fact]
    public void Empty_Preserves_The_Requested_Label_And_Unit()
    {
        var series = MetricSeries.Empty("CPU", "%");
        Assert.Equal("CPU", series.Label);
        Assert.Equal("%", series.Unit);
        Assert.False(series.Samples.IsDefault);
        Assert.Empty(series.Samples);
        Assert.Equal(1, series.CeilingValue);
    }

    [Fact]
    public void Snapshot_Initializers_Override_Activation_Defaults()
    {
        var values = ImmutableArray.Create(2.0, 4.0);
        var series = new MetricSeries
        {
            Label = "Traffic", Unit = "KB/s", Samples = values,
            CurrentValue = 4, AverageValue = 3, PeakValue = 4,
            CeilingValue = 8, WarningValue = 6, CriticalValue = 7,
        };
        Assert.Equal("Traffic", series.Label);
        Assert.Equal("KB/s", series.Unit);
        Assert.Equal(values, series.Samples);
        Assert.Equal(4, series.CurrentValue);
        Assert.Equal(3, series.AverageValue);
        Assert.Equal(4, series.PeakValue);
        Assert.Equal(8, series.CeilingValue);
        Assert.Equal(6, series.WarningValue);
        Assert.Equal(7, series.CriticalValue);
    }
}
