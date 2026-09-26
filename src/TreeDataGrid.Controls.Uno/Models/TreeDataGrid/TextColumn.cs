using System;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.UI.Xaml;
using Uno.Controls.Presentation;

namespace Uno.Controls.Models.TreeDataGrid;

public class TextColumn<TModel, TValue> : ValueCellColumn<TModel, TValue?>, ITextSearchableColumn<TModel> where TModel : class
{
    private readonly TreeDataGridCore.Models.ValueColumn<TModel, TValue?> _definition;
    private TextCellOptions? _snapshot;

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
        : base(column, CellKind.Text, null, options)
    {
        _definition = column;
        Options = options;
        Header = column.Header;
        BeginEditGestures = Options.BeginEditGestures;
    }
    public TextColumnOptions<TModel> Options { get; }

    public override TextCellOptions TextOptions
    {
        get
        {
            var options = Options;
            var previous = _snapshot;
            // Keep one immutable rendering snapshot per configuration, not per
            // cell or query. Culture identity is compared without invoking an
            // application-defined Equals/GetFormat implementation.
            if (previous is null || previous.TextAlignment != options.TextAlignment ||
                previous.TextWrapping != options.TextWrapping || previous.TextTrimming != options.TextTrimming ||
                previous.StringFormat != options.StringFormat || !ReferenceEquals(previous.Culture, options.Culture) ||
                previous.IsTextSearchEnabled != options.IsTextSearchEnabled)
                _snapshot = previous = options.Snapshot();
            return previous;
        }
    }
    public override bool IsTextSearchEnabled => TextSearchValueSelector is not null || Options.IsTextSearchEnabled;
    public override string? GetSearchText(object? model) => TextSearchValueSelector is { } select
        ? select(model) : model is TModel typed ? _definition.GetValue(typed)?.ToString() : null;
    public override string FormatValue(object? value) => Options.StringFormat is { } format
        ? CellTextFormatting.Format(Options.Culture, format, value) : value?.ToString() ?? string.Empty;
    public override CellValue CreateCell(TreeDataGridCore.Models.IRow row) => new LiveTextCell(this, row)
    {
        Kind = CellKind.Text,
    };
    // Match the reference's raw selector contract. Display prefixes, alignment,
    // numeric formats and display-culture callbacks do not participate in search.
    string? ITextSearchableColumn<TModel>.SelectValue(TModel model) => GetSearchText(model);

    private sealed class LiveTextCell(TextColumn<TModel, TValue> column, TreeDataGridCore.Models.IRow row)
        : BoundCell<TModel, TValue?>(column._definition, row, canPool: true,
            bindingSnapshot: column.CaptureBindingSnapshot()), ITextCell
    {
        public override TextCellOptions TextOptions => column.TextOptions;
        protected override CultureInfo? ConversionCulture => column.Options.Culture;
        public override BeginEditGestures EditGestures
        {
            get => column.Options.BeginEditGestures;
            internal set => base.EditGestures = value;
        }
        public string? Text
        {
            get => column.Options.StringFormat is { } format
                ? CellTextFormatting.Format(column.Options.Culture, format, Value) : Value?.ToString();
            set => Write(value);
        }
        public TextTrimming TextTrimming => column.Options.TextTrimming;
        public TextWrapping TextWrapping => column.Options.TextWrapping;
        public TextAlignment TextAlignment => column.Options.TextAlignment;
    }
}
