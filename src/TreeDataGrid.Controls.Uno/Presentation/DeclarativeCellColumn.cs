using System;
using Microsoft.UI.Xaml.Data;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

using UI = global::Uno.Controls.Models.TreeDataGrid;

internal sealed class DeclarativeCellColumn<TModel> : ValueCellColumn<TModel, object?> where TModel : class
{
    private readonly Binding _binding;
    private readonly bool _readOnly;
    private readonly bool _threeState;
    internal DeclarativeCellColumn(ValueColumn<TModel, object?> column, Binding binding, bool readOnly,
        UI.ColumnOptions<TModel> options, TextCellOptions? text = null, bool checkBox = false, bool threeState = false)
        : base(column, checkBox ? CellKind.CheckBox : CellKind.Text, text, options)
    {
        _binding = binding;
        _readOnly = readOnly;
        _threeState = threeState;
        BeginEditGestures = options.BeginEditGestures;
    }
    public override bool IsThreeState => _threeState;
    public override CellValue CreateCell(IRow row) => new NativeCell(_binding, row.Model!, _readOnly, TextOptions)
        { EditGestures = Kind == CellKind.CheckBox ? UI.BeginEditGestures.None : EditGestures, Kind = Kind };

    private sealed class NativeCell : CellValue, UI.ITextCell
    {
        private readonly NativeColumnBinding _binding;
        private readonly bool _readOnly;
        private readonly TextCellOptions? _options;
        internal NativeCell(Binding binding, object model, bool readOnly, TextCellOptions? options)
        {
            _readOnly = readOnly;
            _options = options;
            _binding = new(binding, Changed, options?.Culture);
            _binding.Retarget(model);
        }
        public override object? Value => _binding.Value;
        public override bool CanEdit => Kind != CellKind.CheckBox && CanWrite;
        public override bool CanWrite => !_readOnly && _binding.CanWrite;
        public override Exception? Error => _binding.Error;
        public string? Text
        {
            get => _options is { } options ? string.Format(options.Culture, options.StringFormat, Value) : Value?.ToString();
            set => Write(value);
        }
        public Microsoft.UI.Xaml.TextAlignment TextAlignment => _options?.TextAlignment ?? Microsoft.UI.Xaml.TextAlignment.Left;
        public Microsoft.UI.Xaml.TextWrapping TextWrapping => _options?.TextWrapping ?? Microsoft.UI.Xaml.TextWrapping.NoWrap;
        public Microsoft.UI.Xaml.TextTrimming TextTrimming => _options?.TextTrimming ?? Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;
        public override void Write(object? value)
        {
            if (!CanWrite) throw new InvalidOperationException("The column is read-only.");
            _binding.Write(value);
        }
        internal override bool TryRetarget(IRow row) { _binding.Retarget(row.Model!); return true; }
        internal override bool TrySuspend() { _binding.Suspend(); return true; }
        public override void Dispose() => _binding.Dispose();
        private void Changed() { RaisePropertyChanged(nameof(Value)); RaisePropertyChanged(nameof(Error)); }
    }
}
