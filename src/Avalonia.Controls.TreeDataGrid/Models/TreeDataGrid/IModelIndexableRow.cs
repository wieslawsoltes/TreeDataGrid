namespace Avalonia.Controls.Models.TreeDataGrid
{
    /// <summary>
    /// Represents a row from an integer indexed data source.
    /// </summary>
    public interface IModelIndexableRow : IRow, global::TreeDataGridCore.Models.IModelIndexableRow
    {
        /// <summary>
        /// Gets the index of the model in its parent data source.
        /// </summary>
        new int ModelIndex { get; }

        /// <summary>
        /// Gets the index of the model from the root data source.
        /// </summary>
        new IndexPath ModelIndexPath { get; }
        global::TreeDataGridCore.IndexPath global::TreeDataGridCore.Models.IModelIndexableRow.ModelIndexPath => ModelIndexPath.ToCoreIndexPath();
    }
}
