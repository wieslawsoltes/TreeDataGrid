using System;
using System.Collections.Generic;
#if TREE_DATAGRID_UNO
using Uno.Controls.Selection;
#else
using Avalonia.Controls.Selection;
#endif
#if TREE_DATAGRID_UNO

namespace Uno.Controls

#else

namespace Avalonia.Controls

#endif
{
    public class TreeDataGridSelectionChangedEventArgs : TreeSelectionModelSelectionChangedEventArgs
    {
        public TreeDataGridSelectionChangedEventArgs(
            IReadOnlyList<IndexPath>? deselectedIndexes = null,
            IReadOnlyList<IndexPath>? selectedIndexes = null,
            IReadOnlyList<object?>? deselectedItems = null,
            IReadOnlyList<object?>? selectedItems = null,
            IReadOnlyList<CellIndex>? deselectedCellIndexes = null,
            IReadOnlyList<CellIndex>? selectedCellIndexes = null)
        {
            DeselectedIndexes = deselectedIndexes ?? Array.Empty<IndexPath>();
            SelectedIndexes = selectedIndexes ?? Array.Empty<IndexPath>();
            DeselectedItems = deselectedItems ?? Array.Empty<object?>();
            SelectedItems = selectedItems ?? Array.Empty<object?>();
            DeselectedCellIndexes = deselectedCellIndexes ?? Array.Empty<CellIndex>();
            SelectedCellIndexes = selectedCellIndexes ?? Array.Empty<CellIndex>();
        }

        public override IReadOnlyList<IndexPath> DeselectedIndexes { get; }
        public override IReadOnlyList<IndexPath> SelectedIndexes { get; }
        public new IReadOnlyList<object?> DeselectedItems { get; }
        public new IReadOnlyList<object?> SelectedItems { get; }
        public IReadOnlyList<CellIndex> DeselectedCellIndexes { get; }
        public IReadOnlyList<CellIndex> SelectedCellIndexes { get; }

        protected override IReadOnlyList<object?> GetUntypedDeselectedItems() => DeselectedItems;
        protected override IReadOnlyList<object?> GetUntypedSelectedItems() => SelectedItems;
    }

    public class TreeDataGridSelectionChangedEventArgs<TModel> : TreeSelectionModelSelectionChangedEventArgs<TModel>
    {
        public TreeDataGridSelectionChangedEventArgs(
            IReadOnlyList<IndexPath>? deselectedIndexes = null,
            IReadOnlyList<IndexPath>? selectedIndexes = null,
            IReadOnlyList<TModel?>? deselectedItems = null,
            IReadOnlyList<TModel?>? selectedItems = null,
            IReadOnlyList<CellIndex>? deselectedCellIndexes = null,
            IReadOnlyList<CellIndex>? selectedCellIndexes = null)
            : base(
                deselectedIndexes,
                selectedIndexes,
                deselectedItems,
                selectedItems)
        {
            DeselectedCellIndexes = deselectedCellIndexes ?? Array.Empty<CellIndex>();
            SelectedCellIndexes = selectedCellIndexes ?? Array.Empty<CellIndex>();
        }

        public IReadOnlyList<CellIndex> DeselectedCellIndexes { get; }
        public IReadOnlyList<CellIndex> SelectedCellIndexes { get; }
    }
}
