namespace Avalonia.Controls
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
