using System;
using System.Threading;
using System.Threading.Tasks;
using TreeDataGridUnoActivityMonitor.Models;

namespace TreeDataGridUnoActivityMonitor.Services;

public interface IMonitorTelemetryProvider : IDisposable
{
    bool IsDemoMode { get; }

    Task<MonitorSnapshot> CaptureAsync(CancellationToken cancellationToken);
}
