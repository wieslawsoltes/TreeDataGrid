using System;
using System.Linq.Expressions;
using Microsoft.UI.Xaml;
using Uno.Controls.Presentation;

namespace Uno.Controls.Models.TreeDataGrid;

public class TextColumn<TModel, TValue> : ValueCellColumn<TModel, TValue?> where TModel : class
{
    public TextColumn(object? header, Expression<Func<TModel, TValue?>> getter,
        GridLength? width = null, TextColumnOptions<TModel>? options = null)
        : this(header, getter, null, width, options ?? new(), true) { }
    public TextColumn(object? header, Expression<Func<TModel, TValue?>> getter, Action<TModel, TValue?> setter,
        GridLength? width = null, TextColumnOptions<TModel>? options = null)
        : this(header, getter, setter, width, options ?? new(), true) { }
    private TextColumn(object? header, Expression<Func<TModel, TValue?>> getter, Action<TModel, TValue?>? setter,
        GridLength? width, TextColumnOptions<TModel> options, bool _)
        : this(new TreeDataGridCore.Models.ValueColumn<TModel, TValue?>(header, getter, setter, ColumnOptions<TModel>.ToCore(width), options), options) { }
    public TextColumn(TreeDataGridCore.Models.ValueColumn<TModel, TValue?> column, TextColumnOptions<TModel>? options = null)
        : this(column, options ?? ColumnOptions<TModel>.CopyCore(column.Options, new TextColumnOptions<TModel>()), true) { }
    private TextColumn(TreeDataGridCore.Models.ValueColumn<TModel, TValue?> column, TextColumnOptions<TModel> options, bool _)
        : base(column, CellKind.Text, options.Snapshot(), options)
    {
        Options = options;
        Header = column.Header;
        BeginEditGestures = Options.BeginEditGestures;
    }
    public TextColumnOptions<TModel> Options { get; }
}
