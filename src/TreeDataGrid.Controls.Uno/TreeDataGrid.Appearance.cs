using Microsoft.UI.Xaml;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    private void InitializeAppearance()
    {
        // Callbacks are owned by this control, not a global theme/font source.
        // They run for inherited changes too; there is no per-recycle observer.
        RegisterPropertyChangedCallback(FontFamilyProperty, OnAppearanceMetricsChanged);
        RegisterPropertyChangedCallback(FontSizeProperty, OnAppearanceMetricsChanged);
        RegisterPropertyChangedCallback(FontWeightProperty, OnAppearanceMetricsChanged);
        RegisterPropertyChangedCallback(FontStyleProperty, OnAppearanceMetricsChanged);
        RegisterPropertyChangedCallback(FontStretchProperty, OnAppearanceMetricsChanged);
        // Some Uno heads expose these native Control properties as stubs.
        // Never evaluate a dependency-property getter which is not implemented.
        if (NativeTreeDataGridCapabilities.CharacterSpacing)
        {
#pragma warning disable Uno0001 // Guarded by the platform's implemented-property metadata.
            RegisterPropertyChangedCallback(CharacterSpacingProperty, OnAppearanceMetricsChanged);
#pragma warning restore Uno0001
        }
        if (NativeTreeDataGridCapabilities.TextScaleFactor)
        {
#pragma warning disable Uno0001 // Guarded by the platform's implemented-property metadata.
            RegisterPropertyChangedCallback(IsTextScaleFactorEnabledProperty, OnAppearanceMetricsChanged);
#pragma warning restore Uno0001
        }
        RegisterPropertyChangedCallback(FlowDirectionProperty, OnAppearanceMetricsChanged);
        ActualThemeChanged += OnActualThemeChanged;
    }
    private void OnAppearanceMetricsChanged(DependencyObject sender, DependencyProperty property) => InvalidateAppearanceMeasurements();
    private void OnActualThemeChanged(FrameworkElement sender, object args) => InvalidateAppearanceMeasurements();
    private void InvalidateAppearanceMeasurements()
    {
        _presentation?.ResetColumnMeasurements();
        _presenter?.InvalidateRowMeasurements();
        if (_presenter is not null)
            foreach (var row in _presenter.RealizedRows)
            {
                if (row.CellsPresenter is { } cells)
                {
                    foreach (var cell in cells.RealizedCells) cell.InvalidateMeasure();
                    cells.InvalidateMeasure();
                }
                row.InvalidateMeasure();
            }
        if (_headers is not null)
        {
            foreach (var header in _headers.RealizedHeaders) header.InvalidateMeasure();
            _headers.InvalidateMeasure();
        }
        // Keep the committed geometry until natural measurements arrive. It
        // prevents a font/theme change from temporarily collapsing Auto columns
        // to zero and virtualizing their controls out before they can remeasure.
        _presenter?.InvalidateMeasure();
        InvalidateMeasure();
    }
}
