using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TreeDataGridUnoActivityMonitor.Models;

namespace TreeDataGridUnoActivityMonitor.Services;

internal sealed class DemoTelemetryProvider : IMonitorTelemetryProvider
{
    private readonly List<DemoProcessState> _processes;
    private readonly MetricSeriesBuffer _cpuSeries = new();
    private readonly MetricSeriesBuffer _memorySeries = new();
    private readonly MetricSeriesBuffer _energySeries = new();
    private readonly MetricSeriesBuffer _diskSeries = new();
    private readonly MetricSeriesBuffer _networkSeries = new();
    private DateTimeOffset _timestamp = DateTimeOffset.UtcNow;
    private int _tick;

    public DemoTelemetryProvider()
    {
        var names = new[]
        {
            "WindowServer", "Safari", "Xcode", "Finder", "Music", "Slack", "Photos",
            "Mail", "Figma", "Notes", "Preview", "Terminal", "Dock", "Spotlight",
            "Calendar", "Messages", "Shortcuts", "Maps", "Simulator", "QuickLookUIService",
        };

        _processes = names.Select((name, index) => new DemoProcessState(
            ProcessId: 220 + index,
            Name: name,
            CpuBias: 1.6 + index * 0.17,
            MemoryBytes: (ulong)(450_000_000 + index * 88_000_000d),
            Phase: index * 0.41))
            .ToList();
    }

    public bool IsDemoMode => true;

    public void Dispose()
    {
    }

    public Task<MonitorSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _tick++;
        _timestamp = _timestamp.AddSeconds(1);

        var cpuRows = new List<CpuMonitorRow>(_processes.Count);
        var memoryRows = new List<MemoryMonitorRow>(_processes.Count);
        var energyRows = new List<EnergyMonitorRow>(_processes.Count);
        var diskRows = new List<DiskMonitorRow>(_processes.Count);

        double totalCpu = 0;
        double totalEnergyScore = 0;
        double totalEnergyRate = 0;
        double totalDiskRate = 0;
        ulong totalMemoryBytes = 0;

        foreach (var process in _processes)
        {
            var cpu = Wave(process.Phase, process.CpuBias * 18, process.CpuBias * 6, 0.17, _tick);
            var user = cpu * 0.68;
            var energyScore = cpu * 0.52 + Wave(process.Phase, 7.5, 1.2, 0.14, _tick);
            var energyRate = (energyScore * 12_500_000d) + Wave(process.Phase, 7_500_000d, 3_000_000d, 0.11, _tick);
            var readRate = Wave(process.Phase, 4_200_000, 320_000, 0.09, _tick);
            var writeRate = Wave(process.Phase + 0.8, 2_800_000, 180_000, 0.12, _tick);
            var footprint = (ulong)(process.MemoryBytes * (0.84 + 0.12 * Math.Abs(Math.Sin(_tick * 0.07 + process.Phase))));
            var resident = (ulong)(footprint * 0.81);
            var compressed = (ulong)(footprint * 0.12);
            var pageIns = (ulong)(120 + _tick * 4 + process.ProcessId % 19);
            var idleWakeups = Wave(process.Phase, 210, 14, 0.13, _tick);
            var interruptWakeups = Wave(process.Phase + 0.4, 96, 8, 0.11, _tick);
            var threads = 5 + (process.ProcessId % 11);
            var badge = process.ProcessId % 5 == 0 ? "system" : string.Empty;

            cpuRows.Add(new CpuMonitorRow(
                stableId: process.ProcessId.ToString(),
                name: process.Name,
                secondaryText: MonitorFormatters.BuildProcessSubtitle(process.ProcessId, threads),
                badgeText: badge,
                processId: process.ProcessId,
                cpuPercent: cpu,
                userPercent: user,
                threadCount: threads,
                idleWakeupsPerSecond: idleWakeups));

            memoryRows.Add(new MemoryMonitorRow(
                stableId: process.ProcessId.ToString(),
                name: process.Name,
                secondaryText: MonitorFormatters.BuildProcessSubtitle(process.ProcessId, threads),
                badgeText: badge,
                processId: process.ProcessId,
                footprintBytes: footprint,
                residentBytes: resident,
                compressedBytes: compressed,
                pageIns: pageIns));

            energyRows.Add(new EnergyMonitorRow(
                stableId: process.ProcessId.ToString(),
                name: process.Name,
                secondaryText: MonitorFormatters.BuildProcessSubtitle(process.ProcessId, threads),
                badgeText: badge,
                processId: process.ProcessId,
                score: energyScore,
                nanoJoulesPerSecond: energyRate,
                idleWakeupsPerSecond: idleWakeups,
                interruptWakeupsPerSecond: interruptWakeups));

            diskRows.Add(new DiskMonitorRow(
                stableId: process.ProcessId.ToString(),
                name: process.Name,
                secondaryText: MonitorFormatters.BuildProcessSubtitle(process.ProcessId, threads, "I/O active"),
                badgeText: badge,
                processId: process.ProcessId,
                readBytesPerSecond: readRate,
                writeBytesPerSecond: writeRate,
                totalReadBytes: (ulong)(readRate * (18 + process.ProcessId % 4)),
                totalWriteBytes: (ulong)(writeRate * (12 + process.ProcessId % 5))));

            totalCpu += cpu;
            totalEnergyScore += energyScore;
            totalEnergyRate += energyRate;
            totalDiskRate += readRate + writeRate;
            totalMemoryBytes += footprint;
        }

        var networkRows = new List<NetworkMonitorRow>
        {
            new(
                "en0",
                "Wi-Fi",
                "Primary interface · 1.2 Gbps link",
                "wireless",
                Wave(0.3, 28_000_000, 5_000_000, 0.08, _tick),
                Wave(0.9, 12_000_000, 1_800_000, 0.12, _tick),
                packetsIn: (ulong)(12_400 + _tick * 23),
                packetsOut: (ulong)(8_600 + _tick * 18)),
            new(
                "utun3",
                "VPN Tunnel",
                "Secure transport · on demand",
                "vpn",
                Wave(1.6, 7_600_000, 740_000, 0.1, _tick),
                Wave(2.1, 5_200_000, 520_000, 0.13, _tick),
                packetsIn: (ulong)(4_200 + _tick * 11),
                packetsOut: (ulong)(3_500 + _tick * 9)),
            new(
                "lo0",
                "Loopback",
                "Local simulator traffic",
                "local",
                Wave(2.8, 2_400_000, 160_000, 0.11, _tick),
                Wave(3.2, 2_700_000, 220_000, 0.09, _tick),
                packetsIn: (ulong)(18_200 + _tick * 41),
                packetsOut: (ulong)(18_200 + _tick * 41)),
        };

        var totalNetworkRate = networkRows.Sum(x => x.ReceivedBytesPerSecond + x.SentBytesPerSecond);
        var physicalMemory = 32UL * 1024 * 1024 * 1024;
        var cpuActive = Math.Min(totalCpu / 8.5, 100);
        var memoryPressure = Math.Min((double)totalMemoryBytes / physicalMemory * 100 + 24, 100);
        var diskUsedBytes = 612UL * 1024 * 1024 * 1024;
        var diskCapacityBytes = 1000UL * 1024 * 1024 * 1024;

        _cpuSeries.Add(cpuActive);
        _memorySeries.Add(memoryPressure);
        _energySeries.Add(totalEnergyScore);
        _diskSeries.Add(totalDiskRate);
        _networkSeries.Add(totalNetworkRate);

        var cpuSeries = _cpuSeries.Snapshot("CPU", "%", 100, 65, 85);
        var memorySeries = _memorySeries.Snapshot("Memory Pressure", "%", 100, 70, 88);
        var energySeries = _energySeries.Snapshot("Energy Score", "score", 120, 54, 84);
        var diskSeries = _diskSeries.Snapshot("Disk Throughput", "B/s", 42_000_000, 18_000_000, 30_000_000);
        var networkSeries = _networkSeries.Snapshot("Network Throughput", "B/s", 52_000_000, 18_000_000, 30_000_000);

        cpuRows.Sort((x, y) => y.CpuPercent.CompareTo(x.CpuPercent));
        memoryRows.Sort((x, y) => y.FootprintBytes.CompareTo(x.FootprintBytes));
        energyRows.Sort((x, y) => y.Score.CompareTo(x.Score));
        diskRows.Sort((x, y) => (y.ReadBytesPerSecond + y.WriteBytesPerSecond).CompareTo(x.ReadBytesPerSecond + x.WriteBytesPerSecond));
        networkRows.Sort((x, y) => (y.ReceivedBytesPerSecond + y.SentBytesPerSecond).CompareTo(x.ReceivedBytesPerSecond + x.SentBytesPerSecond));

        var cpuTop = cpuRows.FirstOrDefault();
        var memoryTop = memoryRows.FirstOrDefault();
        var energyTop = energyRows.FirstOrDefault();
        var diskTop = diskRows.FirstOrDefault();
        var networkTop = networkRows.FirstOrDefault();

        var summary = new SystemSummarySnapshot
        {
            HostName = Environment.MachineName,
            PlatformLabel = ".NET 10 cross-platform shell",
            ModeDescription = "Demo telemetry keeps the full TreeDataGrid and chart surface running on non-macOS targets.",
            IsDemoMode = true,
            Timestamp = _timestamp,
            CpuHeadline = MonitorFormatters.FormatPercent(cpuSeries.CurrentValue),
            CpuDetail = $"8 logical cores · top process {cpuTop?.Name ?? "idle"}",
            MemoryHeadline = MonitorFormatters.FormatPercent(memorySeries.CurrentValue),
            MemoryDetail = $"{MonitorFormatters.FormatBytes(totalMemoryBytes)} used of {MonitorFormatters.FormatBytes(physicalMemory)}",
            EnergyHeadline = energyTop?.ScoreText ?? "0.0",
            EnergyDetail = $"Top score from {energyTop?.Name ?? "idle"}",
            DiskHeadline = MonitorFormatters.FormatRate(diskSeries.CurrentValue),
            DiskDetail = $"{MonitorFormatters.FormatBytes(diskUsedBytes)} used of {MonitorFormatters.FormatBytes(diskCapacityBytes)}",
            NetworkHeadline = MonitorFormatters.FormatRate(networkSeries.CurrentValue),
            NetworkDetail = $"{networkTop?.Name ?? "Wi-Fi"} carrying the most traffic",
        };

        return Task.FromResult(new MonitorSnapshot
        {
            Summary = summary,
            Cpu = new MetricSectionSnapshot
            {
                Kind = MetricKind.Cpu,
                Title = "CPU",
                Subtitle = "Per-process active time over the last second.",
                SummaryText = $"{cpuRows.Count} processes · {summary.CpuHeadline} active",
                InsightText = cpuTop is null ? "No process activity." : $"{cpuTop.Name} is pacing the CPU lane right now.",
                CurrentValueText = summary.CpuHeadline,
                AverageValueText = MonitorFormatters.FormatPercent(cpuSeries.AverageValue),
                PeakValueText = MonitorFormatters.FormatPercent(cpuSeries.PeakValue),
                Series = cpuSeries,
                Rows = cpuRows,
            },
            Memory = new MetricSectionSnapshot
            {
                Kind = MetricKind.Memory,
                Title = "Memory",
                Subtitle = "Footprint, resident memory, compression, and page-ins.",
                SummaryText = $"{memoryRows.Count} processes · {summary.MemoryHeadline} pressure",
                InsightText = memoryTop is null ? "No memory footprint available." : $"{memoryTop.Name} is holding the deepest footprint.",
                CurrentValueText = summary.MemoryHeadline,
                AverageValueText = MonitorFormatters.FormatPercent(memorySeries.AverageValue),
                PeakValueText = MonitorFormatters.FormatPercent(memorySeries.PeakValue),
                Series = memorySeries,
                Rows = memoryRows,
            },
            Energy = new MetricSectionSnapshot
            {
                Kind = MetricKind.Energy,
                Title = "Energy",
                Subtitle = "Public energy metrics using wakeups and estimated nanojoules.",
                SummaryText = $"{energyRows.Count} processes · top score {summary.EnergyHeadline}",
                InsightText = energyTop is null ? "No energy telemetry available." : $"{energyTop.Name} is the hottest energy consumer.",
                CurrentValueText = summary.EnergyHeadline,
                AverageValueText = $"{energySeries.AverageValue:0.0}",
                PeakValueText = $"{energySeries.PeakValue:0.0}",
                Series = energySeries,
                Rows = energyRows,
            },
            Disk = new MetricSectionSnapshot
            {
                Kind = MetricKind.Disk,
                Title = "Disk",
                Subtitle = "Read and write rates mapped to active processes.",
                SummaryText = $"{diskRows.Count} processes · {summary.DiskHeadline}",
                InsightText = diskTop is null ? "No disk movement detected." : $"{diskTop.Name} is moving the most disk traffic.",
                CurrentValueText = summary.DiskHeadline,
                AverageValueText = MonitorFormatters.FormatRate(diskSeries.AverageValue),
                PeakValueText = MonitorFormatters.FormatRate(diskSeries.PeakValue),
                Series = diskSeries,
                Rows = diskRows,
            },
            Network = new MetricSectionSnapshot
            {
                Kind = MetricKind.Network,
                Title = "Network",
                Subtitle = "Interface traffic and packet flow over the current window.",
                SummaryText = $"{networkRows.Count} interfaces · {summary.NetworkHeadline}",
                InsightText = networkTop is null ? "No network movement detected." : $"{networkTop.Name} is carrying the current peak.",
                CurrentValueText = summary.NetworkHeadline,
                AverageValueText = MonitorFormatters.FormatRate(networkSeries.AverageValue),
                PeakValueText = MonitorFormatters.FormatRate(networkSeries.PeakValue),
                Series = networkSeries,
                Rows = networkRows,
            },
        });
    }

    private static double Wave(double phase, double amplitude, double floor, double speed, int tick)
    {
        return floor + amplitude * (0.5 + 0.5 * Math.Sin(phase + tick * speed));
    }

    private sealed record DemoProcessState(int ProcessId, string Name, double CpuBias, ulong MemoryBytes, double Phase);
}
