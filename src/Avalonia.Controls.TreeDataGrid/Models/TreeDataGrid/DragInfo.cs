using System;
using System.Collections.Generic;

namespace Avalonia.Controls.Models.TreeDataGrid
{
    /// <summary>
    /// Holds information about an automatic row drag/drop operation carried out
    /// by <see cref="Avalonia.Controls.TreeDataGrid.AutoDragDropRows"/>.
    /// </summary>
    public class DragInfo
    {
        /// <summary>
        /// Defines the data format in an <see cref="Avalonia.Input.IDataObject"/>.
        /// </summary>
        public const string DataFormat = "TreeDataGridDragInfo";

        /// <summary>
        /// Initializes a new instance of the <see cref="DragInfo"/> class.
        /// </summary>
        /// <param name="source">The source of the drag operation/</param>
        /// <param name="indexes">The indexes being dragged.</param>
        public DragInfo(ITreeDataGridSource source, IEnumerable<IndexPath> indexes)
        { _source = source; SourceIdentity = source; Indexes = indexes; }

        internal DragInfo(global::Avalonia.Controls.Presentation.TreeDataGridPresentation presentation, IEnumerable<IndexPath> indexes)
        {
            SourceIdentity = presentation.SourceIdentity;
            _source = SourceIdentity as ITreeDataGridSource;
            Model = SourceIdentity as global::TreeDataGridCore.ITreeDataGridSource;
            Indexes = indexes;
        }

        /// <summary>The legacy source, when the drag originated from the compatibility API.</summary>
        private readonly ITreeDataGridSource? _source;
        public ITreeDataGridSource Source => _source ?? throw new InvalidOperationException("This drag originates from the Core API. Use Model to access its source.");
        /// <summary>The Core source, when the drag originated from the primary API.</summary>
        public global::TreeDataGridCore.ITreeDataGridSource? Model { get; }
        internal object SourceIdentity { get; }

        /// <summary>
        /// Gets or sets the model indexes of the rows being dragged.
        /// </summary>
        public IEnumerable<IndexPath> Indexes { get; }
    }
}
