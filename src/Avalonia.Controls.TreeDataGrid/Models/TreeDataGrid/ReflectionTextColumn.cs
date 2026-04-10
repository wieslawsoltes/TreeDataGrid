using System;
using System.ComponentModel;
using Avalonia.Experimental.Data;

namespace Avalonia.Controls.Models.TreeDataGrid
{
    internal class ReflectionTextColumn : ColumnBase<object, object?>, ITextSearchableColumn<object>
    {
        private readonly Func<object, object?> _getter;
        private readonly bool _isTextSearchEnabled;

        public ReflectionTextColumn(
            object? header,
            Func<object, object?> getter,
            Action<object, object?>? setter,
            Func<object, object>[] links,
            GridLength width,
            TextColumnOptions<object> options)
            : base(header, getter, CreateBinding(getter, setter, links), width, options)
        {
            _getter = getter;
            _isTextSearchEnabled = options.IsTextSearchEnabled;
        }

        bool ITextSearchableColumn<object>.IsTextSearchEnabled => _isTextSearchEnabled;

        public new TextColumnOptions<object> Options => (TextColumnOptions<object>)base.Options;

        public override ICell CreateCell(IRow<object> row)
        {
            return new TextCell<object?>(CreateBindingExpression(row.Model), Binding.Write is null, Options);
        }

        string? ITextSearchableColumn<object>.SelectValue(object model)
        {
            return _getter(model)?.ToString();
        }

        private static TypedBinding<object, object?> CreateBinding(
            Func<object, object?> getter,
            Action<object, object?>? setter,
            Func<object, object>[] links)
        {
            return new TypedBinding<object, object?>
            {
                Read = getter,
                Write = setter,
                Links = links,
            };
        }
    }
}
