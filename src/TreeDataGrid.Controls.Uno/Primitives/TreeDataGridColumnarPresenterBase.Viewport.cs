using Windows.Foundation;

namespace Uno.Controls.Primitives;

public abstract partial class TreeDataGridColumnarPresenterBase<TItem>
{
    // The existing stable realized range is authoritative. This checks coverage,
    // not cached native measure validity, and retains no source/model references.
    internal bool CoversHorizontalViewport(double offset, double width) =>
        IsViewportCoveredByRealizedElements(new Rect(offset, 0, width, 0));
}
