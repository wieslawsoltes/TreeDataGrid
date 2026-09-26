using System;
using System.ComponentModel;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

using UI = global::Uno.Controls.Models.TreeDataGrid;

public partial class ValueCellColumn<TModel, TValue> : UI.IColumn<TModel>
{
    /// <summary>Creates a native value for the supplied shared Core row.</summary>
    /// <remarks>The untyped virtual factory remains the customization point.</remarks>
    public virtual CellValue CreateCell(IRow<TModel> row) => CreateCell((IRow)row);

    // The legacy slot may itself have been explicitly reimplemented by a custom
    // subclass. Both interface routes must use that same implementation.
    UI.ICell UI.IColumn<TModel>.CreateCell(IRow<TModel> row) =>
        ((ICellColumn<TModel>)this).CreateCell(row);
    Comparison<TModel?>? UI.IColumn<TModel>.GetComparison(ListSortDirection direction) => GetComparison(direction);
}
