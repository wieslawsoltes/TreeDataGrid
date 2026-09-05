using System;

namespace Avalonia.Controls.Models.TreeDataGrid
{
    /// <summary>
    /// Represents a cell in a <see cref="HierarchicalTreeDataGridSource{TModel}"/> which displays
    /// an expander to reveal nested data.
    /// </summary>
    public interface IExpanderCell : ICell, IExpander, IExpanderCellPresentation
    {
        /// <summary>
        /// Gets the cell content.
        /// </summary>
        new object? Content { get; }
        object? IExpanderCellPresentation.Content => Content;

        /// <summary>
        /// Gets the row that the cell belongs to.
        /// </summary>
        new IRow Row { get; }
        global::TreeDataGridCore.Models.IRow IExpanderCellPresentation.Row => Row;
    }
}
