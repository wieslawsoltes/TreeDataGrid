using System;
using System.Collections.Generic;

namespace TreeDataGridCore.Selection
{
    public interface ITreeDataGridRowSelectionModel : ITreeSelectionModel, ITreeDataGridSelection
    {
        event EventHandler? StateChanged;
    }

    public interface ITreeDataGridRowSelectionModel<T> : ITreeDataGridRowSelectionModel
    {
        new T? SelectedItem { get; }
        new IReadOnlyList<T?> SelectedItems { get; }
        new event EventHandler<TreeSelectionModelSelectionChangedEventArgs<T>>? SelectionChanged;
    }
}
