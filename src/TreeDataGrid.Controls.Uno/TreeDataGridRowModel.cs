using TreeDataGridCore;

namespace Uno.Controls;

/// <summary>
/// Captures a row's model and model-space index without retaining a reusable
/// row container or Core's mutable anonymous-row wrapper.
/// </summary>
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
