#if TREE_DATAGRID_UNO
namespace Uno.Controls
#else
namespace Avalonia.Controls
#endif
{
    public class TreeDataGridRowModel
    {
        public TreeDataGridRowModel(object? model, IndexPath modelIndexPath)
        {
            Model = model;
            ModelIndexPath = modelIndexPath;
        }

        public object? Model { get; }
        public IndexPath ModelIndexPath { get; }
    }
}
