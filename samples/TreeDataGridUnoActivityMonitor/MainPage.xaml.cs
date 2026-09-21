using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using TreeDataGridUnoActivityMonitor.Models;
using TreeDataGridUnoActivityMonitor.Services;
using TreeDataGridUnoActivityMonitor.ViewModels;

namespace TreeDataGridUnoActivityMonitor;

public sealed partial class MainPage : Page
{
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Storyboard? _pulse;
    private readonly bool _smoke = TreeDataGridUnoSamples.SampleRunContext.HasArgument("--smoke");

    public MainPage()
    {
        ViewModel = CreateViewModel();
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _refreshTimer.Tick += OnRefreshTimerTick;
    }

    internal MonitorShellViewModel ViewModel { get; private set; }
    internal Uno.Controls.TreeDataGrid Table => SectionView.Table;
    private static MonitorShellViewModel CreateViewModel() => new(
        TreeDataGridUnoSamples.SampleRunContext.HasArgument("--demo") ? new DemoTelemetryProvider() : MonitorTelemetryProviderFactory.Create());

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsDisposed)
        {
            ViewModel = CreateViewModel();
            DataContext = ViewModel;
            Bindings.Update();
        }
        if (_smoke) return;
        _refreshTimer.Start();
        StartLivePulse();
        await ViewModel.RefreshAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Stop();
    public void Stop()
    {
        _refreshTimer.Stop();
        _pulse?.Stop();
        _pulse = null;
        Table.Model = null;
        ViewModel.Dispose();
    }

    private async void OnRefreshTimerTick(object? sender, object e) => await ViewModel.RefreshAsync();
    private void CpuSectionClick(object sender, RoutedEventArgs e) => ViewModel.SelectMetric(MetricKind.Cpu);
    private void MemorySectionClick(object sender, RoutedEventArgs e) => ViewModel.SelectMetric(MetricKind.Memory);
    private void EnergySectionClick(object sender, RoutedEventArgs e) => ViewModel.SelectMetric(MetricKind.Energy);
    private void DiskSectionClick(object sender, RoutedEventArgs e) => ViewModel.SelectMetric(MetricKind.Disk);
    private void NetworkSectionClick(object sender, RoutedEventArgs e) => ViewModel.SelectMetric(MetricKind.Network);

    private void StartLivePulse()
    {
        _pulse?.Stop();
        _pulse = new Storyboard();
        var opacity = new DoubleAnimation
        {
            From = 1, To = 0.35,
            Duration = new Duration(TimeSpan.FromMilliseconds(950)),
            RepeatBehavior = RepeatBehavior.Forever,
        };
        Storyboard.SetTarget(opacity, liveDot);
        Storyboard.SetTargetProperty(opacity, nameof(Opacity));
        _pulse.Children.Add(opacity);
        _pulse.Begin();
    }
}
