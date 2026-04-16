using System;

#if TREE_DATAGRID_UNO

namespace Uno.Controls.Models.TreeDataGrid

#else

namespace Avalonia.Controls.Models.TreeDataGrid

#endif
{
    /// <summary>
    /// Holds less commonly-used options for a <see cref="CheckBoxColumn{TModel}"/>.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    public class CheckBoxColumnOptions<TModel> : ColumnOptions<TModel>
    {
    }
}
