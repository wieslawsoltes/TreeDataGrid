using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridUnoActivityMonitor.ViewModels;

namespace TreeDataGridUnoActivityMonitor.Views;

public sealed partial class MetricSectionView : UserControl
{
    internal Uno.Controls.TreeDataGrid Table => Grid;
    public static readonly DependencyProperty SectionProperty =
        DependencyProperty.Register(
            nameof(Section),
            typeof(MetricSectionViewModel),
            typeof(MetricSectionView),
            new PropertyMetadata(null, OnSectionChanged));

    public MetricSectionView()
    {
        InitializeComponent();
    }

    public MetricSectionViewModel? Section
    {
        get => (MetricSectionViewModel?)GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    private static void OnSectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (MetricSectionView)d;
        view.Grid.Model = null;
        view.Grid.CellTemplates["IdentityCell"] = (DataTemplate)Application.Current.Resources["IdentityCell"];
        view.Grid.PresentationOptions = view.Section?.PresentationOptions;
        view.Grid.Model = view.Section?.Source;
        view.Bindings.Update();
    }
}
