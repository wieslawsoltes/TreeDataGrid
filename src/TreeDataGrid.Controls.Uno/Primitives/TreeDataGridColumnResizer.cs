using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Uno.Controls.Primitives;

/// <summary>Native header grip; Uno/WinUI expose cursor configuration to subclasses.</summary>
public class TreeDataGridColumnResizer : Thumb
{
    private InputSystemCursor? _resizeCursor;
    public TreeDataGridColumnResizer()
    {
        Loaded += (_, _) => ProtectedCursor = _resizeCursor ??= InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        Unloaded += (_, _) =>
        {
            ProtectedCursor = null;
            _resizeCursor?.Dispose();
            _resizeCursor = null;
        };
    }
}
