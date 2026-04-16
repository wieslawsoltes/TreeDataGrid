using System;
using System.ComponentModel;

#if TREE_DATAGRID_UNO

namespace Uno.Controls.Models.TreeDataGrid

#else

namespace Avalonia.Controls.Models.TreeDataGrid

#endif
{
    internal class TreeDataGridRowHeaderColumnInternal<TModel> : ColumnBase<TModel>
    {
        public TreeDataGridRowHeaderColumnInternal(
            object? header = null,
            GridLength? width = null,
            ColumnOptions<TModel>? options = null)
            : base(header, width, options ?? new())
        {
        }

        public override ICell CreateCell(IRow<TModel> row)
        {
            if (row is IModelIndexableRow indexable)
                return new TextCell<string>((indexable.ModelIndexPath[^1] + 1).ToString());

            return new TextCell<string>(string.Empty);
        }

        public override Comparison<TModel?>? GetComparison(ListSortDirection direction)
        {
            return null;
        }
    }
}
