using Uno.Controls.Presentation;

namespace Uno.Controls.Models.TreeDataGrid;

/// <summary>A typed collection of native cell columns using the shared view-layout implementation.</summary>
/// <typeparam name="TModel">The model type exposed by the shared Core rows.</typeparam>
/// <remarks>
/// This is the view-side counterpart of Avalonia's ColumnList&lt;TModel&gt;.
/// ICellColumn&lt;TModel&gt; replaces its combined model/layout column interface:
/// sorting, hierarchy and source-column definitions remain in TreeDataGrid.Core.
/// The collection borrows its columns; removal does not dispose caller-owned columns.
/// It is directly usable wherever native presenters accept IColumns.
/// </remarks>
public class ColumnList<TModel> : ColumnListBase<ICellColumn<TModel>> { }
