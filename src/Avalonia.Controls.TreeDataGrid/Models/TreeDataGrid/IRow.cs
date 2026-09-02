namespace Avalonia.Controls.Models.TreeDataGrid
{
    /// <summary>
    /// Represents a row in an <see cref="ITreeDataGridSource"/>.
    /// </summary>
    public interface IRow : global::TreeDataGridCore.Models.IRow
    {
        /// <summary>
        /// Gets the row header.
        /// </summary>
        new object? Header { get; }

        /// <summary>
        /// Gets the height of the row.
        /// </summary>
        new GridLength Height { get; set; }

        /// <summary>
        /// Gets the row model.
        /// </summary>
        new object? Model { get; }

        object? global::TreeDataGridCore.Models.IRow.Header => Header;
        object? global::TreeDataGridCore.Models.IRow.Model => Model;
        global::TreeDataGridCore.GridLength global::TreeDataGridCore.Models.IRow.Height
        {
            get => new(Height.Value, (global::TreeDataGridCore.GridUnitType)Height.GridUnitType);
            set => Height = new(value.Value, (GridUnitType)value.GridUnitType);
        }
    }
}
