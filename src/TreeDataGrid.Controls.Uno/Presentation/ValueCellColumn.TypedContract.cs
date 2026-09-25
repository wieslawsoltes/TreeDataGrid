using System;
using System.ComponentModel;
using TreeDataGridCore.Models;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Presentation;

public partial class ValueCellColumn<TModel, TValue> : UI.IColumn<TModel>
{
    /// <summary>Creates a native value for the supplied shared Core row.</summary>
    /// <remarks>The untyped virtual factory remains the customization point.</remarks>
    public virtual CellValue CreateCell(IRow<TModel> row) => CreateCell((IRow)row);

    UI.ICell UI.IColumn<TModel>.CreateCell(IRow<TModel> row) => CreateCell(row);
    Comparison<TModel?>? UI.IColumn<TModel>.GetComparison(ListSortDirection direction) => GetComparison(direction);
}
