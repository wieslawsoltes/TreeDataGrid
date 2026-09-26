// Adapted from Avalonia 12.0.0 (MIT). Copyright (c) AvaloniaUI OÜ. All Rights Reserved.
// See THIRD-PARTY-NOTICES.md, build/uno-parity-inputs/upstream and docs/uno-contract-materialization.json.
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
