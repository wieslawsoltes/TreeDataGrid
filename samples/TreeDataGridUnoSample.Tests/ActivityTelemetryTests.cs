using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TreeDataGridUnoActivityMonitor.Models;
using TreeDataGridUnoActivityMonitor.Services;
using Xunit;

namespace TreeDataGridUnoSample.Tests;

public class ActivityTelemetryTests
{
    [Fact]
    public async Task Mac_native_cpu_rate_agrees_with_process_cpu_time()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using var provider = new MacOsMonitorTelemetryProvider();
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var first = await provider.CaptureAsync(CancellationToken.None);
        var before = process.TotalProcessorTime;
        await Task.Run(() =>
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 750) Thread.SpinWait(10000);
        });
        var second = await provider.CaptureAsync(CancellationToken.None);
        process.Refresh();
        var cpu = (process.TotalProcessorTime - before).TotalSeconds;
        var elapsed = (second.Summary.Timestamp - first.Summary.Timestamp).TotalSeconds;
        var row = Assert.Single(second.Cpu.Rows.OfType<CpuMonitorRow>(), x => x.ProcessId == process.Id);
        var expected = cpu / elapsed * 100;
        Assert.InRange(row.CpuPercent, expected * 0.5, expected * 2);
    }

    [Fact]
    public async Task Demo_keeps_all_five_sections_and_bounded_series_with_new_row_snapshots()
    {
        using var provider = new DemoTelemetryProvider();
        var first = await provider.CaptureAsync(CancellationToken.None);
        var last = first;
        for (var i = 0; i < 100; ++i) last = await provider.CaptureAsync(CancellationToken.None);
        var sections = new[] { last.Cpu, last.Memory, last.Energy, last.Disk, last.Network };
        Assert.Equal(Enum.GetValues<MetricKind>(), sections.Select(x => x.Kind));
        Assert.All(sections, section =>
        {
            Assert.NotEmpty(section.Rows);
            Assert.Equal(section.Rows.Count, section.Rows.Select(x => x.StableId).Distinct().Count());
            Assert.Equal(90, section.Series.Samples.Length);
            Assert.All(section.Series.Samples, value => Assert.True(double.IsFinite(value) && value >= 0));
        });
        Assert.NotSame(first.Cpu.Rows[0], last.Cpu.Rows.Single(x => x.StableId == first.Cpu.Rows[0].StableId));
        Assert.True(last.Summary.Timestamp > first.Summary.Timestamp);
    }

    [Fact]
    public async Task Cancelled_capture_does_not_advance_demo()
    {
        using var provider = new DemoTelemetryProvider();
        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.CaptureAsync(new CancellationToken(true)));
        var snapshot = await provider.CaptureAsync(CancellationToken.None);
        Assert.Single(snapshot.Cpu.Series.Samples);
    }

    [Theory]
    [InlineData(20UL, 10UL, 10d)]
    [InlineData(10UL, 20UL, 0d)]
    [InlineData(0UL, ulong.MaxValue, 0d)]
    public void Counter_reset_never_underflows(ulong current, ulong previous, double expected) =>
        Assert.Equal(expected, MonitorFormatters.CounterDelta(current, previous));

    [Fact]
    public void Unavailable_compression_is_not_reported_as_wired_memory()
    {
        var row = new MemoryMonitorRow("1", "Process", "", "", 1, 100, 90, null, 2);
        Assert.Null(row.CompressedBytes);
        Assert.Equal("—", row.CompressedText);
    }
}
