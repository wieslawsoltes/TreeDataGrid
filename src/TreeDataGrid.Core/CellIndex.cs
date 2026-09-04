namespace TreeDataGridCore
{
    /// <summary>
    /// Represents a cell in a TreeDataGrid source.
    /// </summary>
    /// <param name="ColumnIndex">
    /// The index of the cell in the <see cref="ITreeDataGridSource.Columns"/> collection.
    /// </param>
    /// <param name="RowIndex">
    /// The hierarchical index of the row model in the data source.
    /// </param>
    public readonly record struct CellIndex(int ColumnIndex, IndexPath RowIndex);
}
