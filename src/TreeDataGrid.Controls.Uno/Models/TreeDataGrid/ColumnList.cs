using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;

namespace Uno.Controls.Models.TreeDataGrid;

/// <summary>A typed collection of native cell columns using the shared view-layout implementation.</summary>
/// <typeparam name="TModel">The model type exposed by the shared Core rows.</typeparam>
/// <remarks>
/// The mutable list keeps the original ICellColumn factory/layout contract.
/// Its typed read-only projection exposes built-in/custom typed columns without
/// changing identity. A legacy-only factory is represented by a lazy, non-owning
/// view facade. Sorting, hierarchy and source definitions remain Core-owned.
/// Removing a column does not dispose it or transfer ownership to this list.
/// </remarks>
public class ColumnList<TModel> : ColumnListBase<ICellColumn<TModel>>, IColumns, IReadOnlyList<IColumn<TModel>>
{
    private ConditionalWeakTable<ICellColumn<TModel>, TypedColumnView>? _typedViews;

    // Provide an exact untyped interface map at the same inheritance level.
    // Otherwise variant dispatch can choose the new typed IReadOnlyList slot
    // instead of the inherited native slot, leaking a facade into native layout.
    IColumn IReadOnlyList<IColumn>.this[int index] => this[index];
    IEnumerator<IColumn> IEnumerable<IColumn>.GetEnumerator() => GetEnumerator();

    IColumn<TModel> IReadOnlyList<IColumn<TModel>>.this[int index] => GetTypedColumn(this[index]);

    IEnumerator<IColumn<TModel>> IEnumerable<IColumn<TModel>>.GetEnumerator()
    {
        // Retain the original list enumerator's mutation detection, rather than
        // accidentally turning typed enumeration into a live index-based walk.
        foreach (var column in (IEnumerable<ICellColumn<TModel>>)this)
            yield return GetTypedColumn(column);
    }

    private IColumn<TModel> GetTypedColumn(ICellColumn<TModel> column) =>
        column as IColumn<TModel> ?? (_typedViews ??= new()).GetValue(column, static value => new(value));

    // No event subscription or model copy is made by this facade. Ephemeron
    // ownership prevents the cache from retaining a removed legacy column.
    private sealed class TypedColumnView(ICellColumn<TModel> column) : IColumn<TModel>
    {
        public double ActualWidth => column.ActualWidth;
        public bool? CanUserResize => column.CanUserResize;
        public object? Header => column.Header;
        public GridLength Width => column.Width;
        public ListSortDirection? SortDirection { get => column.SortDirection; set => column.SortDirection = value; }
        public object? Tag { get => column.Tag; set => column.Tag = value; }
        public ICell CreateCell(IRow<TModel> row) => column.CreateCell(row);
        public Comparison<TModel?>? GetComparison(ListSortDirection direction) => column.GetComparison(direction);
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => column.PropertyChanged += value;
            remove => column.PropertyChanged -= value;
        }
    }
}
