using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Uno.Controls.Primitives;

/// <summary>Retains its chevron template while expansion changes.</summary>
public partial class TreeDataGridExpanderButton : Button
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(TreeDataGridExpanderButton), new PropertyMetadata(false, ExpansionChanged));
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateExpansionState();
    }
    private static void ExpansionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((TreeDataGridExpanderButton)sender).UpdateExpansionState();
    private void UpdateExpansionState() => VisualStateManager.GoToState(this, IsExpanded ? "Expanded" : "Collapsed", false);
}
