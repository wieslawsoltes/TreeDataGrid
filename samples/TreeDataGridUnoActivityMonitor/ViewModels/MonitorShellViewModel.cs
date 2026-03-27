using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using TreeDataGridUnoActivityMonitor.Models;
using TreeDataGridUnoActivityMonitor.Services;
using Color = Windows.UI.Color;

namespace TreeDataGridUnoActivityMonitor.ViewModels;

internal sealed class MonitorShellViewModel : NotifyingBase, IDisposable
{
    private readonly IMonitorTelemetryProvider _provider;
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private MetricSectionViewModel _currentSection;
    private bool _refreshing;
    private string _hostName = "Waiting";
    private string _platformLabel = "Preparing telemetry";
    private string _modeLabel = "Connecting";
    private string _modeDescription = "The sample is collecting its first snapshot.";
    private string _cpuHeadline = "0%";
    private string _cpuDetail = string.Empty;
    private string _memoryHeadline = "0%";
    private string _memoryDetail = string.Empty;
    private string _energyHeadline = "0.0";
    private string _energyDetail = string.Empty;
    private string _diskHeadline = "0 B/s";
    private string _diskDetail = string.Empty;
    private string _networkHeadline = "0 B/s";
    private string _networkDetail = string.Empty;
    private string _timestampLabel = "--:--:--";
    private string _timestampDetail = "Awaiting first sample.";
    private string _notesText = string.Empty;

    public MonitorShellViewModel(IMonitorTelemetryProvider provider)
    {
        _provider = provider;
        CpuSection = new CpuSectionViewModel();
        MemorySection = new MemorySectionViewModel();
        EnergySection = new EnergySectionViewModel();
        DiskSection = new DiskSectionViewModel();
        NetworkSection = new NetworkSectionViewModel();
        _currentSection = CpuSection;
        UpdateSelectionState();
    }

    public CpuSectionViewModel CpuSection { get; }
    public MemorySectionViewModel MemorySection { get; }
    public EnergySectionViewModel EnergySection { get; }
    public DiskSectionViewModel DiskSection { get; }
    public NetworkSectionViewModel NetworkSection { get; }

    public MetricSectionViewModel CurrentSection
    {
        get => _currentSection;
        private set
        {
            if (RaiseAndSetIfChanged(ref _currentSection, value))
            {
                UpdateSelectionState();
                UpdateNotesText(_provider.IsDemoMode);
            }
        }
    }

    public string HostName
    {
        get => _hostName;
        private set => RaiseAndSetIfChanged(ref _hostName, value);
    }

    public string PlatformLabel
    {
        get => _platformLabel;
        private set => RaiseAndSetIfChanged(ref _platformLabel, value);
    }

    public string ModeLabel
    {
        get => _modeLabel;
        private set => RaiseAndSetIfChanged(ref _modeLabel, value);
    }

    public string ModeDescription
    {
        get => _modeDescription;
        private set => RaiseAndSetIfChanged(ref _modeDescription, value);
    }

    public string CpuHeadline
    {
        get => _cpuHeadline;
        private set => RaiseAndSetIfChanged(ref _cpuHeadline, value);
    }

    public string CpuDetail
    {
        get => _cpuDetail;
        private set => RaiseAndSetIfChanged(ref _cpuDetail, value);
    }

    public string MemoryHeadline
    {
        get => _memoryHeadline;
        private set => RaiseAndSetIfChanged(ref _memoryHeadline, value);
    }

    public string MemoryDetail
    {
        get => _memoryDetail;
        private set => RaiseAndSetIfChanged(ref _memoryDetail, value);
    }

    public string EnergyHeadline
    {
        get => _energyHeadline;
        private set => RaiseAndSetIfChanged(ref _energyHeadline, value);
    }

    public string EnergyDetail
    {
        get => _energyDetail;
        private set => RaiseAndSetIfChanged(ref _energyDetail, value);
    }

    public string DiskHeadline
    {
        get => _diskHeadline;
        private set => RaiseAndSetIfChanged(ref _diskHeadline, value);
    }

    public string DiskDetail
    {
        get => _diskDetail;
        private set => RaiseAndSetIfChanged(ref _diskDetail, value);
    }

    public string NetworkHeadline
    {
        get => _networkHeadline;
        private set => RaiseAndSetIfChanged(ref _networkHeadline, value);
    }

    public string NetworkDetail
    {
        get => _networkDetail;
        private set => RaiseAndSetIfChanged(ref _networkDetail, value);
    }

    public string TimestampLabel
    {
        get => _timestampLabel;
        private set => RaiseAndSetIfChanged(ref _timestampLabel, value);
    }

    public string TimestampDetail
    {
        get => _timestampDetail;
        private set => RaiseAndSetIfChanged(ref _timestampDetail, value);
    }

    public string NotesText
    {
        get => _notesText;
        private set => RaiseAndSetIfChanged(ref _notesText, value);
    }

    public bool IsCpuSelected => CurrentSection.Kind == MetricKind.Cpu;
    public bool IsMemorySelected => CurrentSection.Kind == MetricKind.Memory;
    public bool IsEnergySelected => CurrentSection.Kind == MetricKind.Energy;
    public bool IsDiskSelected => CurrentSection.Kind == MetricKind.Disk;
    public bool IsNetworkSelected => CurrentSection.Kind == MetricKind.Network;

    public Brush ModeAccentBrush => new SolidColorBrush(_provider.IsDemoMode
        ? ColorHelper.FromArgb(0xFF, 0xFF, 0xC8, 0x57)
        : ColorHelper.FromArgb(0xFF, 0x8C, 0xE9, 0x9A));

    public Brush CpuSectionButtonBackground => GetSectionBackground(MetricKind.Cpu, CpuSection.AccentColor);
    public Brush MemorySectionButtonBackground => GetSectionBackground(MetricKind.Memory, MemorySection.AccentColor);
    public Brush EnergySectionButtonBackground => GetSectionBackground(MetricKind.Energy, EnergySection.AccentColor);
    public Brush DiskSectionButtonBackground => GetSectionBackground(MetricKind.Disk, DiskSection.AccentColor);
    public Brush NetworkSectionButtonBackground => GetSectionBackground(MetricKind.Network, NetworkSection.AccentColor);
    public Brush CpuSectionButtonBorderBrush => GetSectionBorderBrush(MetricKind.Cpu, CpuSection.AccentColor);
    public Brush MemorySectionButtonBorderBrush => GetSectionBorderBrush(MetricKind.Memory, MemorySection.AccentColor);
    public Brush EnergySectionButtonBorderBrush => GetSectionBorderBrush(MetricKind.Energy, EnergySection.AccentColor);
    public Brush DiskSectionButtonBorderBrush => GetSectionBorderBrush(MetricKind.Disk, DiskSection.AccentColor);
    public Brush NetworkSectionButtonBorderBrush => GetSectionBorderBrush(MetricKind.Network, NetworkSection.AccentColor);

    public async Task RefreshAsync()
    {
        if (_refreshing)
            return;

        _refreshing = true;

        try
        {
            var snapshot = await _provider.CaptureAsync(_disposeTokenSource.Token);
            ApplySnapshot(snapshot);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _refreshing = false;
        }
    }

    public void SelectMetric(MetricKind kind)
    {
        CurrentSection = kind switch
        {
            MetricKind.Cpu => CpuSection,
            MetricKind.Memory => MemorySection,
            MetricKind.Energy => EnergySection,
            MetricKind.Disk => DiskSection,
            MetricKind.Network => NetworkSection,
            _ => CpuSection,
        };
    }

    public void Dispose()
    {
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
        _provider.Dispose();
    }

    private void ApplySnapshot(MonitorSnapshot snapshot)
    {
        HostName = snapshot.Summary.HostName;
        PlatformLabel = snapshot.Summary.PlatformLabel;
        ModeLabel = snapshot.Summary.IsDemoMode ? "Demo telemetry" : "Live native metrics";
        ModeDescription = snapshot.Summary.ModeDescription;
        CpuHeadline = snapshot.Summary.CpuHeadline;
        CpuDetail = snapshot.Summary.CpuDetail;
        MemoryHeadline = snapshot.Summary.MemoryHeadline;
        MemoryDetail = snapshot.Summary.MemoryDetail;
        EnergyHeadline = snapshot.Summary.EnergyHeadline;
        EnergyDetail = snapshot.Summary.EnergyDetail;
        DiskHeadline = snapshot.Summary.DiskHeadline;
        DiskDetail = snapshot.Summary.DiskDetail;
        NetworkHeadline = snapshot.Summary.NetworkHeadline;
        NetworkDetail = snapshot.Summary.NetworkDetail;
        TimestampLabel = MonitorFormatters.FormatTimestamp(snapshot.Summary.Timestamp);
        TimestampDetail = $"Updated {snapshot.Summary.Timestamp.ToLocalTime():MMM d, HH:mm:ss}";

        CpuSection.ApplySnapshot(snapshot.Cpu);
        MemorySection.ApplySnapshot(snapshot.Memory);
        EnergySection.ApplySnapshot(snapshot.Energy);
        DiskSection.ApplySnapshot(snapshot.Disk);
        NetworkSection.ApplySnapshot(snapshot.Network);
        UpdateNotesText(snapshot.Summary.IsDemoMode);
    }

    private Brush GetSectionBackground(MetricKind kind, Color accent)
    {
        if (CurrentSection.Kind != kind)
            return new SolidColorBrush(Colors.Transparent);

        return new SolidColorBrush(ColorHelper.FromArgb(0x28, accent.R, accent.G, accent.B));
    }

    private Brush GetSectionBorderBrush(MetricKind kind, Color accent)
    {
        if (CurrentSection.Kind != kind)
            return new SolidColorBrush(ColorHelper.FromArgb(0x00, 0, 0, 0));

        return new SolidColorBrush(ColorHelper.FromArgb(0x66, accent.R, accent.G, accent.B));
    }

    private void UpdateSelectionState()
    {
        RaisePropertyChanged(nameof(IsCpuSelected));
        RaisePropertyChanged(nameof(IsMemorySelected));
        RaisePropertyChanged(nameof(IsEnergySelected));
        RaisePropertyChanged(nameof(IsDiskSelected));
        RaisePropertyChanged(nameof(IsNetworkSelected));
        RaisePropertyChanged(nameof(CpuSectionButtonBackground));
        RaisePropertyChanged(nameof(MemorySectionButtonBackground));
        RaisePropertyChanged(nameof(EnergySectionButtonBackground));
        RaisePropertyChanged(nameof(DiskSectionButtonBackground));
        RaisePropertyChanged(nameof(NetworkSectionButtonBackground));
        RaisePropertyChanged(nameof(CpuSectionButtonBorderBrush));
        RaisePropertyChanged(nameof(MemorySectionButtonBorderBrush));
        RaisePropertyChanged(nameof(EnergySectionButtonBorderBrush));
        RaisePropertyChanged(nameof(DiskSectionButtonBorderBrush));
        RaisePropertyChanged(nameof(NetworkSectionButtonBorderBrush));
    }

    private void UpdateNotesText(bool isDemoMode)
    {
        NotesText = isDemoMode
            ? "This target is using the demo provider. The shell, TreeDataGrid configuration, filtering, sorting, and Skia charts match the live desktop experience."
            : CurrentSection.Kind == MetricKind.Energy
                ? "Energy uses public metrics only. The score is an estimate built from nanojoules and wakeups, not the private Activity Monitor composite."
                : "macOS desktop is sampling once per second. Selection is restored by stable row key while the underlying data refreshes.";
    }
}

public abstract class MetricSectionViewModel : NotifyingBase
{
    private string _searchText = string.Empty;
    private string _title;
    private string _subtitle;
    private string _summaryText = "Waiting for the first snapshot.";
    private string _insightText = "Telemetry is warming up.";
    private string _currentValueText = "0";
    private string _averageValueText = "0";
    private string _peakValueText = "0";
    private string _rowCountText = "0 rows";
    private string _selectionStatusText = "No row selected.";
    private string _inspectorTitle = "No selection";
    private string _inspectorSubtitle = "Choose a row to inspect.";
    private string _inspectorPrimaryLabel = "Primary";
    private string _inspectorPrimaryValue = "—";
    private string _inspectorSecondaryLabel = "Secondary";
    private string _inspectorSecondaryValue = "—";
    private string _inspectorTertiaryLabel = "Tertiary";
    private string _inspectorTertiaryValue = "—";
    private MetricSeries _trendSeries;

    protected MetricSectionViewModel(MetricKind kind, string title, Color accentColor)
    {
        Kind = kind;
        AccentColor = accentColor;
        _title = title;
        _subtitle = string.Empty;
        _trendSeries = MetricSeries.Empty(title, string.Empty);
    }

    public MetricKind Kind { get; }
    public Color AccentColor { get; }

    public string Title
    {
        get => _title;
        private set => RaiseAndSetIfChanged(ref _title, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        private set => RaiseAndSetIfChanged(ref _subtitle, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        protected set => RaiseAndSetIfChanged(ref _summaryText, value);
    }

    public string InsightText
    {
        get => _insightText;
        protected set => RaiseAndSetIfChanged(ref _insightText, value);
    }

    public string CurrentValueText
    {
        get => _currentValueText;
        protected set => RaiseAndSetIfChanged(ref _currentValueText, value);
    }

    public string AverageValueText
    {
        get => _averageValueText;
        protected set => RaiseAndSetIfChanged(ref _averageValueText, value);
    }

    public string PeakValueText
    {
        get => _peakValueText;
        protected set => RaiseAndSetIfChanged(ref _peakValueText, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (RaiseAndSetIfChanged(ref _searchText, value))
                OnSearchTextChanged();
        }
    }

    public string RowCountText
    {
        get => _rowCountText;
        protected set => RaiseAndSetIfChanged(ref _rowCountText, value);
    }

    public string SelectionStatusText
    {
        get => _selectionStatusText;
        protected set => RaiseAndSetIfChanged(ref _selectionStatusText, value);
    }

    public string InspectorTitle
    {
        get => _inspectorTitle;
        protected set => RaiseAndSetIfChanged(ref _inspectorTitle, value);
    }

    public string InspectorSubtitle
    {
        get => _inspectorSubtitle;
        protected set => RaiseAndSetIfChanged(ref _inspectorSubtitle, value);
    }

    public string InspectorPrimaryLabel
    {
        get => _inspectorPrimaryLabel;
        protected set => RaiseAndSetIfChanged(ref _inspectorPrimaryLabel, value);
    }

    public string InspectorPrimaryValue
    {
        get => _inspectorPrimaryValue;
        protected set => RaiseAndSetIfChanged(ref _inspectorPrimaryValue, value);
    }

    public string InspectorSecondaryLabel
    {
        get => _inspectorSecondaryLabel;
        protected set => RaiseAndSetIfChanged(ref _inspectorSecondaryLabel, value);
    }

    public string InspectorSecondaryValue
    {
        get => _inspectorSecondaryValue;
        protected set => RaiseAndSetIfChanged(ref _inspectorSecondaryValue, value);
    }

    public string InspectorTertiaryLabel
    {
        get => _inspectorTertiaryLabel;
        protected set => RaiseAndSetIfChanged(ref _inspectorTertiaryLabel, value);
    }

    public string InspectorTertiaryValue
    {
        get => _inspectorTertiaryValue;
        protected set => RaiseAndSetIfChanged(ref _inspectorTertiaryValue, value);
    }

    public MetricSeries TrendSeries
    {
        get => _trendSeries;
        protected set => RaiseAndSetIfChanged(ref _trendSeries, value);
    }

    public abstract ITreeDataGridSource Source { get; }

    public void ApplySnapshot(MetricSectionSnapshot snapshot)
    {
        Title = snapshot.Title;
        Subtitle = snapshot.Subtitle;
        SummaryText = snapshot.SummaryText;
        InsightText = snapshot.InsightText;
        CurrentValueText = snapshot.CurrentValueText;
        AverageValueText = snapshot.AverageValueText;
        PeakValueText = snapshot.PeakValueText;
        TrendSeries = snapshot.Series;
        ApplyRows(snapshot.Rows);
    }

    protected abstract void ApplyRows(IReadOnlyList<MonitorRowBase> rows);
    protected abstract void OnSearchTextChanged();
}

internal abstract partial class MetricSectionViewModel<TRow> : MetricSectionViewModel
    where TRow : MonitorRowBase
{
    private readonly TreeDataGridRowSelectionModel<TRow> _selection;
    private List<TRow> _allRows = [];
    private string? _selectedKey;

    protected MetricSectionViewModel(MetricKind kind, string title, Color accentColor)
        : base(kind, title, accentColor)
    {
        TypedSource = new FlatTreeDataGridSource<TRow>(Array.Empty<TRow>());
        ConfigureColumns(TypedSource.Columns);

        _selection = new TreeDataGridRowSelectionModel<TRow>(TypedSource)
        {
            SingleSelect = true,
        };
        _selection.SelectionChanged += OnSelectionChanged;
        TypedSource.Selection = _selection;
    }

    protected FlatTreeDataGridSource<TRow> TypedSource { get; }
    public override ITreeDataGridSource Source => TypedSource;

    protected override void ApplyRows(IReadOnlyList<MonitorRowBase> rows)
    {
        _allRows = rows.OfType<TRow>().ToList();
        ApplyFilterAndSelection();
    }

    protected override void OnSearchTextChanged()
    {
        ApplyFilterAndSelection();
    }

    protected abstract void ConfigureColumns(ColumnList<TRow> columns);
    protected abstract void UpdateInspector(TRow? row);

    protected virtual IEnumerable<TRow> FilterRows(IEnumerable<TRow> rows)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return rows;

        return rows.Where(x => x.SearchText.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyFilterAndSelection()
    {
        var filtered = FilterRows(_allRows).ToList();
        TypedSource.Items = filtered;

        if (filtered.Count == 0)
        {
            _selection.Clear();
            RowCountText = "0 rows";
            SelectionStatusText = "No rows match the current filter.";
            UpdateInspector(null);
            return;
        }

        var targetIndex = 0;

        if (!string.IsNullOrWhiteSpace(_selectedKey))
        {
            var existingIndex = filtered.FindIndex(x => x.StableId == _selectedKey);
            if (existingIndex >= 0)
                targetIndex = existingIndex;
        }

        _selection.SelectedIndex = new IndexPath(targetIndex);
        RowCountText = $"{filtered.Count.ToString("0", CultureInfo.InvariantCulture)} rows";
        SelectionStatusText = $"Selected {(filtered[targetIndex].Name)}";
        UpdateInspector(filtered[targetIndex]);
    }

    private void OnSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<TRow> e)
    {
        var selected = _selection.SelectedItem;
        _selectedKey = selected?.StableId;
        SelectionStatusText = selected is null ? "No row selected." : $"Selected {selected.Name}";
        UpdateInspector(selected);
    }
}

internal sealed class CpuSectionViewModel : MetricSectionViewModel<CpuMonitorRow>
{
    public CpuSectionViewModel()
        : base(MetricKind.Cpu, "CPU", ColorHelper.FromArgb(0xFF, 0x5E, 0xD4, 0xFF))
    {
    }

    protected override void ConfigureColumns(ColumnList<CpuMonitorRow> columns)
    {
        columns.Add(new TemplateColumn<CpuMonitorRow>(
            "Process",
            "IdentityCell",
            width: new GridLength(5, GridUnitType.Star),
            options: new TemplateColumnOptions<CpuMonitorRow>
            {
                CompareAscending = CompareAscending(x => x.Name),
                CompareDescending = CompareDescending(x => x.Name),
                IsTextSearchEnabled = true,
                TextSearchValueSelector = x => x.SearchText,
            }));

        columns.Add(CreateTextColumn("CPU %", x => x.CpuPercentText, x => x.CpuPercent, 1.4));
        columns.Add(CreateTextColumn("User %", x => x.UserPercentText, x => x.UserPercent, 1.2));
        columns.Add(CreateTextColumn("Threads", x => x.ThreadCountText, x => x.ThreadCount, 1.1));
        columns.Add(CreateTextColumn("Idle", x => x.IdleWakeupsText, x => x.IdleWakeupsPerSecond, 1.2));
    }

    protected override void UpdateInspector(CpuMonitorRow? row)
    {
        InspectorTitle = row?.Name ?? "No process selected";
        InspectorSubtitle = row?.SecondaryText ?? "Select a process row to inspect.";
        InspectorPrimaryLabel = "CPU";
        InspectorPrimaryValue = row?.CpuPercentText is null ? "—" : $"{row.CpuPercentText}%";
        InspectorSecondaryLabel = "User";
        InspectorSecondaryValue = row?.UserPercentText is null ? "—" : $"{row.UserPercentText}%";
        InspectorTertiaryLabel = "Threads";
        InspectorTertiaryValue = row?.ThreadCountText ?? "—";
    }
}

internal sealed class MemorySectionViewModel : MetricSectionViewModel<MemoryMonitorRow>
{
    public MemorySectionViewModel()
        : base(MetricKind.Memory, "Memory", ColorHelper.FromArgb(0xFF, 0xFF, 0xC8, 0x57))
    {
    }

    protected override void ConfigureColumns(ColumnList<MemoryMonitorRow> columns)
    {
        columns.Add(new TemplateColumn<MemoryMonitorRow>(
            "Process",
            "IdentityCell",
            width: new GridLength(5, GridUnitType.Star),
            options: new TemplateColumnOptions<MemoryMonitorRow>
            {
                CompareAscending = CompareAscending(x => x.Name),
                CompareDescending = CompareDescending(x => x.Name),
                IsTextSearchEnabled = true,
                TextSearchValueSelector = x => x.SearchText,
            }));

        columns.Add(CreateTextColumn("Footprint", x => x.FootprintText, x => x.FootprintBytes, 1.4));
        columns.Add(CreateTextColumn("Resident", x => x.ResidentText, x => x.ResidentBytes, 1.3));
        columns.Add(CreateTextColumn("Compressed", x => x.CompressedText, x => x.CompressedBytes, 1.3));
        columns.Add(CreateTextColumn("Pageins", x => x.PageInsText, x => x.PageIns, 1.1));
    }

    protected override void UpdateInspector(MemoryMonitorRow? row)
    {
        InspectorTitle = row?.Name ?? "No process selected";
        InspectorSubtitle = row?.SecondaryText ?? "Select a process row to inspect.";
        InspectorPrimaryLabel = "Footprint";
        InspectorPrimaryValue = row?.FootprintText ?? "—";
        InspectorSecondaryLabel = "Resident";
        InspectorSecondaryValue = row?.ResidentText ?? "—";
        InspectorTertiaryLabel = "Compressed";
        InspectorTertiaryValue = row?.CompressedText ?? "—";
    }
}

internal sealed class EnergySectionViewModel : MetricSectionViewModel<EnergyMonitorRow>
{
    public EnergySectionViewModel()
        : base(MetricKind.Energy, "Energy", ColorHelper.FromArgb(0xFF, 0x8C, 0xE9, 0x9A))
    {
    }

    protected override void ConfigureColumns(ColumnList<EnergyMonitorRow> columns)
    {
        columns.Add(new TemplateColumn<EnergyMonitorRow>(
            "Process",
            "IdentityCell",
            width: new GridLength(5, GridUnitType.Star),
            options: new TemplateColumnOptions<EnergyMonitorRow>
            {
                CompareAscending = CompareAscending(x => x.Name),
                CompareDescending = CompareDescending(x => x.Name),
                IsTextSearchEnabled = true,
                TextSearchValueSelector = x => x.SearchText,
            }));

        columns.Add(CreateTextColumn("Score", x => x.ScoreText, x => x.Score, 1.2));
        columns.Add(CreateTextColumn("Energy", x => x.EnergyText, x => x.NanoJoulesPerSecond, 1.5));
        columns.Add(CreateTextColumn("Idle", x => x.IdleWakeupsText, x => x.IdleWakeupsPerSecond, 1.2));
        columns.Add(CreateTextColumn("Interrupts", x => x.InterruptWakeupsText, x => x.InterruptWakeupsPerSecond, 1.3));
    }

    protected override void UpdateInspector(EnergyMonitorRow? row)
    {
        InspectorTitle = row?.Name ?? "No process selected";
        InspectorSubtitle = row?.SecondaryText ?? "Select a process row to inspect.";
        InspectorPrimaryLabel = "Score";
        InspectorPrimaryValue = row?.ScoreText ?? "—";
        InspectorSecondaryLabel = "Energy";
        InspectorSecondaryValue = row?.EnergyText ?? "—";
        InspectorTertiaryLabel = "Wakeups";
        InspectorTertiaryValue = row is null ? "—" : $"{row.IdleWakeupsText} / {row.InterruptWakeupsText}";
    }
}

internal sealed class DiskSectionViewModel : MetricSectionViewModel<DiskMonitorRow>
{
    public DiskSectionViewModel()
        : base(MetricKind.Disk, "Disk", ColorHelper.FromArgb(0xFF, 0xFF, 0x8F, 0x6B))
    {
    }

    protected override void ConfigureColumns(ColumnList<DiskMonitorRow> columns)
    {
        columns.Add(new TemplateColumn<DiskMonitorRow>(
            "Process",
            "IdentityCell",
            width: new GridLength(5, GridUnitType.Star),
            options: new TemplateColumnOptions<DiskMonitorRow>
            {
                CompareAscending = CompareAscending(x => x.Name),
                CompareDescending = CompareDescending(x => x.Name),
                IsTextSearchEnabled = true,
                TextSearchValueSelector = x => x.SearchText,
            }));

        columns.Add(CreateTextColumn("Read/s", x => x.ReadRateText, x => x.ReadBytesPerSecond, 1.3));
        columns.Add(CreateTextColumn("Write/s", x => x.WriteRateText, x => x.WriteBytesPerSecond, 1.3));
        columns.Add(CreateTextColumn("Read Total", x => x.TotalReadText, x => x.TotalReadBytes, 1.3));
        columns.Add(CreateTextColumn("Write Total", x => x.TotalWriteText, x => x.TotalWriteBytes, 1.3));
    }

    protected override void UpdateInspector(DiskMonitorRow? row)
    {
        InspectorTitle = row?.Name ?? "No process selected";
        InspectorSubtitle = row?.SecondaryText ?? "Select a process row to inspect.";
        InspectorPrimaryLabel = "Read/s";
        InspectorPrimaryValue = row?.ReadRateText ?? "—";
        InspectorSecondaryLabel = "Write/s";
        InspectorSecondaryValue = row?.WriteRateText ?? "—";
        InspectorTertiaryLabel = "Totals";
        InspectorTertiaryValue = row is null ? "—" : $"{row.TotalReadText} / {row.TotalWriteText}";
    }
}

internal sealed class NetworkSectionViewModel : MetricSectionViewModel<NetworkMonitorRow>
{
    public NetworkSectionViewModel()
        : base(MetricKind.Network, "Network", ColorHelper.FromArgb(0xFF, 0x7C, 0x9C, 0xFF))
    {
    }

    protected override void ConfigureColumns(ColumnList<NetworkMonitorRow> columns)
    {
        columns.Add(new TemplateColumn<NetworkMonitorRow>(
            "Interface",
            "IdentityCell",
            width: new GridLength(5, GridUnitType.Star),
            options: new TemplateColumnOptions<NetworkMonitorRow>
            {
                CompareAscending = CompareAscending(x => x.Name),
                CompareDescending = CompareDescending(x => x.Name),
                IsTextSearchEnabled = true,
                TextSearchValueSelector = x => x.SearchText,
            }));

        columns.Add(CreateTextColumn("Receive/s", x => x.ReceiveRateText, x => x.ReceivedBytesPerSecond, 1.3));
        columns.Add(CreateTextColumn("Send/s", x => x.SendRateText, x => x.SentBytesPerSecond, 1.3));
        columns.Add(CreateTextColumn("Packets In", x => x.PacketsInText, x => x.PacketsIn, 1.2));
        columns.Add(CreateTextColumn("Packets Out", x => x.PacketsOutText, x => x.PacketsOut, 1.2));
    }

    protected override void UpdateInspector(NetworkMonitorRow? row)
    {
        InspectorTitle = row?.Name ?? "No interface selected";
        InspectorSubtitle = row?.SecondaryText ?? "Select an interface row to inspect.";
        InspectorPrimaryLabel = "Receive";
        InspectorPrimaryValue = row?.ReceiveRateText ?? "—";
        InspectorSecondaryLabel = "Send";
        InspectorSecondaryValue = row?.SendRateText ?? "—";
        InspectorTertiaryLabel = "Packets";
        InspectorTertiaryValue = row is null ? "—" : $"{row.PacketsInText} / {row.PacketsOutText}";
    }
}

internal abstract partial class MetricSectionViewModel<TRow>
{
    protected static TextColumn<TRow, string> CreateTextColumn<TValue>(
        string header,
        System.Linq.Expressions.Expression<Func<TRow, string?>> selector,
        Func<TRow, TValue> sortValue,
        double widthInStars)
    {
        return new TextColumn<TRow, string>(
            header,
            selector,
            width: new GridLength(widthInStars, GridUnitType.Star),
            options: new TextColumnOptions<TRow>
            {
                TextAlignment = Avalonia.Media.TextAlignment.Right,
                CompareAscending = CompareAscending(sortValue),
                CompareDescending = CompareDescending(sortValue),
            });
    }

    protected static Comparison<TRow?> CompareAscending<TValue>(Func<TRow, TValue> selector)
    {
        return (x, y) =>
        {
            if (x is null && y is null)
                return 0;
            if (x is null)
                return -1;
            if (y is null)
                return 1;

            return Comparer<TValue>.Default.Compare(selector(x), selector(y));
        };
    }

    protected static Comparison<TRow?> CompareDescending<TValue>(Func<TRow, TValue> selector)
    {
        return (x, y) =>
        {
            if (x is null && y is null)
                return 0;
            if (x is null)
                return 1;
            if (y is null)
                return -1;

            return Comparer<TValue>.Default.Compare(selector(y), selector(x));
        };
    }
}
