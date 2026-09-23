// Adapted from Avalonia 12.0.0 (MIT). Copyright (c) .NET Foundation and Contributors.
// See build/uno-parity-inputs/upstream and docs/uno-contract-materialization.json.
using TreeDataGridCore.Selection;
using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Selection
{
    public class TreeDataGridColumnSelectionModel : SelectionModel<IColumn>,
        ITreeDataGridColumnSelectionModel
    {
        public TreeDataGridColumnSelectionModel(IColumns columns)
            : base(columns)
        {
        }
    }
}
