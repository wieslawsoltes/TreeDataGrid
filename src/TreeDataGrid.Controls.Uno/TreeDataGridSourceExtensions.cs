using System;
using System.Linq.Expressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using TreeDataGridCore;
using Uno.Controls.Presentation;
using GridLength = Microsoft.UI.Xaml.GridLength;
using GridUnitType = Microsoft.UI.Xaml.GridUnitType;
using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls
{
    using Core = global::TreeDataGridCore.Models;

    public class ColumnCreateOptions
    {
        public GridLength Width { get; set; } = GridLength.Auto;
        public bool? CanUserResize { get; set; }
        public bool? CanUserSortColumn { get; set; }
        public bool AllowTriStateSorting { get; set; }
        public GridLength MinWidth { get; set; } = new(30, GridUnitType.Pixel);
        public GridLength? MaxWidth { get; set; }
        public Comparison<object?>? CompareAscending { get; set; }
        public Comparison<object?>? CompareDescending { get; set; }
        public BeginEditGestures BeginEditGestures { get; set; } = BeginEditGestures.Default;
    }

    public class ColumnCreateOptions<TModel>
        : ColumnCreateOptions
    {
    }

    public class TextColumnCreateOptions : ColumnCreateOptions
    {
        public bool IsReadOnly { get; set; }
        public bool IsTextSearchEnabled { get; set; } = true;
        public string StringFormat { get; set; } = "{0}";
        public IFormatProvider? Culture { get; set; }
        public Microsoft.UI.Xaml.TextAlignment TextAlignment { get; set; } = Microsoft.UI.Xaml.TextAlignment.Left;
        public Microsoft.UI.Xaml.TextTrimming TextTrimming { get; set; } = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;
        public Microsoft.UI.Xaml.TextWrapping TextWrapping { get; set; } = Microsoft.UI.Xaml.TextWrapping.NoWrap;
    }

    public class TextColumnCreateOptions<TModel> : TextColumnCreateOptions
    {
    }

    public class CheckBoxColumnCreateOptions : ColumnCreateOptions
    {
        public bool IsReadOnly { get; set; }
    }

    public class CheckBoxColumnCreateOptions<TModel> : CheckBoxColumnCreateOptions
    {
    }

    public class TemplateColumnCreateOptions : ColumnCreateOptions
    {
        public Binding? TextSearchBinding { get; set; }
    }

    public class TemplateColumnCreateOptions<TModel> : TemplateColumnCreateOptions
    {
    }

    public class HierarchicalExpanderColumnCreateOptions : ColumnCreateOptions
    {
    }

    public class HierarchicalExpanderColumnCreateOptions<TModel> : HierarchicalExpanderColumnCreateOptions
    {
        public Expression<Func<TModel, bool>>? HasChildren { get; set; }
        public Expression<Func<TModel, bool>>? IsExpanded { get; set; }
    }

    public static class TreeDataGridSourceExtensions
    {
        public static FlatTreeDataGridSource<TModel> WithRowHeaderColumn<TModel>(
            this FlatTreeDataGridSource<TModel> source,
            object? header = null,
            Action<ColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddRowHeaderColumn(source.Columns, header, configure);
            return source;
        }

        public static HierarchicalTreeDataGridSource<TModel> WithRowHeaderColumn<TModel>(
            this HierarchicalTreeDataGridSource<TModel> source,
            object? header = null,
            Action<ColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddRowHeaderColumn(source.Columns, header, configure);
            return source;
        }

        public static FlatTreeDataGridSource<TModel> WithTextColumn<TModel, TValue>(
            this FlatTreeDataGridSource<TModel> source,
            Expression<Func<TModel, TValue?>> getter,
            Action<TextColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddTextColumn(source.Columns, null, getter, configure);
            return source;
        }

        public static FlatTreeDataGridSource<TModel> WithTextColumn<TModel, TValue>(
            this FlatTreeDataGridSource<TModel> source,
            object? header,
            Expression<Func<TModel, TValue?>> getter,
            Action<TextColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddTextColumn(source.Columns, header, getter, configure);
            return source;
        }

        public static HierarchicalTreeDataGridSource<TModel> WithTextColumn<TModel, TValue>(
            this HierarchicalTreeDataGridSource<TModel> source,
            Expression<Func<TModel, TValue?>> getter,
            Action<TextColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddTextColumn(source.Columns, null, getter, configure);
            return source;
        }

        public static HierarchicalTreeDataGridSource<TModel> WithTextColumn<TModel, TValue>(
            this HierarchicalTreeDataGridSource<TModel> source,
            object? header,
            Expression<Func<TModel, TValue?>> getter,
            Action<TextColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddTextColumn(source.Columns, header, getter, configure);
            return source;
        }

        public static FlatTreeDataGridSource<TModel> WithCheckBoxColumn<TModel>(
            this FlatTreeDataGridSource<TModel> source,
            Expression<Func<TModel, bool>> getter,
            Action<CheckBoxColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddCheckBoxColumn(source.Columns, null, getter, configure);
            return source;
        }

        public static FlatTreeDataGridSource<TModel> WithCheckBoxColumn<TModel>(
            this FlatTreeDataGridSource<TModel> source,
            object? header,
            Expression<Func<TModel, bool>> getter,
            Action<CheckBoxColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddCheckBoxColumn(source.Columns, header, getter, configure);
            return source;
        }

        public static HierarchicalTreeDataGridSource<TModel> WithCheckBoxColumn<TModel>(
            this HierarchicalTreeDataGridSource<TModel> source,
            Expression<Func<TModel, bool>> getter,
            Action<CheckBoxColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddCheckBoxColumn(source.Columns, null, getter, configure);
            return source;
        }

        public static HierarchicalTreeDataGridSource<TModel> WithCheckBoxColumn<TModel>(
            this HierarchicalTreeDataGridSource<TModel> source,
            object? header,
            Expression<Func<TModel, bool>> getter,
            Action<CheckBoxColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddCheckBoxColumn(source.Columns, header, getter, configure);
            return source;
        }

        public static FlatTreeDataGridSource<TModel> WithThreeStateCheckBoxColumn<TModel>(
            this FlatTreeDataGridSource<TModel> source,
            Expression<Func<TModel, bool?>> getter,
            Action<CheckBoxColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddThreeStateCheckBoxColumn(source.Columns, null, getter, configure);
            return source;
        }

        public static FlatTreeDataGridSource<TModel> WithThreeStateCheckBoxColumn<TModel>(
            this FlatTreeDataGridSource<TModel> source,
            object? header,
            Expression<Func<TModel, bool?>> getter,
            Action<CheckBoxColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddThreeStateCheckBoxColumn(source.Columns, header, getter, configure);
            return source;
        }

        public static HierarchicalTreeDataGridSource<TModel> WithThreeStateCheckBoxColumn<TModel>(
            this HierarchicalTreeDataGridSource<TModel> source,
            Expression<Func<TModel, bool?>> getter,
            Action<CheckBoxColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddThreeStateCheckBoxColumn(source.Columns, null, getter, configure);
            return source;
        }

        public static HierarchicalTreeDataGridSource<TModel> WithThreeStateCheckBoxColumn<TModel>(
            this HierarchicalTreeDataGridSource<TModel> source,
            object? header,
            Expression<Func<TModel, bool?>> getter,
            Action<CheckBoxColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddThreeStateCheckBoxColumn(source.Columns, header, getter, configure);
            return source;
        }

        public static FlatTreeDataGridSource<TModel> WithTemplateColumn<TModel>(
            this FlatTreeDataGridSource<TModel> source,
            object? header,
            DataTemplate cellTemplate,
            DataTemplate? cellEditingTemplate = null,
            Action<TemplateColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddTemplateColumn(source.Columns, header, cellTemplate, cellEditingTemplate, configure);
            return source;
        }

        public static HierarchicalTreeDataGridSource<TModel> WithTemplateColumn<TModel>(
            this HierarchicalTreeDataGridSource<TModel> source,
            object? header,
            DataTemplate cellTemplate,
            DataTemplate? cellEditingTemplate = null,
            Action<TemplateColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddTemplateColumn(source.Columns, header, cellTemplate, cellEditingTemplate, configure);
            return source;
        }

        public static FlatTreeDataGridSource<TModel> WithTemplateColumnFromResourceKeys<TModel>(
            this FlatTreeDataGridSource<TModel> source,
            object? header,
            object cellTemplateResourceKey,
            object? cellEditingTemplateResourceKey = null,
            Action<TemplateColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddTemplateColumn(source.Columns, header, cellTemplateResourceKey, cellEditingTemplateResourceKey, configure);
            return source;
        }

        public static HierarchicalTreeDataGridSource<TModel> WithTemplateColumnFromResourceKeys<TModel>(
            this HierarchicalTreeDataGridSource<TModel> source,
            object? header,
            object cellTemplateResourceKey,
            object? cellEditingTemplateResourceKey = null,
            Action<TemplateColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddTemplateColumn(source.Columns, header, cellTemplateResourceKey, cellEditingTemplateResourceKey, configure);
            return source;
        }

        public static HierarchicalTreeDataGridSource<TModel> WithHierarchicalExpanderTextColumn<TModel, TValue>(
            this HierarchicalTreeDataGridSource<TModel> source,
            Expression<Func<TModel, TValue?>> getter,
            Func<TModel, System.Collections.Generic.IEnumerable<TModel>?> childSelector,
            Action<HierarchicalExpanderColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddHierarchicalExpanderTextColumn(source.Columns, null, getter, childSelector, configure);
            return source;
        }

        public static HierarchicalTreeDataGridSource<TModel> WithHierarchicalExpanderTextColumn<TModel, TValue>(
            this HierarchicalTreeDataGridSource<TModel> source,
            object? header,
            Expression<Func<TModel, TValue?>> getter,
            Func<TModel, System.Collections.Generic.IEnumerable<TModel>?> childSelector,
            Action<HierarchicalExpanderColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            AddHierarchicalExpanderTextColumn(source.Columns, header, getter, childSelector, configure);
            return source;
        }

        public static HierarchicalTreeDataGridSource<TModel> WithHierarchicalExpanderColumn<TModel>(
            this HierarchicalTreeDataGridSource<TModel> source,
            object? header,
            TreeDataGridTemplateColumn innerColumn,
            Func<TModel, System.Collections.Generic.IEnumerable<TModel>?> childSelector,
            Action<HierarchicalExpanderColumnCreateOptions<TModel>>? configure = null)
            where TModel : class
        {
            var options = new HierarchicalExpanderColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            ArgumentNullException.ThrowIfNull(innerColumn);
            var inner = innerColumn.CreateCoreColumn<TModel>(header, options);
            source.Columns.Add(new Core.HierarchicalExpanderColumn<TModel>(inner, childSelector, options.HasChildren, options.IsExpanded));
            return source;
        }

        private static void AddRowHeaderColumn<TModel>(
            Core.ColumnList<TModel> columns, object? header, Action<ColumnCreateOptions<TModel>>? configure)
            where TModel : class
        {
            var options = new ColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            columns.Add(CreateRowHeaderColumn<TModel>(header, options));
        }

        internal static Core.IColumn<TModel> CreateRowHeaderColumn<TModel>(object? header, ColumnCreateOptions options)
            where TModel : class
        {
            var uiOptions = new ColumnOptions<TModel>();
            ApplyCommonOptions(uiOptions, options);
            var coreOptions = ToCoreOptions(uiOptions);
            // Row headers are not sortable, even when a grid enables sorting globally.
            coreOptions.CanUserSortColumn = false;
            uiOptions.CanUserSortColumn = false;
            var column = new Core.ValueColumn<TModel, string>(header, _ => string.Empty,
                width: ColumnOptions<TModel>.ToCore(options.Width), options: coreOptions)
                { PresentationKey = "Uno.RowHeader" };
            return ColumnPresentationRegistry.Register(column, () => new RowHeaderCellColumn<TModel>(column, uiOptions));
        }

        private static void AddTextColumn<TModel, TValue>(
            Core.ColumnList<TModel> columns, object? header, Expression<Func<TModel, TValue?>> getter,
            Action<TextColumnCreateOptions<TModel>>? configure) where TModel : class
        {
            var options = new TextColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            var uiOptions = new TextColumnOptions<TModel>
            {
                IsTextSearchEnabled = options.IsTextSearchEnabled,
                StringFormat = options.StringFormat,
                TextAlignment = options.TextAlignment,
                TextTrimming = options.TextTrimming,
                TextWrapping = options.TextWrapping,
            };
            if (options.Culture is System.Globalization.CultureInfo culture) uiOptions.Culture = culture;
            ApplyCommonOptions(uiOptions, options);
            var setter = options.IsReadOnly ? null : TreeDataGridExpressionHelper.TryCreateSetter(getter);
            columns.Add(CreateTextColumn(header, getter, setter, options.Width, uiOptions));
        }

        private static Core.ValueColumn<TModel, TValue?> CreateTextColumn<TModel, TValue>(
            object? header, Expression<Func<TModel, TValue?>> getter, Action<TModel, TValue?>? setter,
            GridLength width, TextColumnOptions<TModel> options) where TModel : class
        {
            header ??= TreeDataGridExpressionHelper.TryGetMemberName(getter) ?? string.Empty;
            var column = new Core.ValueColumn<TModel, TValue?>(header, getter, setter,
                ColumnOptions<TModel>.ToCore(width), ToCoreOptions(options));
            return ColumnPresentationRegistry.Register(column, () => new TextColumn<TModel, TValue>(column, options));
        }

        private static void AddCheckBoxColumn<TModel>(
            Core.ColumnList<TModel> columns, object? header, Expression<Func<TModel, bool>> getter,
            Action<CheckBoxColumnCreateOptions<TModel>>? configure) where TModel : class
        {
            var options = new CheckBoxColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            var uiOptions = new CheckBoxColumnOptions<TModel>();
            ApplyCommonOptions(uiOptions, options);
            var setter = options.IsReadOnly ? null : TreeDataGridExpressionHelper.TryCreateNonNullableSetter(getter);
            var column = new Core.CheckBoxColumn<TModel>(header ?? TreeDataGridExpressionHelper.TryGetMemberName(getter) ?? string.Empty,
                getter, setter, ColumnOptions<TModel>.ToCore(options.Width), ToCoreOptions(uiOptions));
            columns.Add(ColumnPresentationRegistry.Register(column, () => new CheckBoxColumn<TModel>(column, uiOptions)));
        }

        private static void AddThreeStateCheckBoxColumn<TModel>(
            Core.ColumnList<TModel> columns, object? header, Expression<Func<TModel, bool?>> getter,
            Action<CheckBoxColumnCreateOptions<TModel>>? configure) where TModel : class
        {
            var options = new CheckBoxColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            var uiOptions = new CheckBoxColumnOptions<TModel>();
            ApplyCommonOptions(uiOptions, options);
            var setter = options.IsReadOnly ? null : TreeDataGridExpressionHelper.TryCreateSetter(getter);
            var column = new Core.CheckBoxColumn<TModel>(header ?? TreeDataGridExpressionHelper.TryGetMemberName(getter) ?? string.Empty,
                getter, setter, ColumnOptions<TModel>.ToCore(options.Width), ToCoreOptions(uiOptions));
            columns.Add(ColumnPresentationRegistry.Register(column, () => new CheckBoxColumn<TModel>(column, uiOptions)));
        }

        private static void AddTemplateColumn<TModel>(
            Core.ColumnList<TModel> columns, object? header, DataTemplate cellTemplate,
            DataTemplate? cellEditingTemplate, Action<TemplateColumnCreateOptions<TModel>>? configure)
            where TModel : class
        {
            ArgumentNullException.ThrowIfNull(cellTemplate);
            var options = new TemplateColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            var definition = new TreeDataGridTemplateColumn
                { Header = header, CellTemplate = cellTemplate, CellEditingTemplate = cellEditingTemplate, Width = options.Width,
                  TextSearchBinding = options.TextSearchBinding };
            columns.Add(definition.CreateCoreColumn<TModel>(null, options));
        }

        private static void AddTemplateColumn<TModel>(
            Core.ColumnList<TModel> columns, object? header, object cellTemplateResourceKey,
            object? cellEditingTemplateResourceKey, Action<TemplateColumnCreateOptions<TModel>>? configure)
            where TModel : class
        {
            ArgumentNullException.ThrowIfNull(cellTemplateResourceKey);
            var options = new TemplateColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            var definition = new TreeDataGridTemplateColumn(header, cellTemplateResourceKey, cellEditingTemplateResourceKey)
                { Width = options.Width, TextSearchBinding = options.TextSearchBinding };
            columns.Add(definition.CreateCoreColumn<TModel>(null, options));
        }

        private static void AddHierarchicalExpanderTextColumn<TModel, TValue>(
            Core.ColumnList<TModel> columns, object? header, Expression<Func<TModel, TValue?>> getter,
            Func<TModel, System.Collections.Generic.IEnumerable<TModel>?> childSelector,
            Action<HierarchicalExpanderColumnCreateOptions<TModel>>? configure) where TModel : class
        {
            var options = new HierarchicalExpanderColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            var uiOptions = new TextColumnOptions<TModel>();
            // Preserve the caller's complete policy, not only the width.
            ApplyCommonOptions(uiOptions, options);
            var setter = TreeDataGridExpressionHelper.TryCreateSetter(getter);
            var inner = CreateTextColumn(header, getter, setter, options.Width, uiOptions);
            columns.Add(new Core.HierarchicalExpanderColumn<TModel>(inner, childSelector, options.HasChildren, options.IsExpanded));
        }

        internal static void ApplyCommonOptions<TModel>(ColumnOptions<TModel> target, ColumnCreateOptions source)
        {
            target.CanUserResizeColumn = source.CanUserResize;
            target.CanUserSortColumn = source.CanUserSortColumn;
            target.MinWidth = source.MinWidth;
            target.MaxWidth = source.MaxWidth;
            target.BeginEditGestures = source.BeginEditGestures;
            var ascending = source.CompareAscending;
            var descending = source.CompareDescending;
            target.CompareAscending = ascending is null ? null : (a, b) => ascending(a, b);
            target.CompareDescending = descending is null ? null : (a, b) => descending(a, b);
        }

        // UI options derive from Core options for source compatibility, but the
        // source's actual configuration remains a neutral snapshot.
        internal static Core.ColumnOptions<TModel> ToCoreOptions<TModel>(ColumnOptions<TModel> options)
        {
            Core.ColumnOptions<TModel> core = options;
            return new()
            {
                CanUserResizeColumn = core.CanUserResizeColumn, CanUserSortColumn = core.CanUserSortColumn,
                MinWidth = core.MinWidth, MaxWidth = core.MaxWidth,
                CompareAscending = core.CompareAscending, CompareDescending = core.CompareDescending,
            };
        }
    }
}
