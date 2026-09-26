// Adapted from Avalonia 12.0.0 (MIT). Copyright (c) AvaloniaUI OÜ. All Rights Reserved.
// See THIRD-PARTY-NOTICES.md, build/uno-parity-inputs/upstream and docs/uno-contract-materialization.json.
using TreeDataGridCore.Selection;
using System.Collections.Generic;
using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Selection
{
    public interface ITreeDataGridColumnSelectionModel : ISelectionModel
    {
        new IReadOnlyList<IColumn?> SelectedItems { get; }
        new IColumn? SelectedItem { get; set; }
    }
}
