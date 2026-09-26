using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Uno.Controls.Primitives;

/// <summary>Native counterpart of the reference header's string-only data template.</summary>
public sealed partial class TreeDataGridHeaderTemplateSelector : DataTemplateSelector
{
    public DataTemplate? StringTemplate { get; set; }
    protected override DataTemplate SelectTemplateCore(object item) =>
        item is string && StringTemplate is { } template ? template : base.SelectTemplateCore(item);
    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) =>
        item is string && StringTemplate is { } template ? template : base.SelectTemplateCore(item, container);
}
