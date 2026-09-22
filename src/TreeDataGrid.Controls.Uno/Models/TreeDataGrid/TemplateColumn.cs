using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Uno.Controls.Presentation;

namespace Uno.Controls.Models.TreeDataGrid;

public class TemplateColumn<TModel> : ValueCellColumn<TModel, TModel>, ITextSearchableColumn<TModel> where TModel : class
{
    private DataTemplate? _display;
    private DataTemplate? _editing;
    private readonly object? _displayKey;
    private readonly object? _editingKey;
    public TemplateColumn(object? header, DataTemplate cellTemplate, DataTemplate? cellEditingTemplate = null,
        GridLength? width = null, TemplateColumnOptions<TModel>? options = null)
        : this(header, width, options ?? new())
    {
        _display = cellTemplate ?? throw new ArgumentNullException(nameof(cellTemplate));
        _editing = cellEditingTemplate;
    }
    public TemplateColumn(object? header, object cellTemplateResourceKey, object? cellEditingTemplateResourceKey = null,
        GridLength? width = null, TemplateColumnOptions<TModel>? options = null)
        : this(header, width, options ?? new())
    {
        _displayKey = cellTemplateResourceKey ?? throw new ArgumentNullException(nameof(cellTemplateResourceKey));
        _editingKey = cellEditingTemplateResourceKey;
    }
    private TemplateColumn(object? header, GridLength? width, TemplateColumnOptions<TModel> options)
        : this(new TreeDataGridCore.Models.TemplateColumn<TModel>(header, "Uno.Template", ColumnOptions<TModel>.ToCore(width), options), options)
    {
    }
    public TemplateColumn(TreeDataGridCore.Models.TemplateColumn<TModel> column, DataTemplate cellTemplate,
        DataTemplate? cellEditingTemplate = null, TemplateColumnOptions<TModel>? options = null)
        : this(column, options ?? ColumnOptions<TModel>.CopyCore(column.Options, new TemplateColumnOptions<TModel>()))
    {
        _display = cellTemplate ?? throw new ArgumentNullException(nameof(cellTemplate));
        _editing = cellEditingTemplate;
    }
    public TemplateColumn(TreeDataGridCore.Models.TemplateColumn<TModel> column, object cellTemplateResourceKey,
        object? cellEditingTemplateResourceKey = null, TemplateColumnOptions<TModel>? options = null)
        : this(column, options ?? ColumnOptions<TModel>.CopyCore(column.Options, new TemplateColumnOptions<TModel>()))
    {
        _displayKey = cellTemplateResourceKey ?? throw new ArgumentNullException(nameof(cellTemplateResourceKey));
        _editingKey = cellEditingTemplateResourceKey;
    }
    private TemplateColumn(TreeDataGridCore.Models.TemplateColumn<TModel> column, TemplateColumnOptions<TModel> options)
        : base(column, CellKind.Template, viewOptions: options)
    {
        Options = options;
        Header = column.Header;
        BeginEditGestures = options.BeginEditGestures;
    }
    public TemplateColumnOptions<TModel> Options { get; }
    public override bool IsTextSearchEnabled => Options.IsTextSearchEnabled;
    public override string? GetSearchText(object? model) => model is TModel typed ? Options.TextSearchValueSelector?.Invoke(typed) : null;
    public override DataTemplate GetCellTemplate(Control anchor) => _display ??= FindTemplate(anchor, _displayKey!);
    public override DataTemplate? GetCellEditingTemplate(Control anchor) =>
        _editing ?? (_editingKey is null ? null : _editing = FindTemplate(anchor, _editingKey));
    private DataTemplate FindTemplate(Control anchor, object key)
    {
        for (DependencyObject? current = anchor; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is FrameworkElement element && element.Resources.TryGetValue(key, out var resource))
                return resource as DataTemplate ?? throw new InvalidOperationException($"Resource '{key}' for column '{Header}' is not a DataTemplate.");
        if (Application.Current?.Resources.TryGetValue(key, out var applicationResource) == true)
            return applicationResource as DataTemplate ?? throw new InvalidOperationException($"Resource '{key}' for column '{Header}' is not a DataTemplate.");
        throw new KeyNotFoundException($"No data template resource with key '{key}' was found for column '{Header}'.");
    }
    string? ITextSearchableColumn<TModel>.SelectValue(TModel model) => GetSearchText(model);

}
