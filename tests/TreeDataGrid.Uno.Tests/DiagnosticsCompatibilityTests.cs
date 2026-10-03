using Xunit;
using global::Uno.Controls;
using global::Uno.Controls.Primitives;

namespace TreeDataGrid.Uno.Tests;

public sealed class DiagnosticsCompatibilityTests
{
    [Fact]
    public void Public_diagnostics_controls_the_existing_presenter_trace_state()
    {
        var previous = TreeDataGridDiagnostics.EnableTracing;
        try
        {
            TreeDataGridDiagnostics.EnableTracing = true;
            Assert.True(PresenterDiagnostics.EnableTracing);
            PresenterDiagnostics.EnableTracing = false;
            Assert.False(TreeDataGridDiagnostics.EnableTracing);
            PresenterDiagnostics.EnableTracing = true;
            Assert.True(TreeDataGridDiagnostics.EnableTracing);
            TreeDataGridDiagnostics.EnableTracing = false;
            Assert.False(PresenterDiagnostics.EnableTracing);
        }
        finally { TreeDataGridDiagnostics.EnableTracing = previous; }
    }
}
