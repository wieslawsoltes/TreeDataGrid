using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Presentation;

// One view facade, not another row collection: indexers/enumeration/mappings
// return the Core objects directly. Active public cells belong to their caller.
internal abstract class TreeDataGridRows : UI.ITreeDataGridRows, IDisposable
{
    private readonly TreeDataGridPresentation _owner;
    private bool _active;
    private bool _disposed;
    private int _revision;
    protected TreeDataGridRows(TreeDataGridPresentation owner, bool active)
    {
        _owner = owner;
        _active = active;
        owner.RowsChanged += OnRowsChanged;
    }
    protected IRows CoreRows => _owner.Model.Rows;
    public int Count => CoreRows.Count;
    public IRow this[int index] => CoreRows[index];
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public IEnumerator<IRow> GetEnumerator() => CoreRows.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int ModelIndexToRowIndex(IndexPath modelIndex) => CoreRows.ModelIndexToRowIndex(modelIndex);
    public IndexPath RowIndexToModelIndex(int rowIndex) => CoreRows.RowIndexToModelIndex(rowIndex);
    // Like the Avalonia view rows, the collection itself has no measured height
    // geometry. Native presenters own that geometry and provide precise lookup.
    public (int index, double y) GetRowAt(double y) => y == 0 ? (0, 0) : (-1, -1);
    public UI.ICell RealizeCell(UI.IColumn column, int columnIndex, int rowIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_active) throw new InvalidOperationException("The row presentation is suspended.");
        ArgumentNullException.ThrowIfNull(column);
        var revision = _revision;
        var cell = CreateCell(column, CoreRows[rowIndex]);
        if (revision == _revision && _active && !_disposed) return cell;
        var error = new OperationCanceledException("The row presentation changed during cell creation.");
        try { (cell as IDisposable)?.Dispose(); }
        catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
        throw error;
    }
    protected abstract UI.ICell CreateCell(UI.IColumn column, IRow row);
    public void UnrealizeCell(UI.ICell cell, int columnIndex, int rowIndex) => (cell as IDisposable)?.Dispose();
    internal void Suspend() { ++_revision; _active = false; }
    internal void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ++_revision;
        _active = true;
    }
    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        ++_revision;
        if (_active && !_disposed) CollectionChanged?.Invoke(this, args);
    }
    public void Dispose()
    {
        if (_disposed) return;
        ++_revision;
        _disposed = true;
        _active = false;
        _owner.RowsChanged -= OnRowsChanged;
    }

    internal static TreeDataGridRows Create(TreeDataGridPresentation owner, bool active) => owner.Model.Accept(new Factory(owner, active));
    private sealed class Factory(TreeDataGridPresentation owner, bool active) : ITreeDataGridSourceVisitor<TreeDataGridRows>
    {
        public TreeDataGridRows Visit<TModel>(ITreeDataGridSource<TModel> source) where TModel : class => new TypedRows<TModel>(owner, active);
    }
    private sealed class TypedRows<TModel>(TreeDataGridPresentation owner, bool active) : TreeDataGridRows(owner, active) where TModel : class
    {
        protected override UI.ICell CreateCell(UI.IColumn column, IRow row) => column switch
        {
            CellColumn native => native.CreateCellModel(row),
            ICellColumn<TModel> custom => custom.CreateCell((IRow<TModel>)row) ??
                throw new InvalidOperationException("The column returned no cell model."),
            _ => throw new ArgumentException("The column does not support this row model type.", nameof(column)),
        };
    }
}
