using System;

namespace Avalonia.Controls.Utils
{
    internal static class DoubleUtils
    {
        // Copied from Avalonia.Utilities.MathUtilities (double path).
        // smallest such that 1.0 + DoubleEpsilon != 1.0
        private const double DoubleEpsilon = 2.2204460492503131e-016;

        public static bool IsZero(double value) => Math.Abs(value) < 10.0 * DoubleEpsilon;

        public static bool AreClose(double value1, double value2)
        {
            // In case they are infinities (then epsilon check does not work).
            if (value1 == value2)
                return true;

            var eps = (Math.Abs(value1) + Math.Abs(value2) + 10.0) * DoubleEpsilon;
            var delta = value1 - value2;
            return (-eps < delta) && (eps > delta);
        }

        public static bool GreaterThan(double value1, double value2) =>
            (value1 > value2) && !AreClose(value1, value2);
    }
}
