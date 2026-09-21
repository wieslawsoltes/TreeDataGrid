using System;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Uno.Controls.Selection;
using UICell = Uno.Controls.Models.TreeDataGrid.ICell;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridCell
{
    private readonly record struct RealizationContext(CellColumn Column, CellValue Value, IRow Row, object? RowModel,
        DataTemplate? Template, DataTemplate? EditingTemplate);
    private RealizationContext? _realizationContext;
    private bool _directRealization;
    private CellValue? _standaloneAdapter;
    private TreeDataGridElementFactory? _fallbackFactory;
    private (IRow Row, object? Model, CellColumn? Column)? _standaloneRow;
    private (IRow Row, object? Model)? _nativeRow;
    internal TreeDataGridRow? OwningRow { get; set; }

    internal void RealizeNativeInRow(CellColumn column, CellValue value, IRow row, object? rowModel,
        int columnIndex, int rowIndex, DataTemplate? template, DataTemplate? editingTemplate)
    {
        if (_nativeRow is not null) throw new InvalidOperationException("Cell realization is already in progress.");
        _nativeRow = (row, rowModel);
        try { Realize(column, value, row, columnIndex, rowIndex, template, editingTemplate); }
        finally { _nativeRow = null; }
    }

    internal void RealizeInRow(TreeDataGridElementFactory factory, ITreeDataGridSelectionInteraction? selection,
        UICell model, int columnIndex, int rowIndex, IRow row, object? rowModel, CellColumn? column)
    {
        if (_standaloneRow is not null) throw new InvalidOperationException("Cell realization is already in progress.");
        _standaloneRow = (row, rowModel, column);
        try { Realize(factory, selection, model, columnIndex, rowIndex); }
        finally { _standaloneRow = null; }
    }

    /// <summary>
    /// Realizes a borrowed UI cell model. Both native grid realization and
    /// independently hosted cells pass through this customization entry point.
    /// </summary>
    public virtual void Realize(TreeDataGridElementFactory factory, ITreeDataGridSelectionInteraction? selection,
        UICell model, int columnIndex, int rowIndex)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(model);
        if (ColumnIndex >= 0 || RowIndex >= 0) throw new InvalidOperationException("Cell is already realized.");
        if (columnIndex < 0) throw new IndexOutOfRangeException("Invalid column index.");
        if (rowIndex < 0) throw new IndexOutOfRangeException("Invalid row index.");
        if (_directRealization) throw new InvalidOperationException("Cell realization is already in progress.");

        ContainerFactory = factory;
        if (_realizationContext is { } context)
        {
            if (!ReferenceEquals(context.Value.PresentationModel, model))
                throw new InvalidOperationException("A realization override cannot replace the supplied cell model.");
            RealizeCore(context.Column, context.Value, context.Row, context.RowModel, columnIndex, rowIndex, context.Template, context.EditingTemplate);
            ApplyRealizedSelection(selection, model, columnIndex, rowIndex);
        }
        else
        {
            // A standalone control borrows its model. Only the adapter's own
            // observation is released by Unrealize; the caller owns the model.
            _directRealization = true;
            try
            {
                var value = CellColumnAdapter<object>.Adapt(model, ownsModel: false);
                if (!ReferenceEquals(value, model)) _standaloneAdapter = value;
                var row = _standaloneRow?.Row ?? (value is ExpanderCellValue expander ? expander.Row : new StandaloneRow(model.Value));
                var column = _standaloneRow?.Column ?? new StandaloneColumn(value);
                var template = value.GetCellTemplate(this) ?? column.GetCellTemplate(this);
                var editing = value.GetCellEditingTemplate(this) ?? column.GetCellEditingTemplate(this);
                // Preserve specialized native text/checkbox/template setup and
                // pre-existing Uno overrides, without calling this hook twice.
                Realize(column, value, row, columnIndex, rowIndex, template, editing);
                ApplyRealizedSelection(selection, model, columnIndex, rowIndex);
            }
            catch (Exception error)
            {
                try { Unrealize(); }
                catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
            finally { _directRealization = false; }
        }
    }

    private void ApplyRealizedSelection(ITreeDataGridSelectionInteraction? selection, UICell model, int columnIndex, int rowIndex)
    {
        if (ReferenceEquals(Model, model) && ColumnIndex == columnIndex && RowIndex == rowIndex)
        {
            var realization = RealizationVersion;
            var selected = selection?.IsCellSelected(columnIndex, rowIndex) == true;
            if (realization == RealizationVersion && ReferenceEquals(Model, model) && ColumnIndex == columnIndex && RowIndex == rowIndex)
                IsSelected = selected;
        }
    }

    private sealed class StandaloneRow(object? model) : IRow
    {
        public object? Header => null;
        public object? Model => model;
        public TreeDataGridCore.GridLength Height { get; set; } = TreeDataGridCore.GridLength.Auto;
    }

    private sealed class StandaloneColumn(CellValue value) : CellColumn(
        ValueColumn<object, object?>.FromDelegate(null, static row => row))
    {
        public override CellKind Kind => value.Kind;
        public override TextCellOptions? TextOptions => value.TextOptions;
        public override bool IsThreeState => value.IsThreeState == true;
        public override CellValue CreateCell(IRow row) => throw new NotSupportedException("A standalone cell does not create row models.");
    }
}
