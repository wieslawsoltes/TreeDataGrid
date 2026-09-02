using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Core = global::TreeDataGridCore;

namespace Avalonia.Controls.Adapters
{
    public static class TreeDataGridSourceAdapter
    {
        public static ITreeDataGridSource Create(Core.ITreeDataGridSource source) => source.Accept(Factory.Instance);
        private sealed class Factory : Core.ITreeDataGridSourceVisitor<ITreeDataGridSource>
        {
            public static Factory Instance { get; } = new();
            public ITreeDataGridSource Visit<TModel>(Core.ITreeDataGridSource<TModel> source) where TModel : class => new TreeDataGridSourceAdapter<TModel>(source);
        }
    }

    /// <summary>
    /// Presents a framework-neutral source using the existing TreeDataGrid control API.
    /// The view owns and disposes this adapter; disposing it leaves the source usable.
    /// Source updates must be serialized with presentation on the UI thread.
    /// </summary>
    public sealed class TreeDataGridSourceAdapter<TModel> : ITreeDataGridSource<TModel>, ITreeDataGridSelectionFactory, IDisposable where TModel : class
    {
        private readonly CoreColumnFactory<TModel> _factory;
        private readonly Dictionary<Core.Models.IColumn<TModel>, CoreColumnAdapter<TModel>> _columns = new();
        private readonly INotifyCollectionChanged? _columnNotifications;
        private readonly CoreRowsAdapter<TModel> _rows;
        private CoreRowSelectionAdapter<TModel>? _selection;
        private bool _disposed;
        public TreeDataGridSourceAdapter(Core.ITreeDataGridSource<TModel> model, TreeDataGridPresentationOptions<TModel>? options = null)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _factory = new(options ?? new());
            _columnNotifications = model.Columns as INotifyCollectionChanged;
            SynchronizeColumns();
            _rows = new(model.Rows);
            UpdateSelection();
            if (_columnNotifications is not null) _columnNotifications.CollectionChanged += OnColumnsChanged;
            Model.PropertyChanged += OnModelPropertyChanged;
            Model.Sorted += OnSorted;
        }
        public Core.ITreeDataGridSource<TModel> Model { get; }
        public ColumnList<TModel> Columns { get; } = new();
        IColumns ITreeDataGridSource.Columns => Columns;
        public IRows Rows => _rows;
        public IEnumerable<TModel> Items { get => Model.Items; set => Model.Items = value; }
        IEnumerable<object> ITreeDataGridSource.Items => Model.Items;
        public bool IsHierarchical => Model.IsHierarchical;
        public bool IsSorted => Model.IsSorted;
        public ITreeDataGridRowSelectionModel<TModel>? RowSelection => _selection;
        public ITreeDataGridSelection? Selection
        {
            get => _selection;
            set
            {
                if (ReferenceEquals(value, _selection)) return;
                if (value is null) Model.Selection = null;
                else if (value is CoreRowSelectionAdapter<TModel> adapter) Model.Selection = adapter.Model;
                else throw new ArgumentException("Set selection on the neutral source, or use the existing FlatTreeDataGridSource/HierarchicalTreeDataGridSource API for a legacy selection model.", nameof(value));
            }
        }
        ITreeDataGridSelection ITreeDataGridSelectionFactory.CreateRowSelectionModel()
        {
            Model.Selection = new Core.Selection.TreeDataGridRowSelectionModel<TModel>(Model);
            return _selection!;
        }
        ITreeDataGridSelection ITreeDataGridSelectionFactory.CreateCellSelectionModel() =>
            throw new NotSupportedException("Neutral sources currently support row selection. Use the existing source API for cell selection.");
        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? Sorted;
        public IEnumerable<object>? GetModelChildren(object model) => Model.GetModelChildren(model);
        public bool SortBy(IColumn column, ListSortDirection direction) => column is CoreColumnAdapter<TModel> adapter && Model.SortBy(adapter.Model, direction);
        public void DragDropRows(ITreeDataGridSource source, IEnumerable<IndexPath> indexes, IndexPath targetIndex, TreeDataGridRowDropPosition position, DragDropEffects effects)
        {
            if (source is not TreeDataGridSourceAdapter<TModel> adapter) throw new ArgumentException("The drag source must present a neutral source of the same model type.", nameof(source));
            Model.MoveRows(adapter.Model, indexes.Select(x => x.ToCore()), targetIndex.ToCore(), (Core.RowDropPosition)position, (Core.RowMoveEffects)effects);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Model.PropertyChanged -= OnModelPropertyChanged;
            Model.Sorted -= OnSorted;
            if (_columnNotifications is not null) _columnNotifications.CollectionChanged -= OnColumnsChanged;
            _rows.Dispose();
            _selection?.Dispose();
            foreach (var pair in _columns) { pair.Key.PropertyChanged -= OnColumnPropertyChanged; pair.Value.Dispose(); }
            _columns.Clear();
        }
        private void UpdateSelection()
        {
            var selection = Model.Selection;
            if (ReferenceEquals(selection, _selection?.Model)) return;
            if (selection is not null && selection is not Core.Selection.ITreeDataGridRowSelectionModel<TModel>)
                throw new NotSupportedException("This presentation requires a neutral row selection model.");
            _selection?.Dispose();
            _selection = selection is Core.Selection.ITreeDataGridRowSelectionModel<TModel> rows ? new(this, rows) : null;
            PropertyChanged?.Invoke(this, new(nameof(Selection)));
        }
        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Selection)) UpdateSelection();
            else PropertyChanged?.Invoke(this, e);
        }
        private void OnSorted() => Sorted?.Invoke();
        private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SynchronizeColumns();
        private void SynchronizeColumns()
        {
            var desired = Model.Columns.Cast<Core.Models.IColumn<TModel>>().ToArray();
            foreach (var removed in _columns.Keys.Where(x => !desired.Contains(x)).ToArray())
            { removed.PropertyChanged -= OnColumnPropertyChanged; _columns[removed].Dispose(); _columns.Remove(removed); }
            foreach (var column in desired)
            {
                if (_columns.ContainsKey(column)) continue;
                _columns.Add(column, new(column, column.Accept(_factory)));
                column.PropertyChanged += OnColumnPropertyChanged;
            }
            var visible = desired.Where(x => x.IsVisible).Select(x => _columns[x]).ToArray();
            for (var i = 0; i < visible.Length; ++i)
            {
                if (i < Columns.Count && ReferenceEquals(Columns[i], visible[i])) continue;
                var oldIndex = Columns.IndexOf(visible[i]);
                if (oldIndex >= 0) Columns.RemoveAt(oldIndex);
                Columns.Insert(i, visible[i]);
            }
            while (Columns.Count > visible.Length) Columns.RemoveAt(Columns.Count - 1);
        }
        private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Expander columns forward their inner column's sender, so compare identities through
            // the subscriptions' public properties rather than assuming the event sender is a key.
            if (e.PropertyName == nameof(Core.Models.IColumn.PresentationKey))
            {
                foreach (var pair in _columns.Where(x => x.Key.PresentationKey != x.Value.PresentationKey).ToArray())
                {
                    pair.Key.PropertyChanged -= OnColumnPropertyChanged;
                    pair.Value.Dispose();
                    _columns.Remove(pair.Key);
                }
                SynchronizeColumns();
                return;
            }
            if (e.PropertyName == nameof(Core.Models.IColumn.IsVisible)) { SynchronizeColumns(); return; }
            for (var i = 0; i < Columns.Count; ++i)
            {
                var column = (CoreColumnAdapter<TModel>)Columns[i];
                if (e.PropertyName == nameof(Core.Models.IColumn.Width)) Columns.SetColumnWidth(i, column.Model.Width.ToAvalonia());
                else if (e.PropertyName == nameof(Core.Models.IColumn.SortDirection)) column.RefreshSortDirection();
            }
        }
    }
}
