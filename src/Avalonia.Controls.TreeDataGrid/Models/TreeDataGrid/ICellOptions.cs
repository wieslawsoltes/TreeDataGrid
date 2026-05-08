using Avalonia.Media;

#if TREE_DATAGRID_UNO

namespace Uno.Controls.Models.TreeDataGrid

#else

namespace Avalonia.Controls.Models.TreeDataGrid

#endif
{
    public interface ICellOptions
    {
        /// <summary>
        /// Gets the gesture(s) that will cause the cell to enter edit mode.
        /// </summary>
        BeginEditGestures BeginEditGestures { get; }
    }
}
