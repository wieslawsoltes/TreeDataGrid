using System.Collections.Generic;
using System.Collections.Specialized;
namespace TreeDataGridCore.Models
{
    public interface IRows : IReadOnlyList<IRow>, INotifyCollectionChanged
    {
        int ModelIndexToRowIndex(IndexPath modelIndex);
        IndexPath RowIndexToModelIndex(int rowIndex);
    }
}
