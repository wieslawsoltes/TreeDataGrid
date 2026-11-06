using System;
using Uno.Controls.Presentation;
using CoreModels = TreeDataGridCore.Models;

namespace Uno.Controls.Models.TreeDataGrid;

/// <summary>
/// Lets native Uno columns be added to shared Core sources in the same way as Avalonia
/// columns are added to Avalonia sources, including collection initializers:
/// <c>new FlatTreeDataGridSource&lt;T&gt;(items) { Columns = { new TextColumn&lt;T, string&gt;(...) } }</c>.
/// </summary>
public static class ColumnListExtensions
{
    /// <summary>Adds the Core definition of <paramref name="column"/> and registers its native presentation.</summary>
    public static void Add<TModel>(this CoreModels.ColumnList<TModel> columns, ICellColumn<TModel> column)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(columns);
        columns.Add(ToCoreColumn(column));
    }

    /// <summary>Inserts the Core definition of <paramref name="column"/> and registers its native presentation.</summary>
    public static void Insert<TModel>(this CoreModels.ColumnList<TModel> columns, int index, ICellColumn<TModel> column)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(columns);
        columns.Insert(index, ToCoreColumn(column));
    }

    internal static CoreModels.IColumn<TModel> ToCoreColumn<TModel>(ICellColumn<TModel> column)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(column);
        if (column is not CellColumn cell || cell.Model is not CoreModels.IColumn<TModel> definition)
            throw new ArgumentException("The column does not expose a shared Core column definition.", nameof(column));
        return ColumnPresentationRegistry.Register(definition, cell.CreatePresentationCopy);
    }
}
