using System;
using System.Globalization;
#if TREE_DATAGRID_UNO
using Uno.Controls.Models.TreeDataGrid;
#else
using Avalonia.Controls.Models.TreeDataGrid;
#endif
#if TREE_DATAGRID_UNO

namespace Uno.Controls

#else

namespace Avalonia.Controls

#endif
{
    internal static class TreeDataGridColumnOptionsFactory
    {
        public static ColumnOptions<TModel> CreateCommonOptions<TModel>(TreeDataGridColumn source)
        {
            var result = new ColumnOptions<TModel>();
            ApplyCommonOptions(result, source);
            return result;
        }

        public static ColumnOptions<TModel> CreateCommonOptions<TModel>(ColumnCreateOptions source)
        {
            var result = new ColumnOptions<TModel>();
            ApplyCommonOptions(result, source);
            return result;
        }

        public static CheckBoxColumnOptions<TModel> CreateCheckBoxOptions<TModel>(TreeDataGridColumn source)
        {
            var result = new CheckBoxColumnOptions<TModel>();
            ApplyCommonOptions(result, source);
            return result;
        }

        public static CheckBoxColumnOptions<TModel> CreateCheckBoxOptions<TModel>(CheckBoxColumnCreateOptions source)
        {
            var result = new CheckBoxColumnOptions<TModel>();
            ApplyCommonOptions(result, source);
            return result;
        }

        public static TextColumnOptions<TModel> CreateTextOptions<TModel>(TreeDataGridTextColumn source)
        {
            var result = new TextColumnOptions<TModel>
            {
                IsTextSearchEnabled = source.IsTextSearchEnabled,
                StringFormat = source.StringFormat,
                Culture = source.Culture,
                TextAlignment = source.TextAlignment,
                TextTrimming = source.TextTrimming,
                TextWrapping = source.TextWrapping,
            };

            ApplyCommonOptions(result, source);
            return result;
        }

        public static TextColumnOptions<TModel> CreateTextOptions<TModel>(TextColumnCreateOptions source)
        {
            var result = new TextColumnOptions<TModel>
            {
                IsTextSearchEnabled = source.IsTextSearchEnabled,
                StringFormat = source.StringFormat,
                TextAlignment = source.TextAlignment,
                TextTrimming = source.TextTrimming,
                TextWrapping = source.TextWrapping,
            };

            if (source.Culture is CultureInfo culture)
                result.Culture = culture;

            ApplyCommonOptions(result, source);
            return result;
        }

        public static TemplateColumnOptions<TModel> CreateTemplateOptions<TModel>(
            TreeDataGridTemplateColumn source,
            Func<TModel, string?>? textSearchValueSelector)
        {
            var result = new TemplateColumnOptions<TModel>
            {
                IsTextSearchEnabled = source.TextSearchBinding is not null,
                TextSearchValueSelector = textSearchValueSelector,
            };

            ApplyCommonOptions(result, source);
            return result;
        }

        public static TemplateColumnOptions<TModel> CreateTemplateOptions<TModel>(
            TemplateColumnCreateOptions source,
            Func<TModel, string?>? textSearchValueSelector)
        {
            var result = new TemplateColumnOptions<TModel>
            {
                IsTextSearchEnabled = source.TextSearchBinding is not null,
                TextSearchValueSelector = textSearchValueSelector,
            };

            ApplyCommonOptions(result, source);
            return result;
        }

        public static void ApplyCommonOptions<TModel>(ColumnOptions<TModel> target, TreeDataGridColumn source)
        {
            target.CanUserResizeColumn = source.CanUserResize;
            target.CanUserSortColumn = source.CanUserSortColumn;
            target.MinWidth = source.MinWidth;
            target.MaxWidth = source.MaxWidth;
            target.BeginEditGestures = source.BeginEditGestures;
            target.CompareAscending = source.CompareAscending is null ? null : (a, b) => source.CompareAscending(a, b);
            target.CompareDescending = source.CompareDescending is null ? null : (a, b) => source.CompareDescending(a, b);
        }

        public static void ApplyCommonOptions<TModel>(ColumnOptions<TModel> target, ColumnCreateOptions source)
        {
            target.CanUserResizeColumn = source.CanUserResize;
            target.CanUserSortColumn = source.CanUserSortColumn;
            target.MinWidth = source.MinWidth;
            target.MaxWidth = source.MaxWidth;
            target.BeginEditGestures = source.BeginEditGestures;
            target.CompareAscending = source.CompareAscending is null ? null : (a, b) => source.CompareAscending(a, b);
            target.CompareDescending = source.CompareDescending is null ? null : (a, b) => source.CompareDescending(a, b);
        }
    }
}
