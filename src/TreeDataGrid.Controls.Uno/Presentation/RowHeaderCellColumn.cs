using System;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Presentation;

internal sealed class RowHeaderCellColumn<TModel> : ValueCellColumn<TModel, string> where TModel : class
{
    internal RowHeaderCellColumn(ValueColumn<TModel, string> column, UI.ColumnOptions<TModel> options)
        : base(column, CellKind.Text, viewOptions: options) => BeginEditGestures = options.BeginEditGestures;

    public override CellValue CreateCell(IRow row) => new RowHeaderCell(row) { EditGestures = EditGestures };

    private sealed class RowHeaderCell : CellValue, UI.ITextCell
    {
        private string _text = string.Empty;
        internal RowHeaderCell(IRow row) => TryRetarget(row);
        public override object? Value => Text;
        public override bool CanEdit => false;
        public string? Text
        {
            get => _text;
            set => throw new InvalidOperationException("Row headers are read-only.");
        }
        public TextTrimming TextTrimming => TextTrimming.CharacterEllipsis;
        public TextWrapping TextWrapping => TextWrapping.NoWrap;
        public TextAlignment TextAlignment => TextAlignment.Left;
        public override void Write(object? value) => throw new InvalidOperationException("Row headers are read-only.");
        internal override bool TryRetarget(IRow row)
        {
            // Flat Core sources reuse an AnonymousRow. Capture its index now;
            // retaining that row would make every header display the last index.
            _text = row is IModelIndexableRow indexed ? (indexed.ModelIndexPath[^1] + 1).ToString() : string.Empty;
            return true;
        }
        internal override bool TrySuspend() { _text = string.Empty; return true; }
        public override void Dispose() => _text = string.Empty;
    }
}
