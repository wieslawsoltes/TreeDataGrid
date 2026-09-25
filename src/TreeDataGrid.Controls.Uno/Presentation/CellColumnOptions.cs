using Microsoft.UI.Xaml;

namespace Uno.Controls.Presentation;

/// <summary>View-only sizing and interaction options. Sort execution and value access belong to Core models.</summary>
public sealed class CellColumnOptions
{
    private readonly ICellColumnLayoutOptions? _source;
    private bool? _canUserResizeColumn;
    private bool _allowTriStateSorting;
    private GridLength _minWidth = new(30);
    private GridLength? _maxWidth;

    public CellColumnOptions() { }
    internal CellColumnOptions(ICellColumnLayoutOptions source) => _source = source;

    public bool? CanUserResizeColumn
    {
        get => _source is { } source ? source.CanUserResizeColumn : _canUserResizeColumn;
        set { if (_source is { } source) source.CanUserResizeColumn = value; else _canUserResizeColumn = value; }
    }
    /// <summary>Live header-cycle policy; sorting itself remains Core-owned.</summary>
    public bool AllowTriStateSorting
    {
        get => _source is { } source ? source.AllowTriStateSorting : _allowTriStateSorting;
        set { if (_source is { } source) source.AllowTriStateSorting = value; else _allowTriStateSorting = value; }
    }
    public GridLength MinWidth
    {
        get => _source is { } source ? source.MinWidth : _minWidth;
        set { if (_source is { } source) source.MinWidth = value; else _minWidth = value; }
    }
    public GridLength? MaxWidth
    {
        get => _source is { } source ? source.MaxWidth : _maxWidth;
        set { if (_source is { } source) source.MaxWidth = value; else _maxWidth = value; }
    }
}

// A live view-policy bridge, not a snapshot or a second column definition.
// Both the typed compatibility base and its CellColumnBase view use one owner.
internal interface ICellColumnLayoutOptions
{
    bool? CanUserResizeColumn { get; set; }
    bool AllowTriStateSorting { get; set; }
    GridLength MinWidth { get; set; }
    GridLength? MaxWidth { get; set; }
}
