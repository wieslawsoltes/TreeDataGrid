using System;
using System.Runtime.CompilerServices;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

/// <summary>
/// Associates Uno configuration with fluent/declarative Core definitions without
/// placing templates in Core, using Tag, or retaining discarded sources globally.
/// Every presentation receives its own column, measurement state and template cache.
/// </summary>
internal static class ColumnPresentationRegistry
{
    private sealed record Registration(string? Key, Func<CellColumn> Create);
    private static readonly ConditionalWeakTable<IColumn, Registration> s_columns = new();

    internal static TColumn Register<TColumn>(TColumn column, Func<CellColumn> create) where TColumn : class, IColumn
    {
        s_columns.Add(column, new(column.PresentationKey, create));
        return column;
    }

    internal static bool TryCreate(IColumn column, out CellColumn result)
    {
        if (s_columns.TryGetValue(column, out var registration) && registration.Key == column.PresentationKey)
        {
            result = registration.Create();
            result.AttachModel(column);
            return true;
        }
        result = null!;
        return false;
    }
}
