using System;
using System.Linq.Expressions;

namespace Avalonia.Controls.Models.TreeDataGrid
{
    /// <summary>
    /// A column in an <see cref="ITreeDataGridSource"/> which displays its values as text.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <typeparam name="TValue">The column data type.</typeparam>
    public class TextColumn<TModel, TValue> : ColumnBase<TModel, TValue>,
        ITextSearchableColumn<TModel>, IReusableCellColumn<TModel>, global::Avalonia.Controls.Presentation.ICellColumn<TModel>
        where TModel : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextColumn{TModel, TValue}"/> class.
        /// </summary>
        /// <param name="header">The column header.</param>
        /// <param name="getter">
        /// An expression which given a row model, returns a cell value for the column.
        /// </param>
        /// <param name="width">
        /// The column width. If null defaults to <see cref="GridLength.Auto"/>.
        /// </param>
        /// <param name="options">Additional column options.</param>
        public TextColumn(
            object? header,
            Expression<Func<TModel, TValue?>> getter,
            GridLength? width = null,
            TextColumnOptions<TModel>? options = null)
            : base(header, getter, null, width, options ?? new())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TextColumn{TModel, TValue}"/> class.
        /// </summary>
        /// <param name="header">The column header.</param>
        /// <param name="getter">
        /// An expression which given a row model, returns a cell value for the column.
        /// </param>
        /// <param name="setter">
        /// A method which given a row model and a cell value, writes the cell value to the
        /// row model.
        /// </param>
        /// <param name="width">
        /// The column width. If null defaults to <see cref="GridLength.Auto"/>.
        /// </param>
        /// <param name="options">Additional column options.</param>
        public TextColumn(
            object? header,
            Expression<Func<TModel, TValue?>> getter,
            Action<TModel, TValue?> setter,
            GridLength? width = null,
            TextColumnOptions<TModel>? options = null)
            : base(header, getter, setter, width, options ?? new())
        {
        }

        internal TextColumn(global::TreeDataGridCore.Models.ValueColumn<TModel, TValue?> column, TextColumnOptions<TModel> options)
            : base(column.Header, column.GetterExpression is null ? column.Getter : column.GetValue,
                global::Avalonia.Controls.Presentation.CellBinding.Create(column),
                new GridLength(column.Width.Value, (GridUnitType)column.Width.GridUnitType), options) { }

        public new TextColumnOptions<TModel> Options => (TextColumnOptions<TModel>)base.Options;

        bool ITextSearchableColumn<TModel>.IsTextSearchEnabled => Options?.IsTextSearchEnabled ?? false;

        public override ICell CreateCell(IRow<TModel> row)
        {
            return new TextCell<TValue?>(CreateBindingExpression(row.Model), Binding.Write is null, Options);
        }
        public ICell CreateCell(global::TreeDataGridCore.Models.IRow<TModel> row)
        {
            return new TextCell<TValue?>(CreateBindingExpression(row.Model), Binding.Write is null, Options);
        }
        public bool TryReuseCell(ICell cell, global::TreeDataGridCore.Models.IRow<TModel> row) => cell is TextCell<TValue?> typed && typed.TrySetSource(row.Model);


        string? ITextSearchableColumn<TModel>.SelectValue(TModel model)
        {
            return ValueSelector(model)?.ToString();
        }

        bool IReusableCellColumn<TModel>.TryReuseCell(ICell cell, IRow<TModel> row)
        {
            return cell is TextCell<TValue?> textCell && textCell.TrySetSource(row.Model);
        }
    }
}
