namespace Avalonia.Controls.Models.TreeDataGrid
{
    /// <summary>
    /// Represents an element which may expand.
    /// </summary>
    public interface IExpander : global::TreeDataGridCore.Models.IExpander
    {
        /// <summary>
        /// Gets or sets a value indicating whether the element is expanded.
        /// </summary>
        new bool IsExpanded { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether expander should be shown.
        /// </summary>
        new bool ShowExpander { get; }
    }
}
