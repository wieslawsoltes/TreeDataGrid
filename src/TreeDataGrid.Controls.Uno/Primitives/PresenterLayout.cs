using System;
using Microsoft.UI.Xaml.Controls;

namespace Uno.Controls.Primitives;

internal interface IFinalMeasureSelector
{
    bool NeedsFinalMeasure(Control element, int index);
}

internal static class PresenterDiagnostics
{
    public static bool EnableTracing { get; set; }
}

// Same tolerance as the reference layout implementation.
internal static class PresenterMath
{
    private const double DoubleEpsilon = 2.2204460492503131e-016;
    public static bool IsZero(double value) => Math.Abs(value) < 10 * DoubleEpsilon;
    public static bool AreClose(double left, double right)
    {
        if (left == right) return true;
        var epsilon = (Math.Abs(left) + Math.Abs(right) + 10) * DoubleEpsilon;
        var delta = left - right;
        return -epsilon < delta && delta < epsilon;
    }
    public static bool GreaterThan(double left, double right) => left > right && !AreClose(left, right);
}
