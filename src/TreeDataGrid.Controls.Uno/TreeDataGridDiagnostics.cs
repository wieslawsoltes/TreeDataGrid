namespace Uno.Controls;

/// <summary>Provides diagnostic settings for TreeDataGrid.</summary>
public static class TreeDataGridDiagnostics
{
    /// <summary>
    /// Gets or sets whether diagnostic tracing is enabled. Detailed viewport,
    /// realization and attachment traces are written to
    /// <see cref="System.Diagnostics.Debug"/> in DEBUG builds.
    /// </summary>
    /// <remarks>
    /// Shares the existing presenter setting; no second flag, event subscription
    /// or source reference is created. Release tracing call sites remain omitted.
    /// </remarks>
    public static bool EnableTracing
    {
        get => Primitives.PresenterDiagnostics.EnableTracing;
        set => Primitives.PresenterDiagnostics.EnableTracing = value;
    }
}
