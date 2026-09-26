using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using ITextCell = Uno.Controls.Models.TreeDataGrid.ITextCell;

namespace Uno.Controls.Primitives;

/// <summary>A text cell with the same scalar customization surface as Avalonia.</summary>
public partial class TreeDataGridTextCell : TreeDataGridCell
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(TreeDataGridTextCell), new PropertyMetadata(null, ValueChanged));
    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
        nameof(TextAlignment), typeof(TextAlignment), typeof(TreeDataGridTextCell), new PropertyMetadata(TextAlignment.Left, AppearanceChanged));
    public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
        nameof(TextWrapping), typeof(TextWrapping), typeof(TreeDataGridTextCell), new PropertyMetadata(TextWrapping.NoWrap, AppearanceChanged));
    public static readonly DependencyProperty TextTrimmingProperty = DependencyProperty.Register(
        nameof(TextTrimming), typeof(TextTrimming), typeof(TreeDataGridTextCell), new PropertyMetadata(TextTrimming.CharacterEllipsis, AppearanceChanged));
    private int _synchronizing;
    private int _valueRefreshVersion;
    private int _valueWriteVersion;
    public TreeDataGridTextCell() => DefaultStyleKey = typeof(TreeDataGridTextCell);
    public new string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public TextAlignment TextAlignment { get => (TextAlignment)GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }
    public TextWrapping TextWrapping { get => (TextWrapping)GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }
    public TextTrimming TextTrimming { get => (TextTrimming)GetValue(TextTrimmingProperty); set => SetValue(TextTrimmingProperty, value); }
    protected override string? DisplayText => Value;
    protected override TextAlignment DisplayTextAlignment => TextAlignment;
    protected override TextWrapping DisplayTextWrapping => TextWrapping;
    protected override TextTrimming DisplayTextTrimming => TextTrimming;
    public override bool BeginEdit()
    {
        // A compatible PART_Edit may bind Text back to Value. Initializing its
        // raw edit buffer must not bypass CellEditSession's transaction boundary.
        ++_synchronizing;
        try
        {
            var realization = RealizationVersion;
            var result = base.BeginEdit();
            if (result && IsEditing && UsesTextEditor) Value = EditingText;
            return result && IsEditing && RealizationVersion == realization;
        }
        finally { --_synchronizing; }
    }

    public override void Realize(CellColumn column, CellValue value, IRow row, int columnIndex, int rowIndex,
        DataTemplate? template, DataTemplate? editingTemplate = null)
    {
        if (IsUnrealizing) throw new System.InvalidOperationException("Cell unrealization is in progress.");
        var text = value as ITextCell;
        var options = value.TextOptions ?? column.TextOptions;
        ++_synchronizing;
        try
        {
            var alignment = text?.TextAlignment ?? options?.TextAlignment ?? TextAlignment.Left;
            var wrapping = text?.TextWrapping ?? options?.TextWrapping ?? TextWrapping.NoWrap;
            var trimming = text?.TextTrimming ?? options?.TextTrimming ?? TextTrimming.CharacterEllipsis;
            if (TextAlignment != alignment) TextAlignment = alignment;
            if (TextWrapping != wrapping) TextWrapping = wrapping;
            if (TextTrimming != trimming) TextTrimming = trimming;
        }
        finally { --_synchronizing; }
        base.Realize(column, value, row, columnIndex, rowIndex, template, editingTemplate);
    }
    protected override void UpdateValue()
    {
        var realization = RealizationVersion;
        var refresh = unchecked(++_valueRefreshVersion);
        if (IsEditing || IsUnrealizing) return;
        var model = ViewModel;
        var value = model is ITextCell text ? text.Text : model?.DisplayText ?? Column?.FormatValue(model?.Value);
        if (!Current()) return;
        ++_synchronizing;
        try { if (Value != value) Value = value; }
        finally { --_synchronizing; }
        if (Current()) base.UpdateValue();

        bool Current() => !IsUnrealizing && realization == RealizationVersion &&
            refresh == _valueRefreshVersion && ReferenceEquals(ViewModel, model);
    }
    protected override void ClearContent()
    {
        ++_synchronizing;
        try { Value = null; }
        finally { --_synchronizing; }
        base.ClearContent();
    }
    private static void ValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var cell = (TreeDataGridTextCell)sender;
        if (cell._synchronizing != 0 || cell.IsUnrealizing) return;
        var realization = cell.RealizationVersion;
        var write = unchecked(++cell._valueWriteVersion);
        var model = cell.ViewModel;
        var editing = cell.IsEditing;
        if (!Current()) return;
        if (editing)
        {
            // Also support changing Value programmatically with the lazy native
            // editor, whose Text is not necessarily template-bound to Value.
            if (cell.UsesTextEditor)
            {
                var text = (string?)e.NewValue ?? string.Empty;
                var previous = cell.EditingText;
                if (Current() && previous != text) cell.EditingText = text;
            }
            return;
        }
        if (model is not null)
        {
            try
            {
                var canEdit = model.CanEdit;
                if (!Current() || !canEdit) return;
                // A permission getter can start an edit without replacing the
                // model. Do not bypass that newly opened edit transaction.
                var nowEditing = cell.IsEditing;
                if (!Current() || nowEditing) return;
                if (model is ITextCell text) text.Text = (string?)e.NewValue;
                else model.Write(e.NewValue);
            }
            finally { if (Current()) cell.UpdateValue(); }
        }
        else cell.RefreshCellPresentation();

        bool Current() => !cell.IsUnrealizing && realization == cell.RealizationVersion &&
            write == cell._valueWriteVersion && ReferenceEquals(cell.ViewModel, model);
    }
    private static void AppearanceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var cell = (TreeDataGridTextCell)sender;
        if (cell._synchronizing == 0) cell.RefreshCellPresentation();
    }
}
