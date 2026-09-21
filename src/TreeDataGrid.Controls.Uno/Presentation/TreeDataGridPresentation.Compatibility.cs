using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Selection;
using Uno.Controls.Selection;
using Windows.ApplicationModel.DataTransfer;
using IColumn = Uno.Controls.Models.TreeDataGrid.IColumn;

namespace Uno.Controls.Presentation;

public abstract partial class TreeDataGridPresentation
{
    // Preserve the reference presentation surface while forwarding directly to
    // the shared Core. No additional selection/source observation is required.
    public virtual object SourceIdentity => Model;
    public virtual bool IsHierarchical => Model.IsHierarchical;
    public virtual bool IsSorted => Model.IsSorted;
    public virtual bool CanSelectMultiple => Model.Selection is ITreeDataGridRowSelectionModel { SingleSelect: false };
    public virtual IReadOnlyList<object?>? SelectedItems => (Model.Selection as ITreeDataGridRowSelectionModel)?.SelectedItems;
    public virtual IReadOnlyList<IndexPath>? SelectedIndexes => (Model.Selection as ITreeDataGridRowSelectionModel)?.SelectedIndexes;
    public virtual ITreeDataGridSelectionInteraction? SelectionInteraction =>
        _viewRowsActive && !_viewRowsDisposed && Selection.Model is not null ? Selection : null;
    public virtual event PropertyChangedEventHandler? PropertyChanged;
    public virtual event Action? Sorted;

    public virtual void Select(IndexPath index, bool replace)
    {
        if (Model.Selection is not ITreeDataGridRowSelectionModel rows) return;
        if (replace) rows.SelectedIndex = index;
        else rows.Select(index);
    }
    public virtual void Deselect(IndexPath index) => (Model.Selection as ITreeDataGridRowSelectionModel)?.Deselect(index);
    public virtual bool IsSelected(IndexPath index) => (Model.Selection as ITreeDataGridRowSelectionModel)?.IsSelected(index) ?? false;
    public virtual bool SortBy(IColumn column, ListSortDirection direction) =>
        Model.SortBy(column is CellColumn native && Model.Columns.Any(model => ReferenceEquals(model, native.Model)) ? native.Model : null, direction);

    /// <summary>Moves rows through the original Core source using native drag effects.</summary>
    public virtual void MoveRows(IEnumerable<IndexPath> indexes, IndexPath target, TreeDataGridRowDropPosition position, DataPackageOperation effects) =>
        Model.MoveRows(Model, indexes, target, (RowDropPosition)position, (RowMoveEffects)effects);

    protected void RaisePropertyChanged(PropertyChangedEventArgs args) => PropertyChanged?.Invoke(this, args);
    protected void RaiseSorted() => Sorted?.Invoke();
}
