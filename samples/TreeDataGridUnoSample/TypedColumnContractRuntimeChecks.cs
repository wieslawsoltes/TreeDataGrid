using System;
using System.Collections.Generic;
using System.ComponentModel;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGridUnoSample;

/// <summary>Exercises typed interface dispatch inside actual native/trimmed consumers.</summary>
internal static class TypedColumnContractRuntimeChecks
{
    internal static void Run<TModel, TValue>(ValueCellColumn<TModel, TValue> column, IRow<TModel> row) where TModel : class
    {
        U.IColumn<TModel> typed = column;
        ICellColumn<TModel> legacy = column;
        foreach (var direction in new[] { ListSortDirection.Ascending, ListSortDirection.Descending, (ListSortDirection)42 })
        {
            Check(ReferenceEquals(typed.GetComparison(direction), column.GetComparison(direction)),
                "The typed column interface lost the concrete comparison policy.");
            Check(ReferenceEquals(legacy.GetComparison(direction), typed.GetComparison(direction)),
                "The legacy factory contract selected a different comparison implementation.");
        }
        var columns = new U.ColumnList<TModel> { column };
        IReadOnlyList<U.IColumn<TModel>> projection = columns;
        Check(ReferenceEquals(projection[0], column), "Typed column covariance changed column identity.");
        var model = row.Model;
        var first = typed.CreateCell(row);
        U.ICell? second = null;
        try
        {
            second = legacy.CreateCell(row);
            Check(first is CellValue && second is CellValue && !ReferenceEquals(first, second),
                "Typed factories did not return independently owned native cell values.");
            Check(Equals(first.Value, second.Value) && first.CanEdit == second.CanEdit && ReferenceEquals(row.Model, model),
                "Typed creation lost the binding contract or changed shared Core row identity.");
        }
        finally
        {
            try { (first as IDisposable)?.Dispose(); }
            finally { (second as IDisposable)?.Dispose(); }
        }
        columns.Clear();
        Check(ReferenceEquals(typed.GetComparison(ListSortDirection.Ascending), column.GetComparison(ListSortDirection.Ascending)),
            "Removing a typed column retired the caller's column.");
        Console.WriteLine("UNO_RUNTIME_TYPED_COLUMN_CONTRACT_PASSED: built-in and legacy interface dispatch, comparison identity, Core row identity, native cell factories, typed-list covariance and independent cleanup");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
