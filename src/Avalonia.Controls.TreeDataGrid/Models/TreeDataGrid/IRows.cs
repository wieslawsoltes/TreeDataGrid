using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Avalonia.Controls.Models.TreeDataGrid
{
    /// <summary>
    /// Represents a collection of rows in an <see cref="ITreeDataGridSource"/>.
    /// </summary>
    /// <remarks>
    /// Note that items retrieved from an <see cref="IRows"/> collection may be reused, so the
    /// <see cref="IRow"/> should be treated as valid only until the next item is retrieved from
    /// the collection.
    /// </remarks>
    public interface IRows : IReadOnlyList<IRow>, ITreeDataGridRows
    {
        new IRow this[int index] => ((IReadOnlyList<IRow>)this)[index];
        new int Count => ((IReadOnlyCollection<IRow>)this).Count;
        new IEnumerator<IRow> GetEnumerator() => ((IEnumerable<IRow>)this).GetEnumerator();
        global::TreeDataGridCore.Models.IRow IReadOnlyList<global::TreeDataGridCore.Models.IRow>.this[int index] => this[index];
        int IReadOnlyCollection<global::TreeDataGridCore.Models.IRow>.Count => Count;
        IEnumerator<global::TreeDataGridCore.Models.IRow> IEnumerable<global::TreeDataGridCore.Models.IRow>.GetEnumerator() => GetEnumerator();
    }

    internal interface IReusableCellRows
    {
        bool TryReuseCell(IColumn column, ICell cell, int rowIndex);
    }

    internal interface IReusableCellColumn<TModel>
    {
        bool TryReuseCell(ICell cell, IRow<TModel> row);
    }
}
