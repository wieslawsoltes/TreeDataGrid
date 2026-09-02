using Avalonia.Controls.Models.TreeDataGrid;
using Core = global::TreeDataGridCore;
namespace Avalonia.Controls.Presentation
{
    /// <summary>Creates view cells directly from Core rows, without a legacy row wrapper.</summary>
    public interface ICellColumn<TModel> : IColumn, IUpdateColumnLayout
    {
        ICell CreateCell(Core.Models.IRow<TModel> row);
        bool TryReuseCell(ICell cell, Core.Models.IRow<TModel> row) => false;
    }
}
