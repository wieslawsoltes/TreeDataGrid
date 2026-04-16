using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridUnoActivityMonitor.Models;
using TreeDataGridUnoActivityMonitor.Services;
using TreeDataGridUnoActivityMonitor.ViewModels;

namespace TreeDataGridUnoActivityMonitor;

public sealed partial class MainPage : Page
{
    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1),
    };

    private bool _refreshInFlight;

    public MainPage()
    {
        ViewModel = new MonitorShellViewModel(MonitorTelemetryProviderFactory.Create());
        InitializeComponent();
        DataContext = ViewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _refreshTimer.Tick += OnRefreshTimerTick;
    }

    internal MonitorShellViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Start();
        await RefreshAsync();
        StartLivePulse();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        ViewModel.Dispose();
    }

    private async void OnRefreshTimerTick(object? sender, object e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_refreshInFlight)
            return;

        _refreshInFlight = true;

        try
        {
            await ViewModel.RefreshAsync();
        }
        finally
        {
            _refreshInFlight = false;
        }
    }

    private void CpuSectionClick(object sender, RoutedEventArgs e) => ViewModel.SelectMetric(MetricKind.Cpu);
    private void MemorySectionClick(object sender, RoutedEventArgs e) => ViewModel.SelectMetric(MetricKind.Memory);
    private void EnergySectionClick(object sender, RoutedEventArgs e) => ViewModel.SelectMetric(MetricKind.Energy);
    private void DiskSectionClick(object sender, RoutedEventArgs e) => ViewModel.SelectMetric(MetricKind.Disk);
    private void NetworkSectionClick(object sender, RoutedEventArgs e) => ViewModel.SelectMetric(MetricKind.Network);

    private void StartLivePulse()
    {
        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();

        var opacity = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 1,
            To = 0.35,
            Duration = new Duration(TimeSpan.FromMilliseconds(950)),
            RepeatBehavior = Microsoft.UI.Xaml.Media.Animation.RepeatBehavior.Forever,
        };

        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(opacity, liveDot);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(opacity, nameof(Opacity));
        storyboard.Children.Add(opacity);
        storyboard.Begin();
    }
}
