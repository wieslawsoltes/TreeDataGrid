using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using Avalonia.Controls.Models.TreeDataGrid;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Avalonia.Controls
{
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

    public class ColumnCreateOptions<TModel> : ColumnCreateOptions
    {
    }

    public class TextColumnCreateOptions : ColumnCreateOptions
    {
        public bool IsReadOnly { get; set; }
        public bool IsTextSearchEnabled { get; set; } = true;
        public string StringFormat { get; set; } = "{0}";
        public IFormatProvider? Culture { get; set; }
        public Avalonia.Media.TextAlignment TextAlignment { get; set; } = Avalonia.Media.TextAlignment.Left;
        public Avalonia.Media.TextTrimming TextTrimming { get; set; } = Avalonia.Media.TextTrimming.CharacterEllipsis;
        public Avalonia.Media.TextWrapping TextWrapping { get; set; } = Avalonia.Media.TextWrapping.NoWrap;
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
            var inner = innerColumn.CreateTyped<TModel>();
            TreeDataGridColumnOptionsFactory.ApplyCommonOptions(inner.Options, options);
            inner.Header = header ?? inner.Header;
            source.Columns.Add(new HierarchicalExpanderColumn<TModel>(inner, childSelector, options.HasChildren, options.IsExpanded));
            return source;
        }

        private static void AddRowHeaderColumn<TModel>(
            ColumnList<TModel> columns,
            object? header,
            Action<ColumnCreateOptions<TModel>>? configure)
            where TModel : class
        {
            var options = new ColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            var internalOptions = TreeDataGridColumnOptionsFactory.CreateCommonOptions<TModel>(options);
            columns.Add(new TreeDataGridRowHeaderColumnInternal<TModel>(header, options.Width, internalOptions));
        }

        private static void AddTextColumn<TModel, TValue>(
            ColumnList<TModel> columns,
            object? header,
            Expression<Func<TModel, TValue?>> getter,
            Action<TextColumnCreateOptions<TModel>>? configure)
            where TModel : class
        {
            var options = new TextColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            header ??= TreeDataGridExpressionHelper.TryGetMemberName(getter) ?? string.Empty;

            var internalOptions = TreeDataGridColumnOptionsFactory.CreateTextOptions<TModel>(options);

            var setter = options.IsReadOnly ? null : TreeDataGridExpressionHelper.TryCreateSetter(getter);
            columns.Add(setter is null
                ? new TextColumn<TModel, TValue>(header, getter, options.Width, internalOptions)
                : new TextColumn<TModel, TValue>(header, getter, setter, options.Width, internalOptions));
        }

        private static void AddCheckBoxColumn<TModel>(
            ColumnList<TModel> columns,
            object? header,
            Expression<Func<TModel, bool>> getter,
            Action<CheckBoxColumnCreateOptions<TModel>>? configure)
            where TModel : class
        {
            var options = new CheckBoxColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            header ??= TreeDataGridExpressionHelper.TryGetMemberName(getter) ?? string.Empty;

            var internalOptions = TreeDataGridColumnOptionsFactory.CreateCheckBoxOptions<TModel>(options);

            var setter = options.IsReadOnly ? null : TreeDataGridExpressionHelper.TryCreateNonNullableSetter(getter);
            columns.Add(new CheckBoxColumn<TModel>(header, getter, setter, options.Width, internalOptions));
        }

        private static void AddThreeStateCheckBoxColumn<TModel>(
            ColumnList<TModel> columns,
            object? header,
            Expression<Func<TModel, bool?>> getter,
            Action<CheckBoxColumnCreateOptions<TModel>>? configure)
            where TModel : class
        {
            var options = new CheckBoxColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            header ??= TreeDataGridExpressionHelper.TryGetMemberName(getter) ?? string.Empty;

            var internalOptions = TreeDataGridColumnOptionsFactory.CreateCheckBoxOptions<TModel>(options);

            var setter = options.IsReadOnly ? null : TreeDataGridExpressionHelper.TryCreateSetter(getter);
            columns.Add(new CheckBoxColumn<TModel>(header, getter, setter, options.Width, internalOptions));
        }

        private static void AddTemplateColumn<TModel>(
            ColumnList<TModel> columns,
            object? header,
            DataTemplate cellTemplate,
            DataTemplate? cellEditingTemplate,
            Action<TemplateColumnCreateOptions<TModel>>? configure)
            where TModel : class
        {
            var options = new TemplateColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            var internalOptions = TreeDataGridColumnOptionsFactory.CreateTemplateOptions<TModel>(
                options,
                TreeDataGridBindingAccessor.TryCreateTextSelector<TModel>(options.TextSearchBinding));
            columns.Add(new TemplateColumn<TModel>(header, cellTemplate, cellEditingTemplate, options.Width, internalOptions));
        }

        private static void AddTemplateColumn<TModel>(
            ColumnList<TModel> columns,
            object? header,
            object cellTemplateResourceKey,
            object? cellEditingTemplateResourceKey,
            Action<TemplateColumnCreateOptions<TModel>>? configure)
            where TModel : class
        {
            var options = new TemplateColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            var internalOptions = TreeDataGridColumnOptionsFactory.CreateTemplateOptions<TModel>(
                options,
                TreeDataGridBindingAccessor.TryCreateTextSelector<TModel>(options.TextSearchBinding));
            columns.Add(new TemplateColumn<TModel>(header, cellTemplateResourceKey, cellEditingTemplateResourceKey, options.Width, internalOptions));
        }

        private static void AddHierarchicalExpanderTextColumn<TModel, TValue>(
            ColumnList<TModel> columns,
            object? header,
            Expression<Func<TModel, TValue?>> getter,
            Func<TModel, System.Collections.Generic.IEnumerable<TModel>?> childSelector,
            Action<HierarchicalExpanderColumnCreateOptions<TModel>>? configure)
            where TModel : class
        {
            var options = new HierarchicalExpanderColumnCreateOptions<TModel>();
            configure?.Invoke(options);
            var innerOptions = new TextColumnCreateOptions<TModel>
            {
                Width = options.Width,
            };
            var internalOptions = TreeDataGridColumnOptionsFactory.CreateTextOptions<TModel>(innerOptions);
            var setter = TreeDataGridExpressionHelper.TryCreateSetter(getter);
            var innerHeader = header ?? TreeDataGridExpressionHelper.TryGetMemberName(getter) ?? string.Empty;
            var inner = setter is null
                ? new TextColumn<TModel, TValue>(innerHeader, getter, options.Width, internalOptions)
                : new TextColumn<TModel, TValue>(innerHeader, getter, setter, options.Width, internalOptions);
            columns.Add(new HierarchicalExpanderColumn<TModel>(inner, childSelector, options.HasChildren, options.IsExpanded));
        }
    }
}
