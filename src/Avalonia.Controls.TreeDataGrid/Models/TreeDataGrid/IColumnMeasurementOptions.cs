namespace Avalonia.Controls.Models.TreeDataGrid
{
    /// <summary>Controls whether a column needs natural-width measurement before final layout.</summary>
    public interface IColumnMeasurementOptions
    {
        /// <summary>Return false when a known actual width can be used for the initial measure.</summary>
        bool RequiresUnconstrainedWidthMeasurement { get; }
    }
}
