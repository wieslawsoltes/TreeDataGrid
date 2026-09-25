using System;
using System.ComponentModel;
using TreeDataGridCore.Models;
using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Presentation;

/// <summary>A typed native column with layout and optional retained-cell reuse.</summary>
public interface ICellColumn<TModel> : Uno.Controls.Models.TreeDataGrid.IColumn<TModel>, IUpdateColumnLayout
{
    // Retain this declaration for existing explicit ICellColumn implementations.
    new ICell CreateCell(IRow<TModel> row);
    ICell Uno.Controls.Models.TreeDataGrid.IColumn<TModel>.CreateCell(IRow<TModel> row) => CreateCell(row);

    // Legacy view-only factories need not introduce a comparison merely to keep
    // working. Built-in and compatibility-base columns explicitly forward their
    // existing virtual comparison policy instead of using this default.
    Comparison<TModel?>? Uno.Controls.Models.TreeDataGrid.IColumn<TModel>.GetComparison(ListSortDirection direction) => null;

    bool TryReuseCell(ICell cell, IRow<TModel> row) => false;
}
