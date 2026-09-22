using System;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace TreeDataGridUnoSample;

/// <summary>
/// A framework-only regression: no TreeDataGrid control, source, presenter,
/// binding, factory or template participates in this measurement sequence.
/// </summary>
internal static class NativeLayoutRecoveryRuntimeChecks
{
    public static void Run()
    {
        var probe = new MeasureProbe();
        var constraint = new Size(300, 200);
        probe.Measure(constraint);
        Check(probe.MeasureCalls > 0 && Math.Abs(probe.DesiredSize.Width - 40) < 0.01,
            "The native measurement probe did not initialize.");

        probe.ThrowOnNextMeasure = true;
        probe.InvalidateMeasure();
        var beforeThrow = probe.MeasureCalls;
        Exception? observed = null;
        try { probe.Measure(constraint); }
        catch (Exception error) { observed = error; }
        Check(probe.MeasureCalls == beforeThrow + 1 && observed is not null,
            "The native measurement probe did not execute and propagate its deliberate exception.");

        // Use the SAME constraint. A changed constraint forces DoMeasure even
        // when invalidation is broken and would conceal a sticky MeasuringSelf
        // flag in the underlying framework. This is the recovery contract a
        // reused grid needs after a caller catches an application callback error.
        probe.NaturalWidth = 80;
        var beforeRecovery = probe.MeasureCalls;
        probe.InvalidateMeasure();
        probe.Measure(constraint);
        Console.WriteLine($"UNO_NATIVE_LAYOUT_RECOVERY_STATE: framework={typeof(Control).Assembly.FullName}; " +
            $"exception={observed!.GetType().FullName}; before={beforeRecovery}; after={probe.MeasureCalls}; " +
            $"actualWidth={probe.DesiredSize.Width}; expectedWidth={probe.NaturalWidth}; constraint={constraint}");
        Check(probe.MeasureCalls > beforeRecovery && Math.Abs(probe.DesiredSize.Width - probe.NaturalWidth) < 0.01,
            "Native Control.InvalidateMeasure stopped working after a handled MeasureOverride exception. " +
            "The reproduction contains no TreeDataGrid code; a dependency with exception-safe native measurement is required.");

        probe.NaturalWidth = 120;
        probe.InvalidateMeasure();
        probe.Measure(constraint);
        Check(Math.Abs(probe.DesiredSize.Width - 120) < 0.01,
            "The recovered native control failed a subsequent ordinary invalidation.");
        Console.WriteLine("UNO_RUNTIME_NATIVE_LAYOUT_RECOVERY_PASSED: bare native Control, deliberate measurement exception, same-constraint invalidation and repeated recovery");
    }

    private sealed partial class MeasureProbe : Control
    {
        public int MeasureCalls { get; private set; }
        public double NaturalWidth { get; set; } = 40;
        public bool ThrowOnNextMeasure { get; set; }
        protected override Size MeasureOverride(Size availableSize)
        {
            ++MeasureCalls;
            if (ThrowOnNextMeasure)
            {
                ThrowOnNextMeasure = false;
                throw new InvalidOperationException("Deliberate native measurement recovery probe.");
            }
            return new Size(NaturalWidth, 24);
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
