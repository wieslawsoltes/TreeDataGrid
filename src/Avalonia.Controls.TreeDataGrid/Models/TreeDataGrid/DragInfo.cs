using System.Collections.Generic;
using Avalonia.Input;

#if TREE_DATAGRID_UNO

namespace Uno.Controls.Models.TreeDataGrid

#else

namespace Avalonia.Controls.Models.TreeDataGrid

#endif
{
    /// <summary>
    /// Holds information about an automatic row drag/drop operation carried out
    /// by <see cref="Avalonia.Controls.TreeDataGrid.AutoDragDropRows"/>.
    /// </summary>
    public class DragInfo
    {
        /// <summary>
        /// Defines the data format that carries the drag token in an <see cref="IDataTransfer"/>.
        /// </summary>
        public static readonly DataFormat<string> DataFormat =
            Avalonia.Input.DataFormat.CreateStringApplicationFormat("TreeDataGridDragInfo");

        /// <summary>
        /// Initializes a new instance of the <see cref="DragInfo"/> class.
        /// </summary>
        /// <param name="source">The source of the drag operation/</param>
        /// <param name="indexes">The indexes being dragged.</param>
        public DragInfo(ITreeDataGridSource source, IEnumerable<IndexPath> indexes)
        {
            Source = source;
            Indexes = indexes;
        }

        /// <summary>
        /// Gets the <see cref="ITreeDataGridSource"/> that rows are being dragged from.
        /// </summary>
        public ITreeDataGridSource Source { get; }

        /// <summary>
        /// Gets or sets the model indexes of the rows being dragged.
        /// </summary>
        public IEnumerable<IndexPath> Indexes { get; }
    }
}
