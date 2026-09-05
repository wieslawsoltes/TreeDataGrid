namespace TreeDataGridCore.Models
{
    /// <summary>
    /// Represents a row which can be indented to represent nested data.
    /// </summary>
    public interface IIndentedRow : IRow
    {
        /// <summary>
        /// Gets the row indent level.
        /// </summary>
        int Indent { get; }
    }
}
