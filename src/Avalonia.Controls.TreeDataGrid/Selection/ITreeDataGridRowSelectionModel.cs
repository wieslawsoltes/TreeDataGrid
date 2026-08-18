using System;
using System.Collections.Generic;
#if TREE_DATAGRID_UNO
using Uno.Controls;
#else
using Avalonia.Controls;
#endif
#if TREE_DATAGRID_UNO

namespace Uno.Controls.Selection

#else

namespace Avalonia.Controls.Selection

#endif
{
    public interface ITreeDataGridRowSelectionModel : ITreeSelectionModel, ITreeDataGridSelection
    {
    }

    public interface ITreeDataGridRowSelectionModel<T> : ITreeDataGridRowSelectionModel
    {
        new T? SelectedItem { get; }
        new IReadOnlyList<T?> SelectedItems { get; }
        new event EventHandler<TreeDataGridSelectionChangedEventArgs<T>>? SelectionChanged;
    }
}
