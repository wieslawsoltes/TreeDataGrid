using System;
using System.Collections.Generic;
using System.ComponentModel;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
namespace TreeDataGridCore
{
    public enum RowDropPosition { None, Before, After, Inside }
    [Flags] public enum RowMoveEffects { None = 0, Copy = 1, Move = 2, Link = 4 }
    public interface ITreeDataGridSource : INotifyPropertyChanged
    {
        TResult Accept<TResult>(ITreeDataGridSourceVisitor<TResult> visitor);
        IReadOnlyList<IColumn> Columns { get; }
        IRows Rows { get; }
        ITreeDataGridSelection? Selection { get; set; }
        bool IsHierarchical { get; }
        bool IsSorted { get; }
        IEnumerable<object> Items { get; }
        IEnumerable<object>? GetModelChildren(object model);
        bool SortBy(IColumn? column, ListSortDirection direction);
        void ClearSort();
        event Action? Sorted;
        void MoveRows(ITreeDataGridSource source, IEnumerable<IndexPath> indexes, IndexPath targetIndex, RowDropPosition position, RowMoveEffects effects = RowMoveEffects.Move);
    }
    public interface ITreeDataGridSourceVisitor<out TResult>
    {
        TResult Visit<TModel>(ITreeDataGridSource<TModel> source) where TModel : class;
    }
    public interface ITreeDataGridSource<TModel> : ITreeDataGridSource
    {
        new IEnumerable<TModel> Items { get; set; }
    }
}
