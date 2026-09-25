using System;

namespace Uno.Controls.Presentation;

public partial class ValueCellColumn<TModel, TValue>
{
    /// <summary>Gets the committed width, or the configured pixel width before first layout.</summary>
    /// <remarks>
    /// Pixel columns have a useful initial width without realizing a header/cell.
    /// Auto and star columns remain unmeasured until their normal layout commit.
    /// A committed constrained width remains authoritative, including zero.
    /// This fallback neither records a natural measurement nor mutates Core.
    /// </remarks>
    public override double ActualWidth
    {
        get
        {
            var actual = base.ActualWidth;
            if (!double.IsNaN(actual)) return actual;
            var width = Width;
            return width.IsAbsolute ? width.Value : actual;
        }
    }
}
