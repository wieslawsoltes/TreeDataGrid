using TreeDataGridCore.Models;
using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Presentation;

public interface ICellColumn<TModel> : IUpdateColumnLayout
{
    ICell CreateCell(IRow<TModel> row);
    bool TryReuseCell(ICell cell, IRow<TModel> row) => false;
}
