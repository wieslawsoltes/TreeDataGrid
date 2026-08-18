using System;
using Avalonia.Experimental.Data;

#if TREE_DATAGRID_UNO

namespace Uno.Controls.Models.TreeDataGrid

#else

namespace Avalonia.Controls.Models.TreeDataGrid

#endif
{
    internal class ReflectionCheckBoxColumn : ColumnBase<object, bool?>, IReusableCellColumn<object>
    {
        private readonly bool _isThreeState;

        public ReflectionCheckBoxColumn(
            object? header,
            Func<object, bool?> getter,
            Action<object, bool?>? setter,
            Func<object, object>[] links,
            GridLength width,
            CheckBoxColumnOptions<object> options,
            bool isThreeState)
            : base(header, getter, CreateBinding(getter, setter, links), width, options)
        {
            _isThreeState = isThreeState;
        }

        public override ICell CreateCell(IRow<object> row)
        {
            return new CheckBoxCell(CreateBindingExpression(row.Model), Binding.Write is null, _isThreeState);
        }

        bool IReusableCellColumn<object>.TryReuseCell(ICell cell, IRow<object> row)
        {
            return cell is CheckBoxCell checkBoxCell && checkBoxCell.TrySetSource(row.Model);
        }

        private static TypedBinding<object, bool?> CreateBinding(
            Func<object, bool?> getter,
            Action<object, bool?>? setter,
            Func<object, object>[] links)
        {
            return new TypedBinding<object, bool?>
            {
                Read = getter,
                Write = setter,
                Links = links,
            };
        }
    }
}
