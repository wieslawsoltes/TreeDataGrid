using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Presentation;
using Avalonia.Controls.Selection;
using Avalonia.Input;
namespace Avalonia.Controls.Adapters
{
    // Compatibility goes from the old source API into the control's presentation contract.
    internal sealed class LegacySourcePresentation : TreeDataGridPresentation
    {
        public LegacySourcePresentation(ITreeDataGridSource source) => Source = source;
        private ITreeDataGridRowSelectionModel? RowSelection => Source.Selection as ITreeDataGridRowSelectionModel;
        public override object SourceIdentity => Source;
        public override bool CanSelectMultiple => RowSelection?.SingleSelect == false;
        public override void Select(IndexPath index, bool replace)
        { if (RowSelection is not { } selection) return; if (replace) selection.SelectedIndex = index; else selection.Select(index); }
        public override void Deselect(IndexPath index) => RowSelection?.Deselect(index);
        public override bool IsSelected(IndexPath index) => RowSelection?.IsSelected(index) ?? false;
        public ITreeDataGridSource Source { get; }
        public override IColumns Columns => Source.Columns;
        public override ITreeDataGridRows Rows => Source.Rows;
        public override bool IsHierarchical => Source.IsHierarchical;
        public override bool IsSorted => Source.IsSorted;
        public override ITreeDataGridSelectionInteraction? SelectionInteraction => Source.Selection as ITreeDataGridSelectionInteraction;
        public override IReadOnlyList<object?>? SelectedItems => (Source.Selection as ITreeDataGridRowSelectionModel)?.SelectedItems;
        public override IReadOnlyList<IndexPath>? SelectedIndexes => (Source.Selection as ITreeDataGridRowSelectionModel)?.SelectedIndexes;
        public override event PropertyChangedEventHandler? PropertyChanged { add => Source.PropertyChanged += value; remove => Source.PropertyChanged -= value; }
        public override event Action? Sorted { add => Source.Sorted += value; remove => Source.Sorted -= value; }
        public override bool SortBy(IColumn column, ListSortDirection direction) => Source.SortBy(column, direction);
        public override void MoveRows(IEnumerable<IndexPath> indexes, IndexPath target, TreeDataGridRowDropPosition position, DragDropEffects effects) => Source.DragDropRows(Source, indexes, target, position, effects);
        public override void Dispose() { }
    }
}
