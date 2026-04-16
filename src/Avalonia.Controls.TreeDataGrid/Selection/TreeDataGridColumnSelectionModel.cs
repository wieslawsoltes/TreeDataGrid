#if TREE_DATAGRID_UNO
using Uno.Controls.Models.TreeDataGrid;
#else
using Avalonia.Controls.Models.TreeDataGrid;
#endif

#if TREE_DATAGRID_UNO

namespace Uno.Controls.Selection

#else

namespace Avalonia.Controls.Selection

#endif
{
    public class TreeDataGridColumnSelectionModel : SelectionModel<IColumn>,
        ITreeDataGridColumnSelectionModel
    {
        public TreeDataGridColumnSelectionModel(IColumns columns)
            : base(columns)
        {
        }
    }
}
