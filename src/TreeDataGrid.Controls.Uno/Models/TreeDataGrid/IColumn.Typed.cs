using System;
using System.ComponentModel;
using TreeDataGridCore.Models;

namespace Uno.Controls.Models.TreeDataGrid;

/// <summary>A typed native column whose row identity is owned by the shared Core.</summary>
/// <typeparam name="TModel">The shared Core row model type.</typeparam>
/// <remarks>
/// Cell creation and comparison share one public view contract. An attached Core
/// source remains responsible for ordering its rows; this interface introduces
/// neither another source-column definition nor a second selection controller.
/// </remarks>
public interface IColumn<TModel> : IColumn
{
    /// <summary>Creates a cell over the supplied shared Core row.</summary>
    ICell CreateCell(IRow<TModel> row);

    /// <summary>Gets this view's comparison, or null when it does not define one.</summary>
    Comparison<TModel?>? GetComparison(ListSortDirection direction);
}
