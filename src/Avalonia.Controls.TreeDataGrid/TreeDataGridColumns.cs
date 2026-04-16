using Avalonia.Collections;

#if TREE_DATAGRID_UNO

namespace Uno.Controls

#else

namespace Avalonia.Controls

#endif
{
    public class TreeDataGridColumns : AvaloniaList<TreeDataGridColumn>
    {
    }
}
