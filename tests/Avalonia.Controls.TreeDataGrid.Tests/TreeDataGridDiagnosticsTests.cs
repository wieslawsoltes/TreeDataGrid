using System;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls.Primitives;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests
{
    public class TreeDataGridDiagnosticsTests
    {
        [Fact]
        public void Tracing_Is_OptIn_And_Trace_Calls_Are_Debug_Only()
        {
            Assert.False(TreeDataGridDiagnostics.EnableTracing);

            AssertDebugConditional(typeof(TreeDataGridRowsPresenter).BaseType!);

            var realizedElementsType = typeof(TreeDataGrid).Assembly.GetType(
                "Avalonia.Controls.Primitives.RealizedStackElements",
                throwOnError: true)!;
            AssertDebugConditional(realizedElementsType);
        }

        private static void AssertDebugConditional(Type type)
        {
            var trace = type.GetMethod("Trace", BindingFlags.Instance | BindingFlags.NonPublic);
            var conditional = Assert.IsType<ConditionalAttribute>(
                trace?.GetCustomAttribute(typeof(ConditionalAttribute)));

            Assert.Equal("DEBUG", conditional.ConditionString);
        }
    }
}
