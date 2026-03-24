using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Avalonia.Controls.Models.TreeDataGrid;

public interface ITemplateCellOptions : ICellOptions
{
}

public sealed class TemplateColumnOptions<TModel> : ColumnOptions<TModel>, ITemplateCellOptions
{
    public bool IsTextSearchEnabled { get; set; }
    public Func<TModel, string?>? TextSearchValueSelector { get; set; }
}

public sealed class TemplateCell : ICell, IEditableObject
{
    private readonly ITemplateCellOptions? _options;

    public TemplateCell(
        object? value,
        Func<Control, DataTemplate> getCellTemplate,
        Func<Control, DataTemplate>? getCellEditingTemplate,
        ITemplateCellOptions? options)
    {
        Value = value;
        GetCellTemplate = getCellTemplate;
        GetCellEditingTemplate = getCellEditingTemplate;
        _options = options;
    }

    public bool CanEdit => GetCellEditingTemplate is not null;
    public BeginEditGestures EditGestures => _options?.BeginEditGestures ?? BeginEditGestures.Default;
    public Func<Control, DataTemplate> GetCellTemplate { get; }
    public Func<Control, DataTemplate>? GetCellEditingTemplate { get; }
    public object? Value { get; }

    object? ICell.Value => Value;

    void IEditableObject.BeginEdit() => (Value as IEditableObject)?.BeginEdit();
    void IEditableObject.CancelEdit() => (Value as IEditableObject)?.CancelEdit();
    void IEditableObject.EndEdit() => (Value as IEditableObject)?.EndEdit();
}

public class TemplateColumn<TModel> : ColumnBase<TModel>, ITextSearchableColumn<TModel>
    where TModel : class
{
    private readonly Func<Control, DataTemplate> _getCellTemplate;
    private readonly Func<Control, DataTemplate>? _getEditingCellTemplate;
    private DataTemplate? _cellTemplate;
    private DataTemplate? _cellEditingTemplate;
    private object? _cellTemplateResourceKey;
    private object? _cellEditingTemplateResourceKey;

    public TemplateColumn(
        object? header,
        DataTemplate cellTemplate,
        DataTemplate? cellEditingTemplate = null,
        GridLength? width = null,
        TemplateColumnOptions<TModel>? options = null)
        : base(header, width, options ?? new())
    {
        _cellTemplate = cellTemplate;
        _cellEditingTemplate = cellEditingTemplate;
        _getCellTemplate = GetCellTemplate;
        _getEditingCellTemplate = cellEditingTemplate is not null ? GetCellEditingTemplate : null;
    }

    public TemplateColumn(
        object? header,
        object cellTemplateResourceKey,
        object? cellEditingTemplateResourceKey = null,
        GridLength? width = null,
        TemplateColumnOptions<TModel>? options = null)
        : base(header, width, options ?? new())
    {
        _cellTemplateResourceKey = cellTemplateResourceKey ?? throw new ArgumentNullException(nameof(cellTemplateResourceKey));
        _cellEditingTemplateResourceKey = cellEditingTemplateResourceKey;
        _getCellTemplate = GetCellTemplate;
        _getEditingCellTemplate = cellEditingTemplateResourceKey is not null ? GetCellEditingTemplate : null;
    }

    public new TemplateColumnOptions<TModel> Options => (TemplateColumnOptions<TModel>)base.Options;

    bool ITextSearchableColumn<TModel>.IsTextSearchEnabled => Options.IsTextSearchEnabled;

    public DataTemplate GetCellTemplate(Control anchor)
    {
        return _cellTemplate ??= FindTemplate(anchor, _cellTemplateResourceKey!);
    }

    public DataTemplate GetCellEditingTemplate(Control anchor)
    {
        return _cellEditingTemplate ??= FindTemplate(anchor, _cellEditingTemplateResourceKey!);
    }

    public override ICell CreateCell(IRow<TModel> row)
    {
        return new TemplateCell(row.Model, _getCellTemplate, _getEditingCellTemplate, Options);
    }

    public override Comparison<TModel?>? GetComparison(ListSortDirection direction)
    {
        return direction switch
        {
            ListSortDirection.Ascending => Options.CompareAscending,
            ListSortDirection.Descending => Options.CompareDescending,
            _ => null,
        };
    }

    string? ITextSearchableColumn<TModel>.SelectValue(TModel model) => Options.TextSearchValueSelector?.Invoke(model);

    private static DataTemplate FindTemplate(FrameworkElement? anchor, object key)
    {
        for (FrameworkElement? current = anchor; current is not null; current = VisualTreeHelper.GetParent(current) as FrameworkElement)
        {
            if (current.Resources.ContainsKey(key) && current.Resources[key] is DataTemplate template)
                return template;
        }

        if (Application.Current?.Resources.ContainsKey(key) == true && Application.Current.Resources[key] is DataTemplate appTemplate)
            return appTemplate;

        throw new InvalidOperationException($"No DataTemplate resource with the key '{key}' could be found.");
    }
}
