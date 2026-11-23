using System;
using System.ComponentModel;
using TreeDataGridCore.Models;
using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Presentation;

/// <summary>A native cell factory with layout and optional retained-cell reuse.</summary>
/// <remarks>
/// This legacy interface deliberately does not derive from IColumn&lt;TModel&gt;.
/// Adding that base would reimplement its factory slot in existing subclasses
/// which explicitly implement ICellColumn, selecting their public method instead
/// of their explicit factory. Library bases implement both contracts separately.
/// </remarks>
public interface ICellColumn<TModel> : IUpdateColumnLayout
{
    ICell CreateCell(IRow<TModel> row);

    /// <summary>Uses the typed view's comparison when available; otherwise null.</summary>
    Comparison<TModel?>? GetComparison(ListSortDirection direction) =>
        this is global::Uno.Controls.Models.TreeDataGrid.IColumn<TModel> typed
            ? typed.GetComparison(direction) : null;

    bool TryReuseCell(ICell cell, IRow<TModel> row) => false;
}
