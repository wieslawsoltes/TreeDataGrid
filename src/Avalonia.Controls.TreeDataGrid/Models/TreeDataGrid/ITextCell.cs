using System.Globalization;

using Avalonia.Media;

#if TREE_DATAGRID_UNO

namespace Uno.Controls.Models.TreeDataGrid

#else

namespace Avalonia.Controls.Models.TreeDataGrid

#endif
{
    /// <summary>
    /// Represents a text cell in an <see cref="ITreeDataGridSource"/>.
    /// </summary>
    public interface ITextCell : ICell
    {
        /// <summary>
        /// Gets or sets the cell's value as a string.
        /// </summary>
        string? Text { get; set; }

        /// <summary>
        /// Gets the cell's text trimming mode.
        /// </summary>
        TextTrimming TextTrimming { get; }

        /// <summary>
        /// Gets the cell's text wrapping mode.
        /// </summary>
        TextWrapping TextWrapping { get; }

        /// <summary>
        /// Gets the cell's text alignment mode.
        /// </summary>
        TextAlignment TextAlignment { get; }
    }
}
