using System;
using Avalonia.Controls.Adapters;
using Avalonia.Controls.Models.TreeDataGrid;
using Core = global::TreeDataGridCore;
namespace Avalonia.Controls.Presentation
{
    internal sealed class CellColumnFactory<TModel> : Core.Models.IColumnVisitor<TModel, ICellColumn<TModel>> where TModel : class
    {
        private readonly TreeDataGridPresentationOptions<TModel> _options;
        public CellColumnFactory(TreeDataGridPresentationOptions<TModel> options) => _options = options;
        public ICellColumn<TModel> Visit<TValue>(Core.Models.ValueColumn<TModel, TValue> column)
        {
            if (column.PresentationKey is { } key && _options.Columns.TryGetValue(key, out var factory)) return factory(column);
            if (column is Core.Models.CheckBoxColumn<TModel> checkBox)
                return new CheckBoxColumn<TModel>(checkBox, CopyOptions(column, new CheckBoxColumnOptions<TModel>()));
            if (column.PresentationKey is { } missing) throw new InvalidOperationException($"No TreeDataGrid column presentation is registered for '{missing}'.");
            return new TextColumn<TModel, TValue>(column!, CopyOptions(column, new TextColumnOptions<TModel>()));
        }
        public ICellColumn<TModel> Visit(Core.Models.HierarchicalExpanderColumn<TModel> column) =>
            new ExpanderColumnPresentation<TModel>(column, column.Inner.Accept(this));
        private static TOptions CopyOptions<TValue, TOptions>(Core.Models.ValueColumn<TModel, TValue> column, TOptions options) where TOptions : ColumnOptions<TModel>
        {
            options.CanUserResizeColumn = column.Options.CanUserResizeColumn;
            options.CanUserSortColumn = false;
            options.MinWidth = column.Options.MinWidth.ToAvalonia();
            options.MaxWidth = column.Options.MaxWidth?.ToAvalonia();
            // Sorting belongs to Core; these columns contain only view binding and layout state.
            return options;
        }
    }

}
