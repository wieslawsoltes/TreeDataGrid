using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;

namespace Uno.Controls;

using Core = global::TreeDataGridCore.Models;

public abstract class TreeDataGridColumn : ColumnCreateOptions
{
    public object? Header { get; set; }

    /// <summary>Creates the reference-compatible common options for a derived column.</summary>
    /// <remarks>Scalar policies are captured; configured comparison callbacks retain the definition's live comparison policy.</remarks>
    protected ColumnOptions<object> CreateCommonOptions() => new()
    {
        CanUserResizeColumn = CanUserResize,
        CanUserSortColumn = CanUserSortColumn,
        AllowTriStateSorting = AllowTriStateSorting,
        MinWidth = MinWidth,
        MaxWidth = MaxWidth,
        BeginEditGestures = BeginEditGestures,
        CompareAscending = CompareAscending is null ? null : (a, b) => CompareAscending(a, b),
        CompareDescending = CompareDescending is null ? null : (a, b) => CompareDescending(a, b),
    };

    internal virtual bool IsHierarchical => false;
    internal abstract Core.IColumn<TModel> CreateCoreColumn<TModel>(object? header = null, ColumnCreateOptions? common = null,
        DeclarativeSourceContext? context = null)
        where TModel : class;
}

public class TreeDataGridTextColumn : TreeDataGridColumn
{
    public Binding? Binding { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsTextSearchEnabled { get; set; } = true;
    public string StringFormat { get; set; } = "{0}";
    public CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;
    public TextAlignment TextAlignment { get; set; } = TextAlignment.Left;
    public TextTrimming TextTrimming { get; set; } = TextTrimming.CharacterEllipsis;
    public TextWrapping TextWrapping { get; set; } = TextWrapping.NoWrap;

    internal override Core.IColumn<TModel> CreateCoreColumn<TModel>(object? header = null, ColumnCreateOptions? common = null,
        DeclarativeSourceContext? context = null)
    {
        var binding = Binding ?? throw new InvalidOperationException("TreeDataGridTextColumn requires Binding.");
        var accessor = new NativeColumnBinding(binding, culture: Culture);
        context?.Own(accessor);
        var readOnly = IsReadOnly;
        var options = new TextColumnOptions<TModel>
        {
            IsTextSearchEnabled = IsTextSearchEnabled, StringFormat = StringFormat, Culture = Culture,
            TextAlignment = TextAlignment, TextTrimming = TextTrimming, TextWrapping = TextWrapping,
        };
        TreeDataGridSourceExtensions.ApplyCommonOptions(options, common ?? this);
        var column = Core.ValueColumn<TModel, object?>.FromDelegate(header ?? Header, model => accessor.ReadSnapshot(model),
            setter: readOnly || !accessor.CanWrite ? null : (model, value) => accessor.WriteSnapshot(model, value),
            width: ColumnOptions<TModel>.ToCore(Width), options: TreeDataGridSourceExtensions.ToCoreOptions(options));
        return ColumnPresentationRegistry.Register(column, () => new DeclarativeCellColumn<TModel>(
            column, binding, readOnly, options, options.Snapshot()));
    }
}

public class TreeDataGridCheckBoxColumn : TreeDataGridColumn
{
    public Binding? Binding { get; set; }
    public bool IsReadOnly { get; set; }
    public bool? IsThreeState { get; set; }

    internal override Core.IColumn<TModel> CreateCoreColumn<TModel>(object? header = null, ColumnCreateOptions? common = null,
        DeclarativeSourceContext? context = null)
    {
        var binding = Binding ?? throw new InvalidOperationException("TreeDataGridCheckBoxColumn requires Binding.");
        var accessor = new NativeColumnBinding(binding);
        context?.Own(accessor);
        var readOnly = IsReadOnly;
        var valueType = GetValueType(binding, context?.ModelType);
        var threeState = IsThreeState ?? (valueType == typeof(bool?) ||
            (context?.Sample is { } sample && accessor.ReadSnapshot(sample) is null));
        var options = new CheckBoxColumnOptions<TModel>();
        TreeDataGridSourceExtensions.ApplyCommonOptions(options, common ?? this);
        var column = Core.ValueColumn<TModel, object?>.FromDelegate(header ?? Header, model => accessor.ReadSnapshot(model),
            setter: readOnly || !accessor.CanWrite ? null : (model, value) => accessor.WriteSnapshot(model, value),
            width: ColumnOptions<TModel>.ToCore(Width), options: TreeDataGridSourceExtensions.ToCoreOptions(options));
        return ColumnPresentationRegistry.Register(column, () => new DeclarativeCellColumn<TModel>(
            column, binding, readOnly, options, checkBox: true, threeState: threeState));
    }

    private static Type? GetValueType(Binding binding, Type? type) =>
        // Use the same grammar as writeback, including indexers. Explicit Source
        // is not the row type. No getter is invoked merely to discover nullability.
        NativeBindingPathWriter.GetPathValueType(binding.Source?.GetType() ?? type, binding.Path?.Path ?? string.Empty);
}

public class TreeDataGridTemplateColumn : TreeDataGridColumn
{
    public TreeDataGridTemplateColumn() { }
    public TreeDataGridTemplateColumn(object? header, object cellTemplateResourceKey, object? cellEditingTemplateResourceKey = null)
    {
        Header = header;
        CellTemplateResourceKey = cellTemplateResourceKey;
        CellEditingTemplateResourceKey = cellEditingTemplateResourceKey;
    }
    public DataTemplate? CellTemplate { get; set; }
    public DataTemplate? CellEditingTemplate { get; set; }
    public object? CellTemplateResourceKey { get; set; }
    public object? CellEditingTemplateResourceKey { get; set; }
    public Binding? TextSearchBinding { get; set; }

    internal override Core.IColumn<TModel> CreateCoreColumn<TModel>(object? header = null, ColumnCreateOptions? common = null,
        DeclarativeSourceContext? context = null)
    {
        var display = CellTemplate;
        var editing = CellEditingTemplate;
        var displayKey = CellTemplateResourceKey;
        var editingKey = CellEditingTemplateResourceKey;
        if (display is null && displayKey is null)
            throw new System.InvalidOperationException("TreeDataGridTemplateColumn requires CellTemplate or CellTemplateResourceKey.");
        var searchBinding = TextSearchBinding;
        var options = new TemplateColumnOptions<TModel>();
        TreeDataGridSourceExtensions.ApplyCommonOptions(options, common ?? this);
        var column = new Core.TemplateColumn<TModel>(header ?? Header, "Uno.Template",
            ColumnOptions<TModel>.ToCore(Width), TreeDataGridSourceExtensions.ToCoreOptions(options));
        return ColumnPresentationRegistry.Register(column, () => display is not null
            ? new TemplateColumn<TModel>(column, display, editing, CreateViewOptions())
            : new TemplateColumn<TModel>(column, displayKey!, editingKey, CreateViewOptions()));

        TemplateColumnOptions<TModel> CreateViewOptions() => ColumnOptions<TModel>.CopyCore(options,
            new TemplateColumnOptions<TModel>
            {
                BeginEditGestures = options.BeginEditGestures,
                TextSearchValueSelector = TreeDataGridBindingAccessor.TryCreateTextSelector<TModel>(searchBinding),
                IsTextSearchEnabled = searchBinding is not null,
            });
    }
}

public class TreeDataGridRowHeaderColumn : TreeDataGridColumn
{
    internal override Core.IColumn<TModel> CreateCoreColumn<TModel>(object? header = null, ColumnCreateOptions? common = null,
        DeclarativeSourceContext? context = null) =>
        TreeDataGridSourceExtensions.CreateRowHeaderColumn<TModel>(header ?? Header, common ?? this);
}

[Microsoft.UI.Xaml.Markup.ContentProperty(Name = nameof(InnerColumn))]
public class TreeDataGridHierarchicalExpanderColumn : TreeDataGridColumn
{
    public Binding? ChildrenBinding { get; set; }
    public Binding? HasChildrenBinding { get; set; }
    public Binding? IsExpandedBinding { get; set; }
    public TreeDataGridColumn? InnerColumn { get; set; }
    internal override bool IsHierarchical => true;
    internal override Core.IColumn<TModel> CreateCoreColumn<TModel>(object? header = null, ColumnCreateOptions? common = null,
        DeclarativeSourceContext? context = null)
    {
        var children = ChildrenBinding ?? throw new InvalidOperationException("TreeDataGridHierarchicalExpanderColumn requires ChildrenBinding.");
        var inner = InnerColumn?.CreateCoreColumn<TModel>(context: context)
            ?? throw new InvalidOperationException("TreeDataGridHierarchicalExpanderColumn requires an inner column.");
        return new DeclarativeExpanderColumn<TModel>(inner, children, HasChildrenBinding, IsExpandedBinding, context);
    }
}
