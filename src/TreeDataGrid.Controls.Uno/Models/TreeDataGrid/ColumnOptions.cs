using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Uno.Controls.Presentation;

namespace Uno.Controls.Models.TreeDataGrid;

/// <summary>UI options extending, rather than duplicating, Core column policy.</summary>
public class ColumnOptions<TModel> : TreeDataGridCore.Models.ColumnOptions<TModel>, ICellOptions, ICellColumnLayoutOptions
{
    public new GridLength MinWidth
    {
        get => new(base.MinWidth.Value, (GridUnitType)base.MinWidth.GridUnitType);
        set => base.MinWidth = new(value.Value, (TreeDataGridCore.GridUnitType)value.GridUnitType);
    }
    public new GridLength? MaxWidth
    {
        get => base.MaxWidth is { } width ? new(width.Value, (GridUnitType)width.GridUnitType) : null;
        set => base.MaxWidth = value is { } width ? new(width.Value, (TreeDataGridCore.GridUnitType)width.GridUnitType) : null;
    }
    /// <summary>Allows header activation to cycle from descending back to source order.</summary>
    /// <remarks>This is a live view interaction policy; Core comparers remain unchanged.</remarks>
    public bool AllowTriStateSorting { get; set; }
    public BeginEditGestures BeginEditGestures { get; set; } = BeginEditGestures.Default;
    internal static TreeDataGridCore.GridLength? ToCore(GridLength? width) =>
        width is { } value ? new(value.Value, (TreeDataGridCore.GridUnitType)value.GridUnitType) : null;
    internal static TOptions CopyCore<TOptions>(TreeDataGridCore.Models.ColumnOptions<TModel> source, TOptions target)
        where TOptions : ColumnOptions<TModel>
    {
        TreeDataGridCore.Models.ColumnOptions<TModel> core = target;
        core.MinWidth = source.MinWidth;
        core.MaxWidth = source.MaxWidth;
        core.CanUserResizeColumn = source.CanUserResizeColumn;
        core.CanUserSortColumn = source.CanUserSortColumn;
        core.CompareAscending = source.CompareAscending;
        core.CompareDescending = source.CompareDescending;
        target.AllowTriStateSorting = source is ColumnOptions<TModel> native && native.AllowTriStateSorting;
        return target;
    }
}

public class TextColumnOptions<TModel> : ColumnOptions<TModel>, ITextCellOptions
{
    public bool IsTextSearchEnabled { get; set; }
    public string StringFormat { get; set; } = "{0}";
    public CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;
    public TextTrimming TextTrimming { get; set; } = TextTrimming.CharacterEllipsis;
    public TextWrapping TextWrapping { get; set; } = TextWrapping.NoWrap;
    public TextAlignment TextAlignment { get; set; } = TextAlignment.Left;
    internal TextCellOptions Snapshot() => new()
    {
        IsTextSearchEnabled = IsTextSearchEnabled, StringFormat = StringFormat,
        Culture = Culture, TextTrimming = TextTrimming,
        TextWrapping = TextWrapping, TextAlignment = TextAlignment,
    };
}

public class CheckBoxColumnOptions<TModel> : ColumnOptions<TModel> { }

public class TemplateColumnOptions<TModel> : ColumnOptions<TModel>, ITemplateCellOptions
{
    public bool IsTextSearchEnabled { get; set; }
    public Func<TModel, string?>? TextSearchValueSelector { get; set; }
}
