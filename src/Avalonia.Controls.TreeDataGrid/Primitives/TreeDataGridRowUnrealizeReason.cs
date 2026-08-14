namespace Avalonia.Controls.Primitives
{
    /// <summary>
    /// Describes why a realized <see cref="TreeDataGridRow"/> is being unrealized.
    /// </summary>
    public enum TreeDataGridRowUnrealizeReason
    {
        /// <summary>
        /// The row is leaving the realized viewport and may be recycled for another row.
        /// </summary>
        Recycle,

        /// <summary>
        /// The row's item was removed from the source.
        /// </summary>
        ItemRemoved,
    }
}
