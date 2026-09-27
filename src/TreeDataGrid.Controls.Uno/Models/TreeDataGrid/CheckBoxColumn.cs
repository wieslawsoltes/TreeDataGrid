using System;
using System.Linq.Expressions;
using Microsoft.UI.Xaml;
using Uno.Controls.Presentation;

namespace Uno.Controls.Models.TreeDataGrid;

public class CheckBoxColumn<TModel> : ValueCellColumn<TModel, bool?> where TModel : class
{
    public CheckBoxColumn(object? header, Expression<Func<TModel, bool>> getter, Action<TModel, bool>? setter = null,
        GridLength? width = null, CheckBoxColumnOptions<TModel>? options = null)
        : this(new TreeDataGridCore.Models.CheckBoxColumn<TModel>(header, getter, setter, ColumnOptions<TModel>.ToCore(width), options ?? new()), options) { }
    public CheckBoxColumn(object? header, Expression<Func<TModel, bool?>> getter, Action<TModel, bool?>? setter = null,
        GridLength? width = null, CheckBoxColumnOptions<TModel>? options = null)
        : this(new TreeDataGridCore.Models.CheckBoxColumn<TModel>(header, getter, setter, ColumnOptions<TModel>.ToCore(width), options ?? new()), options) { }
    public CheckBoxColumn(TreeDataGridCore.Models.CheckBoxColumn<TModel> column, CheckBoxColumnOptions<TModel>? options = null)
        : this(column, options ?? ColumnOptions<TModel>.CopyCore(column.Options, new CheckBoxColumnOptions<TModel>()), true) { }
    private CheckBoxColumn(TreeDataGridCore.Models.CheckBoxColumn<TModel> column, CheckBoxColumnOptions<TModel> options, bool _)
        : base(column, CellKind.CheckBox, viewOptions: options)
    {
        Options = options;
        Header = column.Header;
        BeginEditGestures = Options.BeginEditGestures;
    }
    public CheckBoxColumnOptions<TModel> Options { get; }

    /// <summary>Creates a native cell over the caller's actual shared Core row.</summary>
    /// <remarks>Preserves the existing typed virtual slot and disposable CellValue result.</remarks>
    public override CellValue CreateCell(TreeDataGridCore.Models.IRow<TModel> row) => base.CreateCell(row);

    /// <summary>Retargets a compatible native cell using the existing binding owner.</summary>
    public new bool TryReuseCell(ICell cell, TreeDataGridCore.Models.IRow<TModel> row) =>
        base.TryReuseCell(cell, row);

    /// <summary>Gets whether this column represents nullable Boolean values.</summary>
    public new bool IsThreeState => base.IsThreeState;
}
