using System.Collections;

#if TREE_DATAGRID_UNO

namespace Uno.Controls.Selection

#else

namespace Avalonia.Controls.Selection

#endif
{
    public interface ITreeDataGridSelection
    {
        IEnumerable? Source { get; set; }
    }
}
