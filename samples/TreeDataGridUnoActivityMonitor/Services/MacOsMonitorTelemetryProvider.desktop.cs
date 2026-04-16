using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TreeDataGridUnoActivityMonitor.Models;

namespace TreeDataGridUnoActivityMonitor.Services;

internal sealed class MacOsMonitorTelemetryProvider : IMonitorTelemetryProvider
{
    private const int ProcAllPids = 1;
    private const int ProcPidTaskAllInfo = 2;
    private const int ProcPidTaskAllInfoSize = 232;
    private const int RusageInfoCurrent = 6;
    private const int ProcessorCpuLoadInfo = 2;
    private const int HostVmInfo64 = 4;
    private const int CpuStateCount = 4;
    private const int CpuStateUser = 0;
    private const int CpuStateSystem = 1;
    private const int CpuStateIdle = 2;
    private const int CpuStateNice = 3;
    private const int AfLink = 18;
    private const int MntNowait = 2;
    private const uint IffLoopback = 0x8;
    private const uint IffRunning = 0x40;
    private const int MaxComLen = 16;
    private const int MaxPathLen = 1024;
    private const int MfsTypeNameLen = 16;

    private readonly MetricSeriesBuffer _cpuSeries = new();
    private readonly MetricSeriesBuffer _memorySeries = new();
    private readonly MetricSeriesBuffer _energySeries = new();
    private readonly MetricSeriesBuffer _diskSeries = new();
    private readonly MetricSeriesBuffer _networkSeries = new();
    private readonly Dictionary<int, ProcessCounters> _previousProcessCounters = new();
    private readonly Dictionary<string, NetworkCounters> _previousNetworkCounters = new(StringComparer.Ordinal);
    private long[]? _previousCpuLoad;
    private DateTimeOffset? _previousTimestamp;
    private readonly ulong _physicalMemoryBytes = ReadUnsignedLong("hw.memsize");

    public bool IsDemoMode => false;

    public void Dispose()
    {
    }

    public Task<MonitorSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => CaptureSnapshot(cancellationToken), cancellationToken);
    }

    private MonitorSnapshot CaptureSnapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        var elapsedSeconds = Math.Max((now - (_previousTimestamp ?? now.AddSeconds(-1))).TotalSeconds, 1);
        _previousTimestamp = now;

        var cpuSummary = SampleCpuLoad();
        var memorySummary = SampleMemory();
        var volumeSummary = SampleVolumes();
        var interfaceRows = SampleInterfaces(elapsedSeconds);
        var processSnapshot = SampleProcesses(elapsedSeconds, cancellationToken);

        var cpuRows = processSnapshot.CpuRows.OrderByDescending(x => x.CpuPercent).Take(280).Cast<MonitorRowBase>().ToList();
        var memoryRows = processSnapshot.MemoryRows.OrderByDescending(x => x.FootprintBytes).Take(280).Cast<MonitorRowBase>().ToList();
        var energyRows = processSnapshot.EnergyRows.OrderByDescending(x => x.Score).Take(280).Cast<MonitorRowBase>().ToList();
        var diskRows = processSnapshot.DiskRows.OrderByDescending(x => x.ReadBytesPerSecond + x.WriteBytesPerSecond).Take(280).Cast<MonitorRowBase>().ToList();
        var networkRows = interfaceRows.OrderByDescending(x => x.ReceivedBytesPerSecond + x.SentBytesPerSecond).Cast<MonitorRowBase>().ToList();

        var totalNetworkRate = interfaceRows.Sum(x => x.ReceivedBytesPerSecond + x.SentBytesPerSecond);
        var totalDiskRate = processSnapshot.DiskRows.Sum(x => x.ReadBytesPerSecond + x.WriteBytesPerSecond);
        var totalEnergyRate = processSnapshot.EnergyRows.Sum(x => x.NanoJoulesPerSecond);
        var totalEnergyScore = processSnapshot.EnergyRows.Sum(x => x.Score);
        var memoryPressure = _physicalMemoryBytes == 0 ? 0 : Math.Clamp(memorySummary.UsedBytes / (double)_physicalMemoryBytes * 100, 0, 100);

        _cpuSeries.Add(cpuSummary.ActivePercent);
        _memorySeries.Add(memoryPressure);
        _energySeries.Add(totalEnergyScore);
        _diskSeries.Add(totalDiskRate);
        _networkSeries.Add(totalNetworkRate);

        var cpuSeries = _cpuSeries.Snapshot("CPU", "%", 100, 65, 85);
        var memorySeries = _memorySeries.Snapshot("Memory", "%", 100, 68, 86);
        var energySeries = _energySeries.Snapshot("Energy", "score", 140, 64, 94);
        var diskSeries = _diskSeries.Snapshot("Disk", "B/s", Math.Max(totalDiskRate * 1.2, 32_000_000), totalDiskRate * 0.7, totalDiskRate * 0.9);
        var networkSeries = _networkSeries.Snapshot("Network", "B/s", Math.Max(totalNetworkRate * 1.2, 32_000_000), totalNetworkRate * 0.7, totalNetworkRate * 0.9);

        var cpuTop = cpuRows.OfType<CpuMonitorRow>().FirstOrDefault();
        var memoryTop = memoryRows.OfType<MemoryMonitorRow>().FirstOrDefault();
        var energyTop = energyRows.OfType<EnergyMonitorRow>().FirstOrDefault();
        var diskTop = diskRows.OfType<DiskMonitorRow>().FirstOrDefault();
        var networkTop = networkRows.OfType<NetworkMonitorRow>().FirstOrDefault();

        return new MonitorSnapshot
        {
            Summary = new SystemSummarySnapshot
            {
                HostName = Environment.MachineName,
                PlatformLabel = "macOS AppKit shell · native telemetry",
                ModeDescription = "Live telemetry uses libproc, Mach host statistics, getfsstat, and getifaddrs on the desktop head.",
                IsDemoMode = false,
                Timestamp = now,
                CpuHeadline = MonitorFormatters.FormatPercent(cpuSeries.CurrentValue),
                CpuDetail = $"{Environment.ProcessorCount} logical cores · top process {cpuTop?.Name ?? "idle"}",
                MemoryHeadline = MonitorFormatters.FormatPercent(memorySeries.CurrentValue),
                MemoryDetail = $"{MonitorFormatters.FormatBytes(memorySummary.UsedBytes)} used of {MonitorFormatters.FormatBytes(_physicalMemoryBytes)}",
                EnergyHeadline = energyTop?.ScoreText ?? "0.0",
                EnergyDetail = energyTop is null ? "No public energy data." : $"Top process {energyTop.Name}",
                DiskHeadline = MonitorFormatters.FormatRate(diskSeries.CurrentValue),
                DiskDetail = $"{MonitorFormatters.FormatBytes(volumeSummary.UsedBytes)} used of {MonitorFormatters.FormatBytes(volumeSummary.TotalBytes)}",
                NetworkHeadline = MonitorFormatters.FormatRate(networkSeries.CurrentValue),
                NetworkDetail = networkTop is null ? "No interface data." : $"Busiest interface {networkTop.Name}",
            },
            Cpu = BuildSection(
                MetricKind.Cpu,
                "CPU",
                "Per-process CPU time and wakeups from libproc snapshots.",
                $"{cpuRows.Count} processes · {MonitorFormatters.FormatPercent(cpuSeries.CurrentValue)} active",
                cpuTop is null ? "No CPU activity captured." : $"{cpuTop.Name} is leading the CPU lane.",
                MonitorFormatters.FormatPercent(cpuSeries.CurrentValue),
                MonitorFormatters.FormatPercent(cpuSeries.AverageValue),
                MonitorFormatters.FormatPercent(cpuSeries.PeakValue),
                cpuSeries,
                cpuRows),
            Memory = BuildSection(
                MetricKind.Memory,
                "Memory",
                "Physical footprint, resident memory, compression, and page-ins.",
                $"{memoryRows.Count} processes · {MonitorFormatters.FormatPercent(memorySeries.CurrentValue)} pressure",
                memoryTop is null ? "No memory footprint captured." : $"{memoryTop.Name} is carrying the largest footprint.",
                MonitorFormatters.FormatPercent(memorySeries.CurrentValue),
                MonitorFormatters.FormatPercent(memorySeries.AverageValue),
                MonitorFormatters.FormatPercent(memorySeries.PeakValue),
                memorySeries,
                memoryRows),
            Energy = BuildSection(
                MetricKind.Energy,
                "Energy",
                "Public energy estimate built from nanojoules and wakeups.",
                $"{energyRows.Count} processes · top score {energyTop?.ScoreText ?? "0.0"}",
                energyTop is null ? "No energy telemetry captured." : $"{energyTop.Name} is the current energy hotspot.",
                energyTop?.ScoreText ?? "0.0",
                $"{energySeries.AverageValue:0.0}",
                $"{energySeries.PeakValue:0.0}",
                energySeries,
                energyRows),
            Disk = BuildSection(
                MetricKind.Disk,
                "Disk",
                "Process-level read and write throughput, with mounted volume totals.",
                $"{diskRows.Count} processes · {MonitorFormatters.FormatRate(diskSeries.CurrentValue)}",
                diskTop is null ? "No disk activity captured." : $"{diskTop.Name} is moving the most I/O.",
                MonitorFormatters.FormatRate(diskSeries.CurrentValue),
                MonitorFormatters.FormatRate(diskSeries.AverageValue),
                MonitorFormatters.FormatRate(diskSeries.PeakValue),
                diskSeries,
                diskRows),
            Network = BuildSection(
                MetricKind.Network,
                "Network",
                "Interface throughput and packet counts from getifaddrs.",
                $"{networkRows.Count} interfaces · {MonitorFormatters.FormatRate(networkSeries.CurrentValue)}",
                networkTop is null ? "No interface traffic captured." : $"{networkTop.Name} is carrying the peak traffic.",
                MonitorFormatters.FormatRate(networkSeries.CurrentValue),
                MonitorFormatters.FormatRate(networkSeries.AverageValue),
                MonitorFormatters.FormatRate(networkSeries.PeakValue),
                networkSeries,
                networkRows),
        };
    }

    private static MetricSectionSnapshot BuildSection(
        MetricKind kind,
        string title,
        string subtitle,
        string summaryText,
        string insightText,
        string currentValueText,
        string averageValueText,
        string peakValueText,
        MetricSeries series,
        IReadOnlyList<MonitorRowBase> rows)
    {
        return new MetricSectionSnapshot
        {
            Kind = kind,
            Title = title,
            Subtitle = subtitle,
            SummaryText = summaryText,
            InsightText = insightText,
            CurrentValueText = currentValueText,
            AverageValueText = averageValueText,
            PeakValueText = peakValueText,
            Series = series,
            Rows = rows,
        };
    }

    private CpuSummary SampleCpuLoad()
    {
        if (host_processor_info(mach_host_self(), ProcessorCpuLoadInfo, out var processorCount, out var processorInfoPtr, out var processorInfoCount) != 0)
            return new CpuSummary(0);

        try
        {
            var current = new int[processorInfoCount];
            Marshal.Copy(processorInfoPtr, current, 0, (int)processorInfoCount);

            var activeTicks = 0L;
            var totalTicks = 0L;

            if (_previousCpuLoad is not null && _previousCpuLoad.Length == current.Length)
            {
                for (var i = 0; i < current.Length; i += CpuStateCount)
                {
                    var user = current[i + CpuStateUser] - _previousCpuLoad[i + CpuStateUser];
                    var system = current[i + CpuStateSystem] - _previousCpuLoad[i + CpuStateSystem];
                    var nice = current[i + CpuStateNice] - _previousCpuLoad[i + CpuStateNice];
                    var idle = current[i + CpuStateIdle] - _previousCpuLoad[i + CpuStateIdle];

                    activeTicks += Math.Max(0, user) + Math.Max(0, system) + Math.Max(0, nice);
                    totalTicks += Math.Max(0, user) + Math.Max(0, system) + Math.Max(0, nice) + Math.Max(0, idle);
                }
            }

            _previousCpuLoad = current.Select(x => (long)x).ToArray();
            var activePercent = totalTicks == 0 ? 0 : activeTicks / (double)totalTicks * 100;
            return new CpuSummary(activePercent);
        }
        finally
        {
            _ = vm_deallocate(mach_task_self(), processorInfoPtr, (IntPtr)(processorInfoCount * sizeof(int)));
        }
    }

    private MemorySummary SampleMemory()
    {
        var vmStats = new VmStatistics64();
        var count = (uint)(Marshal.SizeOf<VmStatistics64>() / sizeof(int));

        if (host_statistics64(mach_host_self(), HostVmInfo64, ref vmStats, ref count) != 0)
            return new MemorySummary(0, 0);

        var pageSize = ReadUnsignedLong("hw.pagesize");
        var freeBytes = (ulong)(vmStats.FreeCount + vmStats.SpeculativeCount) * pageSize;
        var usedBytes = _physicalMemoryBytes > freeBytes ? _physicalMemoryBytes - freeBytes : 0;
        return new MemorySummary(usedBytes, (ulong)vmStats.CompressorPageCount * pageSize);
    }

    private VolumeSummary SampleVolumes()
    {
        var count = getfsstat(IntPtr.Zero, 0, MntNowait);
        if (count <= 0)
            return new VolumeSummary(0, 0);

        var entrySize = Marshal.SizeOf<StatFs>();
        var buffer = Marshal.AllocHGlobal(count * entrySize);

        try
        {
            var actualCount = getfsstat(buffer, count * entrySize, MntNowait);
            ulong totalBytes = 0;
            ulong usedBytes = 0;

            for (var i = 0; i < actualCount; ++i)
            {
                var stat = Marshal.PtrToStructure<StatFs>(buffer + (i * entrySize));
                var bytesTotal = stat.Blocks * stat.BlockSize;
                var bytesFree = stat.BlockAvailable * stat.BlockSize;
                totalBytes += bytesTotal;
                usedBytes += bytesTotal > bytesFree ? bytesTotal - bytesFree : 0;
            }

            return new VolumeSummary(totalBytes, usedBytes);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private List<NetworkMonitorRow> SampleInterfaces(double elapsedSeconds)
    {
        var rows = new List<NetworkMonitorRow>();
        if (getifaddrs(out var ifap) != 0 || ifap == IntPtr.Zero)
            return rows;

        var seen = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            for (var current = ifap; current != IntPtr.Zero; current = Marshal.PtrToStructure<IfAddrs>(current).Next)
            {
                var ifa = Marshal.PtrToStructure<IfAddrs>(current);
                if (ifa.Address == IntPtr.Zero || ifa.Data == IntPtr.Zero || ifa.Name == IntPtr.Zero)
                    continue;

                var address = Marshal.PtrToStructure<SockAddr>(ifa.Address);
                if (address.Family != AfLink)
                    continue;

                var name = Marshal.PtrToStringAnsi(ifa.Name);
                if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                    continue;

                var data = Marshal.PtrToStructure<IfData>(ifa.Data);
                var received = data.InputBytes;
                var sent = data.OutputBytes;

                _previousNetworkCounters.TryGetValue(name, out var previous);
                var rxPerSecond = previous is null ? 0 : Math.Max(0, received - previous.InputBytes) / elapsedSeconds;
                var txPerSecond = previous is null ? 0 : Math.Max(0, sent - previous.OutputBytes) / elapsedSeconds;

                _previousNetworkCounters[name] = new NetworkCounters(received, sent);

                rows.Add(new NetworkMonitorRow(
                    stableId: name,
                    name: DescribeInterfaceName(name),
                    secondaryText: BuildInterfaceSubtitle(name, data.Mtu, ifa.Flags),
                    badgeText: DescribeInterfaceBadge(name, ifa.Flags),
                    receivedBytesPerSecond: rxPerSecond,
                    sentBytesPerSecond: txPerSecond,
                    packetsIn: data.InputPackets,
                    packetsOut: data.OutputPackets));
            }
        }
        finally
        {
            freeifaddrs(ifap);
        }

        return rows;
    }

    private ProcessSnapshot SampleProcesses(double elapsedSeconds, CancellationToken cancellationToken)
    {
        var processes = proc_listallpids(IntPtr.Zero, 0);
        if (processes <= 0)
            return new ProcessSnapshot([], [], [], []);

        var buffer = Marshal.AllocHGlobal(processes * sizeof(int));
        var activeKeys = new HashSet<int>();

        try
        {
            var bytes = proc_listallpids(buffer, processes * sizeof(int));
            if (bytes <= 0)
                return new ProcessSnapshot([], [], [], []);

            var count = bytes / sizeof(int);
            var pids = new int[count];
            Marshal.Copy(buffer, pids, 0, count);

            var cpuRows = new List<CpuMonitorRow>();
            var memoryRows = new List<MemoryMonitorRow>();
            var energyRows = new List<EnergyMonitorRow>();
            var diskRows = new List<DiskMonitorRow>();

            foreach (var pid in pids)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (pid <= 0)
                    continue;

                var info = new ProcTaskAllInfo();
                if (proc_pidinfo(pid, ProcPidTaskAllInfo, 0, ref info, Marshal.SizeOf<ProcTaskAllInfo>()) <= 0)
                    continue;

                var usage = new RUsageInfoV6();
                if (proc_pid_rusage(pid, RusageInfoCurrent, ref usage) != 0)
                    continue;

                var name = GetProcessName(info.BsdInfo.Name, info.BsdInfo.Command);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                activeKeys.Add(pid);
                _previousProcessCounters.TryGetValue(pid, out var previous);

                var totalCpuTime = usage.UserTime + usage.SystemTime;
                var cpuDelta = previous is null ? 0 : Math.Max(0, totalCpuTime - previous.CpuTime);
                var cpuPercent = cpuDelta / (elapsedSeconds * 10_000_000d);
                var userDelta = previous is null ? 0 : Math.Max(0, usage.UserTime - previous.UserTime);
                var userPercent = userDelta / (elapsedSeconds * 10_000_000d);

                var idleWakeups = previous is null ? 0 : Math.Max(0, usage.PackageIdleWakeups - previous.PackageIdleWakeups) / elapsedSeconds;
                var interruptWakeups = previous is null ? 0 : Math.Max(0, usage.InterruptWakeups - previous.InterruptWakeups) / elapsedSeconds;
                var readPerSecond = previous is null ? 0 : Math.Max(0, usage.DiskReadBytes - previous.ReadBytes) / elapsedSeconds;
                var writePerSecond = previous is null ? 0 : Math.Max(0, usage.DiskWriteBytes - previous.WriteBytes) / elapsedSeconds;
                var energyPerSecond = previous is null ? 0 : Math.Max(0, usage.EnergyNanoJoules - previous.EnergyNanoJoules) / elapsedSeconds;
                var energyScore = (energyPerSecond / 15_000_000d) + (idleWakeups * 0.03) + (interruptWakeups * 0.05);

                _previousProcessCounters[pid] = new ProcessCounters(
                    totalCpuTime,
                    usage.UserTime,
                    usage.DiskReadBytes,
                    usage.DiskWriteBytes,
                    usage.EnergyNanoJoules,
                    usage.PackageIdleWakeups,
                    usage.InterruptWakeups);

                var badge = info.BsdInfo.UserId == 0 ? "system" : string.Empty;
                var subtitle = MonitorFormatters.BuildProcessSubtitle(pid, info.TaskInfo.ThreadCount);

                cpuRows.Add(new CpuMonitorRow(
                    pid.ToString(),
                    name,
                    subtitle,
                    badge,
                    pid,
                    cpuPercent,
                    userPercent,
                    info.TaskInfo.ThreadCount,
                    idleWakeups));

                memoryRows.Add(new MemoryMonitorRow(
                    pid.ToString(),
                    name,
                    subtitle,
                    badge,
                    pid,
                    usage.PhysicalFootprint,
                    usage.ResidentSize,
                    usage.WiredSize,
                    usage.PageIns));

                energyRows.Add(new EnergyMonitorRow(
                    pid.ToString(),
                    name,
                    subtitle,
                    badge,
                    pid,
                    energyScore,
                    energyPerSecond,
                    idleWakeups,
                    interruptWakeups));

                diskRows.Add(new DiskMonitorRow(
                    pid.ToString(),
                    name,
                    MonitorFormatters.BuildProcessSubtitle(pid, info.TaskInfo.ThreadCount, "disk active"),
                    badge,
                    pid,
                    readPerSecond,
                    writePerSecond,
                    usage.DiskReadBytes,
                    usage.DiskWriteBytes));
            }

            var staleKeys = _previousProcessCounters.Keys.Where(x => !activeKeys.Contains(x)).ToArray();
            foreach (var stale in staleKeys)
                _previousProcessCounters.Remove(stale);

            return new ProcessSnapshot(cpuRows, memoryRows, energyRows, diskRows);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string GetProcessName(byte[] name, byte[] command)
    {
        var registered = ReadNullTerminated(command);
        if (!string.IsNullOrWhiteSpace(registered))
            return registered;

        return ReadNullTerminated(name);
    }

    private static string ReadNullTerminated(byte[] buffer)
    {
        var end = Array.IndexOf(buffer, (byte)0);
        var length = end >= 0 ? end : buffer.Length;
        return Encoding.UTF8.GetString(buffer, 0, length).Trim();
    }

    private static ulong ReadUnsignedLong(string name)
    {
        nuint length = sizeof(ulong);
        var ptr = Marshal.AllocHGlobal((int)length);

        try
        {
            if (sysctlbyname(name, ptr, ref length, IntPtr.Zero, 0) != 0)
                return 0;

            return (ulong)Marshal.ReadInt64(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static string DescribeInterfaceName(string name) => name switch
    {
        "en0" => "Wi-Fi",
        "en1" => "Ethernet",
        var n when n.StartsWith("utun", StringComparison.Ordinal) => "VPN Tunnel",
        var n when n.StartsWith("awdl", StringComparison.Ordinal) => "AWDL / AirDrop",
        "lo0" => "Loopback",
        _ => name,
    };

    private static string DescribeInterfaceBadge(string name, uint flags)
    {
        if ((flags & IffLoopback) != 0)
            return "local";
        if (name.StartsWith("utun", StringComparison.Ordinal))
            return "vpn";
        if (name.StartsWith("en", StringComparison.Ordinal))
            return "primary";
        return string.Empty;
    }

    private static string BuildInterfaceSubtitle(string name, uint mtu, uint flags)
    {
        var state = (flags & IffRunning) != 0 ? "running" : "idle";
        return $"{name} · MTU {mtu} · {state}";
    }

    [DllImport("/usr/lib/libproc.dylib")]
    private static extern int proc_listallpids(IntPtr buffer, int bufferSize);

    [DllImport("/usr/lib/libproc.dylib")]
    private static extern int proc_pidinfo(int pid, int flavor, ulong arg, ref ProcTaskAllInfo buffer, int bufferSize);

    [DllImport("/usr/lib/libproc.dylib")]
    private static extern int proc_pid_rusage(int pid, int flavor, ref RUsageInfoV6 buffer);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern IntPtr mach_host_self();

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern IntPtr mach_task_self();

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern int host_processor_info(
        IntPtr host,
        int flavor,
        out uint outProcessorCount,
        out IntPtr outProcessorInfo,
        out uint outProcessorInfoCount);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern int host_statistics64(
        IntPtr host,
        int flavor,
        ref VmStatistics64 hostInfo,
        ref uint hostInfoCount);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern int vm_deallocate(IntPtr task, IntPtr address, IntPtr size);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern int getifaddrs(out IntPtr ifap);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern void freeifaddrs(IntPtr ifap);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern int getfsstat(IntPtr buffer, int bufferSize, int flags);

    [DllImport("/usr/lib/libSystem.B.dylib", CharSet = CharSet.Ansi)]
    private static extern int sysctlbyname(string name, IntPtr oldp, ref nuint oldlenp, IntPtr newp, nuint newlen);

    private sealed record CpuSummary(double ActivePercent);
    private sealed record MemorySummary(ulong UsedBytes, ulong CompressedBytes);
    private sealed record VolumeSummary(ulong TotalBytes, ulong UsedBytes);
    private sealed record ProcessCounters(
        ulong CpuTime,
        ulong UserTime,
        ulong ReadBytes,
        ulong WriteBytes,
        ulong EnergyNanoJoules,
        ulong PackageIdleWakeups,
        ulong InterruptWakeups);
    private sealed record NetworkCounters(ulong InputBytes, ulong OutputBytes);
    private sealed record ProcessSnapshot(
        IReadOnlyList<CpuMonitorRow> CpuRows,
        IReadOnlyList<MemoryMonitorRow> MemoryRows,
        IReadOnlyList<EnergyMonitorRow> EnergyRows,
        IReadOnlyList<DiskMonitorRow> DiskRows);

    [StructLayout(LayoutKind.Sequential)]
    private struct SockAddr
    {
        public byte Length;
        public byte Family;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IfAddrs
    {
        public IntPtr Next;
        public IntPtr Name;
        public uint Flags;
        public IntPtr Address;
        public IntPtr Netmask;
        public IntPtr DestinationAddress;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct IfData
    {
        public byte Type;
        public byte TypeLength;
        public byte Physical;
        public byte AddressLength;
        public byte HeaderLength;
        public byte ReceiveQuota;
        public byte TransmitQuota;
        public byte Unused1;
        public uint Mtu;
        public uint Metric;
        public uint BaudRate;
        public uint InputPackets;
        public uint InputErrors;
        public uint OutputPackets;
        public uint OutputErrors;
        public uint Collisions;
        public uint InputBytes;
        public uint OutputBytes;
        public uint InputMulticast;
        public uint OutputMulticast;
        public uint InputDrops;
        public uint NoProtocol;
        public uint ReceiveTiming;
        public uint TransmitTiming;
        public TimeVal LastChange;
        public uint Unused2;
        public uint HardwareAssist;
        public uint Reserved1;
        public uint Reserved2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TimeVal
    {
        public long Seconds;
        public int Microseconds;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct StatFs
    {
        public uint BlockSize;
        public int IoSize;
        public ulong Blocks;
        public ulong BlockFree;
        public ulong BlockAvailable;
        public ulong Files;
        public ulong FilesFree;
        public FsId FsId;
        public uint Owner;
        public uint Type;
        public uint Flags;
        public uint SubType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MfsTypeNameLen)]
        public string FileSystemTypeName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPathLen)]
        public string MountOnName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPathLen)]
        public string MountFromName;
        public uint ExtendedFlags;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public uint[] Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FsId
    {
        public int Value0;
        public int Value1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VmStatistics64
    {
        public uint FreeCount;
        public uint ActiveCount;
        public uint InactiveCount;
        public uint WireCount;
        public ulong ZeroFillCount;
        public ulong Reactivations;
        public ulong PageIns;
        public ulong PageOuts;
        public ulong Faults;
        public ulong CopyOnWriteFaults;
        public ulong Lookups;
        public ulong Hits;
        public ulong Purges;
        public uint PurgeableCount;
        public uint SpeculativeCount;
        public ulong Decompressions;
        public ulong Compressions;
        public ulong SwapIns;
        public ulong SwapOuts;
        public uint CompressorPageCount;
        public uint ThrottledCount;
        public uint ExternalPageCount;
        public uint InternalPageCount;
        public ulong TotalUncompressedPagesInCompressor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcTaskAllInfo
    {
        public ProcBsdInfo BsdInfo;
        public ProcTaskInfo TaskInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcBsdInfo
    {
        public uint Flags;
        public uint Status;
        public uint ExitStatus;
        public uint ProcessId;
        public uint ParentProcessId;
        public uint UserId;
        public uint GroupId;
        public uint RealUserId;
        public uint RealGroupId;
        public uint SavedUserId;
        public uint SavedGroupId;
        public uint Reserved1;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxComLen)]
        public byte[] Command;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxComLen * 2)]
        public byte[] Name;
        public uint FileCount;
        public uint ProcessGroupId;
        public uint JobControlCount;
        public uint TerminalDevice;
        public uint TerminalProcessGroupId;
        public int Nice;
        public ulong StartTimeSeconds;
        public ulong StartTimeMicroseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcTaskInfo
    {
        public ulong VirtualSize;
        public ulong ResidentSize;
        public ulong TotalUser;
        public ulong TotalSystem;
        public ulong ThreadsUser;
        public ulong ThreadsSystem;
        public int Policy;
        public int Faults;
        public int PageIns;
        public int CopyOnWriteFaults;
        public int MessagesSent;
        public int MessagesReceived;
        public int MachSystemCalls;
        public int UnixSystemCalls;
        public int ContextSwitches;
        public int ThreadCount;
        public int RunningThreadCount;
        public int Priority;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RUsageInfoV6
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Uuid;
        public ulong UserTime;
        public ulong SystemTime;
        public ulong PackageIdleWakeups;
        public ulong InterruptWakeups;
        public ulong PageIns;
        public ulong WiredSize;
        public ulong ResidentSize;
        public ulong PhysicalFootprint;
        public ulong ProcessStartAbsoluteTime;
        public ulong ProcessExitAbsoluteTime;
        public ulong ChildUserTime;
        public ulong ChildSystemTime;
        public ulong ChildPackageIdleWakeups;
        public ulong ChildInterruptWakeups;
        public ulong ChildPageIns;
        public ulong ChildElapsedAbsoluteTime;
        public ulong DiskReadBytes;
        public ulong DiskWriteBytes;
        public ulong CpuTimeQosDefault;
        public ulong CpuTimeQosMaintenance;
        public ulong CpuTimeQosBackground;
        public ulong CpuTimeQosUtility;
        public ulong CpuTimeQosLegacy;
        public ulong CpuTimeQosUserInitiated;
        public ulong CpuTimeQosUserInteractive;
        public ulong BilledSystemTime;
        public ulong ServicedSystemTime;
        public ulong LogicalWrites;
        public ulong LifetimeMaxPhysicalFootprint;
        public ulong Instructions;
        public ulong Cycles;
        public ulong BilledEnergy;
        public ulong ServicedEnergy;
        public ulong IntervalMaxPhysicalFootprint;
        public ulong RunnableTime;
        public ulong Flags;
        public ulong UserPTime;
        public ulong SystemPTime;
        public ulong PInstructions;
        public ulong PCycles;
        public ulong EnergyNanoJoules;
        public ulong ProcessEnergyNanoJoules;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
        public ulong[] Reserved;
    }
}
