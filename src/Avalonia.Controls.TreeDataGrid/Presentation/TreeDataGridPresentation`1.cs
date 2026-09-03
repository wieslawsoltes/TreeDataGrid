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
        private readonly HashSet<Core.Models.IColumn<TModel>> _failedColumns;
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
            _cells = new(model.Columns.Count, ReferenceEqualityComparer.Instance);
            _failedColumns = new(ReferenceEqualityComparer.Instance);
            _columnChanged = OnColumnPropertyChanged;
            _factory = new(options ?? new());
            _columns = new(this);
            _columnNotifications = model.Columns as INotifyCollectionChanged;
            try
            {
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
            catch
            {
                Dispose();
                throw;
            }
        }
        public override object SourceIdentity => Model;
        public override bool CanSelectMultiple => _selection?.SingleSelect == false;
        public override void Select(IndexPath index, bool replace)
        { if (_selection is null) return; if (replace) _selection.SelectedIndex = index.ToCore(); else _selection.Select(index.ToCore()); }
        public override void Deselect(IndexPath index) => _selection?.Deselect(index.ToCore());
        public override bool IsSelected(IndexPath index) => _selection?.IsSelected(index.ToCore()) ?? false;
        public Core.ITreeDataGridSource<TModel> Model { get; }
        public override IColumns Columns => _columns;
        public override ITreeDataGridRows Rows
        {
            get
            {
                _rows ??= new(Model.Rows);
                _rows.SetRows(Model.Rows);
                return _rows;
            }
        }
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
            if (_selection is not null) _selection.SelectionChanged -= OnNativeSelectionChanged;
            _interaction?.Dispose();
            _interaction = null;
            _selection = null;
            PropertyChanged?.Invoke(this, new(nameof(SelectionInteraction)));
            _rows?.Suspend();
            Model.PropertyChanged -= OnModelPropertyChanged;
            Model.Sorted -= OnSorted;
            if (_columnNotifications is not null) _columnNotifications.CollectionChanged -= OnColumnsChanged;
            foreach (var column in _cells.Keys) column.PropertyChanged -= _columnChanged;
            foreach (var column in _failedColumns) column.PropertyChanged -= _columnChanged;
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
            SynchronizeRows();
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
            _failedColumns.Clear();
        }
        private void UpdateSelection()
        {
            var selection = Model.Selection;
            if (ReferenceEquals(selection, _selection)) return;
            if (selection is not null && selection is not Core.Selection.ITreeDataGridRowSelectionModel<TModel>)
                throw new NotSupportedException("This presentation requires a Core row selection model.");
            if (_selection is not null) _selection.SelectionChanged -= OnNativeSelectionChanged;
            _interaction?.Dispose();
            _selection = selection as Core.Selection.ITreeDataGridRowSelectionModel<TModel>;
            _interaction = _selection is null ? null : new(Model, _selection);
            if (_selection is not null) _selection.SelectionChanged += OnNativeSelectionChanged;
            PropertyChanged?.Invoke(this, new(nameof(SelectionInteraction)));
        }
        internal override void ApplySelectionMode(TreeDataGridSelectionMode mode)
        {
            if ((mode & TreeDataGridSelectionMode.Cell) != 0)
                throw new NotSupportedException("Core models support row selection; use the compatibility Source API for cell selection.");
            if (Model.Selection is null) Model.Selection = new Core.Selection.TreeDataGridRowSelectionModel<TModel>(Model);
            if (Model.Selection is Core.Selection.ITreeDataGridRowSelectionModel<TModel> selection) selection.SingleSelect = (mode & TreeDataGridSelectionMode.Multiple) == 0;
        }
        private void OnNativeSelectionChanged(object? sender, Core.Selection.TreeSelectionModelSelectionChangedEventArgs<TModel> e) =>
            RaiseNativeSelectionChanged(e);
        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Model.Selection)) UpdateSelection();
            else
            {
                if (e.PropertyName == nameof(Model.Rows)) SynchronizeRows();
                PropertyChanged?.Invoke(this, e);
            }
        }
        private void OnSorted() => Sorted?.Invoke();
        private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SynchronizeColumns();
        private ICellColumn<TModel> CreateColumn(Core.Models.IColumn<TModel> model)
        {
            ICellColumn<TModel>? cell = null;
            try
            {
                cell = CreateCell(model);
                _cells.Add(model, new(cell, model.PresentationKey));
                var wasFailed = _failedColumns.Remove(model);
                if (!_suspended && !wasFailed) model.PropertyChanged += _columnChanged;
                return cell;
            }
            catch
            {
                if (_cells.Remove(model))
                    model.PropertyChanged -= _columnChanged;
                if (!_suspended && _failedColumns.Add(model))
                    model.PropertyChanged += _columnChanged;
                (cell as IDisposable)?.Dispose();
                throw;
            }
        }
        private ICellColumn<TModel> CreateCell(Core.Models.IColumn<TModel> model)
        {
            var cell = model.Accept(_factory);
            try
            {
                if (cell.Width != model.Width.ToAvalonia()) cell.SetWidth(model.Width.ToAvalonia());
                cell.SortDirection = model.SortDirection;
                return cell;
            }
            catch
            {
                (cell as IDisposable)?.Dispose();
                throw;
            }
        }
        private void RemoveColumn(Core.Models.IColumn<TModel> model)
        {
            model.PropertyChanged -= _columnChanged;
            var cell = _cells[model].Cell;
            _cells.Remove(model);
            (cell as IDisposable)?.Dispose();
        }
        private void ReplaceColumn(Core.Models.IColumn<TModel> model)
        {
            var previous = _cells[model];
            var replacement = CreateCell(model);
            _cells[model] = new(replacement, model.PresentationKey);
            var index = _columns.IndexOf(previous.Cell);
            if (index >= 0) _columns.RemoveAt(index);
            (previous.Cell as IDisposable)?.Dispose();
        }
        private void SynchronizeColumns()
        {
            var desired = Model.Columns;
            if (_cells.Count > 0 || _failedColumns.Count > 0)
            {
                var desiredSet = new HashSet<Core.Models.IColumn>(
                    desired,
                    ReferenceEqualityComparer.Instance);
                foreach (var removed in _cells.Keys.Where(x => !desiredSet.Contains(x)).ToArray())
                    RemoveColumn(removed);
                foreach (var removed in _failedColumns.Where(x => !desiredSet.Contains(x)).ToArray())
                {
                    removed.PropertyChanged -= _columnChanged;
                    _failedColumns.Remove(removed);
                }
                foreach (var changed in _cells.Keys.Where(x =>
                    x.PresentationKey != _cells[x].Key).ToArray())
                    ReplaceColumn(changed);
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
        private void SynchronizeRows() => _rows?.SetRows(Model.Rows);
        private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Core.Models.IColumn.PresentationKey))
                SynchronizeColumns();
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
