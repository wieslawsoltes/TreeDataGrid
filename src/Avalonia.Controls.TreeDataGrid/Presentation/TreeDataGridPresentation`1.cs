using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls.Adapters;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Core = global::TreeDataGridCore;

namespace Avalonia.Controls.Presentation
{
    /// <summary>Native presentation of Core models. Owns only UI columns and input state.</summary>
    public sealed class TreeDataGridPresentation<TModel> : TreeDataGridPresentation where TModel : class
    {
        private readonly CellColumnFactory<TModel> _factory;
        private readonly Dictionary<Core.Models.IColumn<TModel>, ColumnView> _cells;
        private readonly PropertyChangedEventHandler _columnChanged;
        private readonly INotifyCollectionChanged? _columnNotifications;
        private readonly PresentationColumns _columns;
        private TreeDataGridRows<TModel>? _rows;
        private TreeDataGridRowSelectionInteraction<TModel>? _interaction;
        private Core.Selection.ITreeDataGridRowSelectionModel<TModel>? _selection;
        private bool _disposed;
        private bool _suspended;
        public TreeDataGridPresentation(Core.ITreeDataGridSource<TModel> model, TreeDataGridPresentationOptions<TModel>? options = null)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _cells = new(model.Columns.Count);
            _columnChanged = OnColumnPropertyChanged;
            _factory = new(options ?? new());
            _columns = new(this);
            _columnNotifications = model.Columns as INotifyCollectionChanged;
            foreach (Core.Models.IColumn<TModel> column in model.Columns)
            {
                var cell = CreateColumn(column);
                if (column.IsVisible) _columns.Add(cell);
            }
            UpdateSelection();
            if (_columnNotifications is not null) _columnNotifications.CollectionChanged += OnColumnsChanged;
            Model.PropertyChanged += OnModelPropertyChanged;
            Model.Sorted += OnSorted;
        }
        public override object SourceIdentity => Model;
        public override bool CanSelectMultiple => _selection?.SingleSelect == false;
        public override void Select(IndexPath index, bool replace)
        { if (_selection is null) return; if (replace) _selection.SelectedIndex = index.ToCore(); else _selection.Select(index.ToCore()); }
        public override void Deselect(IndexPath index) => _selection?.Deselect(index.ToCore());
        public override bool IsSelected(IndexPath index) => _selection?.IsSelected(index.ToCore()) ?? false;
        public Core.ITreeDataGridSource<TModel> Model { get; }
        public override IColumns Columns => _columns;
        public override ITreeDataGridRows Rows => _rows ??= new(Model.Rows);
        public override bool IsHierarchical => Model.IsHierarchical;
        public override bool IsSorted => Model.IsSorted;
        public override ITreeDataGridSelectionInteraction? SelectionInteraction => _interaction;
        public override IReadOnlyList<object?>? SelectedItems => _selection?.SelectedItems;
        public override IReadOnlyList<IndexPath>? SelectedIndexes => _selection?.SelectedIndexes.ToAvalonia();
        public override event PropertyChangedEventHandler? PropertyChanged;
        public override event Action? Sorted;
        public override bool SortBy(IColumn column, ListSortDirection direction) => Model.SortBy(GetModel(column), direction);
        private Core.Models.IColumn<TModel>? GetModel(IColumn column)
        {
            foreach (var pair in _cells) if (ReferenceEquals(pair.Value.Cell, column)) return pair.Key;
            return null;
        }
        public override void MoveRows(IEnumerable<IndexPath> indexes, IndexPath target, TreeDataGridRowDropPosition position, DragDropEffects effects) =>
            Model.MoveRows(Model, indexes.Select(x => x.ToCore()), target.ToCore(), (Core.RowDropPosition)position, (Core.RowMoveEffects)effects);
        internal override void Suspend()
        {
            if (_disposed || _suspended) return;
            _suspended = true;
            _interaction?.Dispose();
            _interaction = null;
            _selection = null;
            PropertyChanged?.Invoke(this, new(nameof(SelectionInteraction)));
            _rows?.Suspend();
            Model.PropertyChanged -= OnModelPropertyChanged;
            Model.Sorted -= OnSorted;
            if (_columnNotifications is not null) _columnNotifications.CollectionChanged -= OnColumnsChanged;
            foreach (var column in _cells.Keys) column.PropertyChanged -= _columnChanged;
        }
        internal override void Resume()
        {
            if (_disposed || !_suspended) return;
            SynchronizeColumns();
            foreach (var pair in _cells)
            {
                var index = _columns.IndexOf(pair.Value.Cell);
                if (index >= 0) _columns.SetColumnWidth(index, pair.Key.Width.ToAvalonia());
                else pair.Value.Cell.SetWidth(pair.Key.Width.ToAvalonia());
                pair.Value.Cell.SortDirection = pair.Key.SortDirection;
                pair.Key.PropertyChanged += _columnChanged;
            }
            _suspended = false;
            _rows?.Resume();
            UpdateSelection();
            if (_columnNotifications is not null) _columnNotifications.CollectionChanged += OnColumnsChanged;
            Model.PropertyChanged += OnModelPropertyChanged;
            Model.Sorted += OnSorted;
        }
        public override void Dispose()
        {
            if (_disposed) return;
            Suspend();
            _disposed = true;
            _rows?.Dispose();
            foreach (var pair in _cells) (pair.Value.Cell as IDisposable)?.Dispose();
            _cells.Clear();
        }
        private void UpdateSelection()
        {
            var selection = Model.Selection;
            if (ReferenceEquals(selection, _selection)) return;
            if (selection is not null && selection is not Core.Selection.ITreeDataGridRowSelectionModel<TModel>)
                throw new NotSupportedException("This presentation requires a Core row selection model.");
            _interaction?.Dispose();
            _selection = selection as Core.Selection.ITreeDataGridRowSelectionModel<TModel>;
            _interaction = _selection is null ? null : new(Model, _selection);
            PropertyChanged?.Invoke(this, new(nameof(SelectionInteraction)));
        }
        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        { if (e.PropertyName == nameof(Model.Selection)) UpdateSelection(); else PropertyChanged?.Invoke(this, e); }
        private void OnSorted() => Sorted?.Invoke();
        private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SynchronizeColumns();
        private ICellColumn<TModel> CreateColumn(Core.Models.IColumn<TModel> model)
        {
            var cell = model.Accept(_factory);
            if (cell.Width != model.Width.ToAvalonia()) cell.SetWidth(model.Width.ToAvalonia());
            cell.SortDirection = model.SortDirection;
            _cells.Add(model, new(cell, model.PresentationKey));
            if (!_suspended) model.PropertyChanged += _columnChanged;
            return cell;
        }
        private void RemoveColumn(Core.Models.IColumn<TModel> model)
        {
            model.PropertyChanged -= _columnChanged;
            var cell = _cells[model].Cell;
            _cells.Remove(model);
            (cell as IDisposable)?.Dispose();
        }
        private void SynchronizeColumns()
        {
            var desired = Model.Columns;
            if (_cells.Count > 0)
            {
                var desiredSet = new HashSet<Core.Models.IColumn>(desired);
                foreach (var removed in _cells.Keys.Where(x =>
                    !desiredSet.Contains(x) || x.PresentationKey != _cells[x].Key).ToArray())
                    RemoveColumn(removed);
            }
            foreach (Core.Models.IColumn<TModel> model in desired)
            {
                if (_cells.ContainsKey(model)) continue;
                CreateColumn(model);
            }
            var target = 0;
            foreach (Core.Models.IColumn<TModel> model in desired)
            {
                if (!model.IsVisible) continue;
                var cell = _cells[model].Cell;
                if (target >= _columns.Count || !ReferenceEquals(_columns[target], cell))
                {
                    var oldIndex = _columns.IndexOf(cell);
                    if (oldIndex >= 0) _columns.RemoveAt(oldIndex);
                    _columns.Insert(target, cell);
                }
                ++target;
            }
            while (_columns.Count > target) _columns.RemoveAt(_columns.Count - 1);
        }
        private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Core.Models.IColumn.PresentationKey))
            {
                foreach (var model in _cells.Keys.Where(x => x.PresentationKey != _cells[x].Key).ToArray()) RemoveColumn(model);
                SynchronizeColumns();
            }
            else if (e.PropertyName == nameof(Core.Models.IColumn.IsVisible)) SynchronizeColumns();
            else if (e.PropertyName == nameof(Core.Models.IColumn.Width) || e.PropertyName == nameof(Core.Models.IColumn.SortDirection))
            {
                // Expander models forward notifications with their inner model as sender.
                foreach (var pair in _cells)
                {
                    if (e.PropertyName == nameof(Core.Models.IColumn.Width) && pair.Value.Cell.Width != pair.Key.Width.ToAvalonia())
                    {
                        var index = _columns.IndexOf(pair.Value.Cell);
                        if (index >= 0) _columns.SetColumnWidth(index, pair.Key.Width.ToAvalonia());
                        else ((IUpdateColumnLayout)pair.Value.Cell).SetWidth(pair.Key.Width.ToAvalonia());
                    }
                    else if (e.PropertyName == nameof(Core.Models.IColumn.SortDirection)) pair.Value.Cell.SortDirection = pair.Key.SortDirection;
                }
            }
        }
        private readonly struct ColumnView
        {
            public ColumnView(ICellColumn<TModel> cell, string? key) { Cell = cell; Key = key; }
            public ICellColumn<TModel> Cell { get; }
            public string? Key { get; }
        }
        private sealed class PresentationColumns : ColumnListBase<ICellColumn<TModel>>, IColumns
        {
            private readonly TreeDataGridPresentation<TModel> _owner;
            public PresentationColumns(TreeDataGridPresentation<TModel> owner) => _owner = owner;
            public new void SetColumnWidth(int index, GridLength width)
            {
                base.SetColumnWidth(index, width);
                if (_owner.GetModel(this[index]) is { } model) model.Width = width.ToCore();
            }
        }
    }
}
