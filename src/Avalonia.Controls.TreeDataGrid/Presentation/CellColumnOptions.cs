using Avalonia.Controls;

namespace Avalonia.Controls.Presentation;

/// <summary>View-only sizing options. Sorting and value access belong to Core models.</summary>
public sealed class CellColumnOptions
{
    public bool? CanUserResizeColumn { get; set; }
    public GridLength MinWidth { get; set; } = new(30);
    public GridLength? MaxWidth { get; set; }
}
