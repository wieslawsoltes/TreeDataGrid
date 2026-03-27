using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace TreeDataGridUnoActivityMonitor.Models;

public enum MetricKind
{
    Cpu,
    Memory,
    Energy,
    Disk,
    Network,
}

public sealed class MonitorSnapshot
{
    public required SystemSummarySnapshot Summary { get; init; }
    public required MetricSectionSnapshot Cpu { get; init; }
    public required MetricSectionSnapshot Memory { get; init; }
    public required MetricSectionSnapshot Energy { get; init; }
    public required MetricSectionSnapshot Disk { get; init; }
    public required MetricSectionSnapshot Network { get; init; }
}

public sealed class SystemSummarySnapshot
{
    public required string HostName { get; init; }
    public required string PlatformLabel { get; init; }
    public required string ModeDescription { get; init; }
    public required bool IsDemoMode { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string CpuHeadline { get; init; }
    public required string CpuDetail { get; init; }
    public required string MemoryHeadline { get; init; }
    public required string MemoryDetail { get; init; }
    public required string EnergyHeadline { get; init; }
    public required string EnergyDetail { get; init; }
    public required string DiskHeadline { get; init; }
    public required string DiskDetail { get; init; }
    public required string NetworkHeadline { get; init; }
    public required string NetworkDetail { get; init; }
}

public sealed class MetricSectionSnapshot
{
    public required MetricKind Kind { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string SummaryText { get; init; }
    public required string InsightText { get; init; }
    public required string CurrentValueText { get; init; }
    public required string AverageValueText { get; init; }
    public required string PeakValueText { get; init; }
    public required MetricSeries Series { get; init; }
    public required IReadOnlyList<MonitorRowBase> Rows { get; init; }
}

public sealed class MetricSeries
{
    public static MetricSeries Empty(string label, string unit) => new()
    {
        Label = label,
        Unit = unit,
        Samples = ImmutableArray<double>.Empty,
        CurrentValue = 0,
        AverageValue = 0,
        PeakValue = 0,
        CeilingValue = 1,
    };

    public required string Label { get; init; }
    public required string Unit { get; init; }
    public required ImmutableArray<double> Samples { get; init; }
    public required double CurrentValue { get; init; }
    public required double AverageValue { get; init; }
    public required double PeakValue { get; init; }
    public required double CeilingValue { get; init; }
    public double? WarningValue { get; init; }
    public double? CriticalValue { get; init; }
}

public abstract class MonitorRowBase
{
    protected MonitorRowBase(
        string stableId,
        string name,
        string secondaryText,
        string badgeText,
        int? processId)
    {
        StableId = stableId;
        Name = name;
        SecondaryText = secondaryText;
        BadgeText = badgeText;
        ProcessId = processId;
    }

    public string StableId { get; }
    public string Name { get; }
    public string SecondaryText { get; }
    public string BadgeText { get; }
    public int? ProcessId { get; }
    public bool HasBadge => !string.IsNullOrWhiteSpace(BadgeText);
    public string SearchText => $"{Name} {SecondaryText} {BadgeText}";
}

public sealed class CpuMonitorRow : MonitorRowBase
{
    public CpuMonitorRow(
        string stableId,
        string name,
        string secondaryText,
        string badgeText,
        int processId,
        double cpuPercent,
        double userPercent,
        int threadCount,
        double idleWakeupsPerSecond)
        : base(stableId, name, secondaryText, badgeText, processId)
    {
        CpuPercent = cpuPercent;
        UserPercent = userPercent;
        ThreadCount = threadCount;
        IdleWakeupsPerSecond = idleWakeupsPerSecond;
        CpuPercentText = cpuPercent.ToString("0.0");
        UserPercentText = userPercent.ToString("0.0");
        ThreadCountText = threadCount.ToString("0");
        IdleWakeupsText = idleWakeupsPerSecond.ToString("0");
    }

    public double CpuPercent { get; }
    public double UserPercent { get; }
    public int ThreadCount { get; }
    public double IdleWakeupsPerSecond { get; }
    public string CpuPercentText { get; }
    public string UserPercentText { get; }
    public string ThreadCountText { get; }
    public string IdleWakeupsText { get; }
}

public sealed class MemoryMonitorRow : MonitorRowBase
{
    public MemoryMonitorRow(
        string stableId,
        string name,
        string secondaryText,
        string badgeText,
        int processId,
        ulong footprintBytes,
        ulong residentBytes,
        ulong compressedBytes,
        ulong pageIns)
        : base(stableId, name, secondaryText, badgeText, processId)
    {
        FootprintBytes = footprintBytes;
        ResidentBytes = residentBytes;
        CompressedBytes = compressedBytes;
        PageIns = pageIns;
        FootprintText = Services.MonitorFormatters.FormatBytes(footprintBytes);
        ResidentText = Services.MonitorFormatters.FormatBytes(residentBytes);
        CompressedText = Services.MonitorFormatters.FormatBytes(compressedBytes);
        PageInsText = Services.MonitorFormatters.FormatCompactNumber(pageIns);
    }

    public ulong FootprintBytes { get; }
    public ulong ResidentBytes { get; }
    public ulong CompressedBytes { get; }
    public ulong PageIns { get; }
    public string FootprintText { get; }
    public string ResidentText { get; }
    public string CompressedText { get; }
    public string PageInsText { get; }
}

public sealed class EnergyMonitorRow : MonitorRowBase
{
    public EnergyMonitorRow(
        string stableId,
        string name,
        string secondaryText,
        string badgeText,
        int processId,
        double score,
        double nanoJoulesPerSecond,
        double idleWakeupsPerSecond,
        double interruptWakeupsPerSecond)
        : base(stableId, name, secondaryText, badgeText, processId)
    {
        Score = score;
        NanoJoulesPerSecond = nanoJoulesPerSecond;
        IdleWakeupsPerSecond = idleWakeupsPerSecond;
        InterruptWakeupsPerSecond = interruptWakeupsPerSecond;
        ScoreText = score.ToString("0.0");
        EnergyText = Services.MonitorFormatters.FormatEnergyRate(nanoJoulesPerSecond);
        IdleWakeupsText = idleWakeupsPerSecond.ToString("0");
        InterruptWakeupsText = interruptWakeupsPerSecond.ToString("0");
    }

    public double Score { get; }
    public double NanoJoulesPerSecond { get; }
    public double IdleWakeupsPerSecond { get; }
    public double InterruptWakeupsPerSecond { get; }
    public string ScoreText { get; }
    public string EnergyText { get; }
    public string IdleWakeupsText { get; }
    public string InterruptWakeupsText { get; }
}

public sealed class DiskMonitorRow : MonitorRowBase
{
    public DiskMonitorRow(
        string stableId,
        string name,
        string secondaryText,
        string badgeText,
        int processId,
        double readBytesPerSecond,
        double writeBytesPerSecond,
        ulong totalReadBytes,
        ulong totalWriteBytes)
        : base(stableId, name, secondaryText, badgeText, processId)
    {
        ReadBytesPerSecond = readBytesPerSecond;
        WriteBytesPerSecond = writeBytesPerSecond;
        TotalReadBytes = totalReadBytes;
        TotalWriteBytes = totalWriteBytes;
        ReadRateText = Services.MonitorFormatters.FormatRate(readBytesPerSecond);
        WriteRateText = Services.MonitorFormatters.FormatRate(writeBytesPerSecond);
        TotalReadText = Services.MonitorFormatters.FormatBytes(totalReadBytes);
        TotalWriteText = Services.MonitorFormatters.FormatBytes(totalWriteBytes);
    }

    public double ReadBytesPerSecond { get; }
    public double WriteBytesPerSecond { get; }
    public ulong TotalReadBytes { get; }
    public ulong TotalWriteBytes { get; }
    public string ReadRateText { get; }
    public string WriteRateText { get; }
    public string TotalReadText { get; }
    public string TotalWriteText { get; }
}

public sealed class NetworkMonitorRow : MonitorRowBase
{
    public NetworkMonitorRow(
        string stableId,
        string name,
        string secondaryText,
        string badgeText,
        double receivedBytesPerSecond,
        double sentBytesPerSecond,
        ulong packetsIn,
        ulong packetsOut)
        : base(stableId, name, secondaryText, badgeText, null)
    {
        ReceivedBytesPerSecond = receivedBytesPerSecond;
        SentBytesPerSecond = sentBytesPerSecond;
        PacketsIn = packetsIn;
        PacketsOut = packetsOut;
        ReceiveRateText = Services.MonitorFormatters.FormatRate(receivedBytesPerSecond);
        SendRateText = Services.MonitorFormatters.FormatRate(sentBytesPerSecond);
        PacketsInText = Services.MonitorFormatters.FormatCompactNumber(packetsIn);
        PacketsOutText = Services.MonitorFormatters.FormatCompactNumber(packetsOut);
    }

    public double ReceivedBytesPerSecond { get; }
    public double SentBytesPerSecond { get; }
    public ulong PacketsIn { get; }
    public ulong PacketsOut { get; }
    public string ReceiveRateText { get; }
    public string SendRateText { get; }
    public string PacketsInText { get; }
    public string PacketsOutText { get; }
}
