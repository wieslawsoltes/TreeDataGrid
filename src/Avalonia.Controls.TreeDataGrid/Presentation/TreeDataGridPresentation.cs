using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Core = global::TreeDataGridCore;

namespace Avalonia.Controls.Presentation
{
    /// <summary>View-owned layout, cell realization and input state. Never owns the source model.</summary>
    public abstract class TreeDataGridPresentation : INotifyPropertyChanged, IDisposable
    {
        public abstract object SourceIdentity { get; }
        public abstract bool CanSelectMultiple { get; }
        public abstract void Select(IndexPath index, bool replace);
        public abstract void Deselect(IndexPath index);
        public abstract bool IsSelected(IndexPath index);
        public abstract IColumns Columns { get; }
        public abstract ITreeDataGridRows Rows { get; }
        public abstract bool IsHierarchical { get; }
        public abstract bool IsSorted { get; }
        public abstract ITreeDataGridSelectionInteraction? SelectionInteraction { get; }
        public abstract IReadOnlyList<object?>? SelectedItems { get; }
        public abstract IReadOnlyList<IndexPath>? SelectedIndexes { get; }
        public abstract event PropertyChangedEventHandler? PropertyChanged;
        public abstract event Action? Sorted;
        public abstract bool SortBy(IColumn column, ListSortDirection direction);
        public abstract void MoveRows(IEnumerable<IndexPath> indexes, IndexPath target, TreeDataGridRowDropPosition position, DragDropEffects effects);
        public abstract void Dispose();
        internal virtual void Suspend() { }
        internal virtual void Resume() { }
        internal event EventHandler<TreeDataGridSelectionChangedEventArgs>? NativeSelectionChanged;
        internal virtual void ApplySelectionMode(TreeDataGridSelectionMode mode) { }
        protected void RaiseNativeSelectionChanged(Core.Selection.TreeSelectionModelSelectionChangedEventArgs e) =>
            NativeSelectionChanged?.Invoke(this, new TreeDataGridSelectionChangedEventArgs(
                Adapters.CoreConversions.ToAvalonia(e.DeselectedIndexes),
                Adapters.CoreConversions.ToAvalonia(e.SelectedIndexes), e.DeselectedItems, e.SelectedItems));
        protected void RaiseNativeCellSelectionChanged(IEnumerable<Core.CellIndex> deselected, IEnumerable<Core.CellIndex> selected) =>
            NativeSelectionChanged?.Invoke(this, new TreeDataGridSelectionChangedEventArgs(
                deselectedCellIndexes: deselected.Select(x => new CellIndex(x.ColumnIndex, Adapters.CoreConversions.ToAvalonia(x.RowIndex))).ToArray(),
                selectedCellIndexes: selected.Select(x => new CellIndex(x.ColumnIndex, Adapters.CoreConversions.ToAvalonia(x.RowIndex))).ToArray()));
        public static TreeDataGridPresentation Create(Core.ITreeDataGridSource model, ITreeDataGridPresentationOptions? options = null) =>
            options?.Create(model) ?? model.Accept(Factory.Instance);
        private sealed class Factory : Core.ITreeDataGridSourceVisitor<TreeDataGridPresentation>
        {
            public static Factory Instance { get; } = new();
            public TreeDataGridPresentation Visit<T>(Core.ITreeDataGridSource<T> model) where T : class => new TreeDataGridPresentation<T>(model);
        }
    }
}
