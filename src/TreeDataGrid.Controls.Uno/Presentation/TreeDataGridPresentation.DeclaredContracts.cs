using System;
using System.Collections.Generic;
using System.ComponentModel;
using Uno.Controls.Selection;
using IColumn = Uno.Controls.Models.TreeDataGrid.IColumn;

namespace Uno.Controls.Presentation;

public sealed partial class TreeDataGridPresentation<TModel> where TModel : class
{
    // Restore the generic presenter's portable declaration owners, without a
    // second source, selection projection, event store or subscription lifetime.
    public override object SourceIdentity => base.SourceIdentity;
    public override bool IsHierarchical => base.IsHierarchical;
    public override bool IsSorted => base.IsSorted;
    public override bool CanSelectMultiple => base.CanSelectMultiple;
    public override IReadOnlyList<object?>? SelectedItems => base.SelectedItems;
    public override ITreeDataGridSelectionInteraction? SelectionInteraction => base.SelectionInteraction;

    public override event PropertyChangedEventHandler? PropertyChanged
    {
        add => base.PropertyChanged += value;
        remove => base.PropertyChanged -= value;
    }

    public override event Action? Sorted
    {
        add => base.Sorted += value;
        remove => base.Sorted -= value;
    }

    public override bool SortBy(IColumn column, ListSortDirection direction) => base.SortBy(column, direction);
}
