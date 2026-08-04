namespace Avalonia.Controls
{
    /// <summary>
    /// Provides diagnostic settings for TreeDataGrid.
    /// </summary>
    public static class TreeDataGridDiagnostics
    {
        /// <summary>
        /// Gets or sets a value indicating whether diagnostic tracing is enabled.
        /// When enabled, detailed trace output is written to <see cref="System.Diagnostics.Debug"/>
        /// for viewport changes, element realization, and attach/detach operations.
        /// This setting is only active in DEBUG builds.
        /// </summary>
        public static bool EnableTracing { get; set; }
    }
}
