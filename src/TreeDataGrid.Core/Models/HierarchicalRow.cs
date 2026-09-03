using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace TreeDataGridCore.Models
{
    /// <summary>
    /// A row in a <see cref="HierarchicalTreeDataGridSource{TModel}"/>.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    public class HierarchicalRow<TModel> : NotifyingBase,
        IExpanderRow<TModel>,
        IIndentedRow,
        IModelIndexableRow,
        IDisposable
    {
        private readonly IExpanderRowController<TModel> _controller;
        private readonly IExpanderColumn<TModel> _expanderColumn;
        private Comparison<TModel>? _comparison;
        private IEnumerable<TModel>? _childModels;
        private ChildRows? _childRows;
        private readonly IDisposable? _isExpandedSubscription;
        private readonly bool _observesExpansionViaModel;
        private INotifyPropertyChanged? _modelNotifications;
        private bool _isExpanded;
        private bool? _showExpander;

        public HierarchicalRow(
            IExpanderRowController<TModel> controller,
            IExpanderColumn<TModel> expanderColumn,
            IndexPath modelIndex,
            TModel model,
            Comparison<TModel>? comparison)
        {
            if (modelIndex.Count == 0)
                throw new ArgumentException("Invalid model index");

            _controller = controller;
            _expanderColumn = expanderColumn;
            _comparison = comparison;
            ModelIndexPath = modelIndex;
            Model = model;
            var expanded = expanderColumn.GetModelIsExpanded(model);
            if (expanded == true && expanderColumn.HasChildren(model))
            {
                _childModels = expanderColumn.GetChildModels(model);
                _childRows = new ChildRows(this, TreeDataGridItemsSourceView<TModel>.GetOrCreate(_childModels), comparison);
                _isExpanded = _childRows.Count > 0;
            }
            if (expanded.HasValue)
            {
                _isExpandedSubscription =
                    (expanderColumn as IModelExpansionObserver<TModel>)?.ExpansionObserver?.Subscribe(
                        model, OnModelIsExpandedChanged);
            }
            _observesExpansionViaModel = expanded.HasValue && _isExpandedSubscription is null;
            if (_observesExpansionViaModel || _isExpanded)
                SubscribeToModelChanges();
        }

        /// <summary>
        /// Gets the row's visible child rows.
        /// </summary>
        public IReadOnlyList<HierarchicalRow<TModel>>? Children => _isExpanded ? _childRows : null;

        internal IReadOnlyList<HierarchicalRow<TModel>>? MaterializedChildren => _childRows;

        /// <summary>
        /// Gets the index of the model relative to its parent.
        /// </summary>
        /// <remarks>
        /// To retrieve the index path to the model from the root data source, see
        /// <see cref="ModelIndexPath"/>.
        /// </remarks>
        public int ModelIndex => ModelIndexPath[^1];

        /// <summary>
        /// Gets the index path of the model in the data source.
        /// </summary>
        public IndexPath ModelIndexPath { get; private set; }

        public object? Header => ModelIndexPath;
        public int Indent => ModelIndexPath.Count - 1;
        public TModel Model { get; }

        public GridLength Height
        {
            get => GridLength.Auto;
            set { }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    if (value)
                        Expand();
                    else
                        Collapse();
                }
            }
        }

        public bool ShowExpander
        {
            get => _showExpander ?? _expanderColumn.HasChildren(Model);
            private set => RaiseAndSetIfChanged(ref _showExpander, value);
        }

        public void Dispose()
        {
            _isExpandedSubscription?.Dispose();
            UnsubscribeFromModelChanges();
            _childRows?.Dispose();
        }
        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isExpanded)
                RefreshChildModels();
            if (_observesExpansionViaModel)
                OnModelIsExpandedChanged();
        }

        private void OnModelIsExpandedChanged()
        {
            if (_expanderColumn.GetModelIsExpanded(Model) is bool expanded)
                IsExpanded = expanded;
        }

        public void UpdateModelIndex(int delta)
        {
            ModelIndexPath = ModelIndexPath[..^1].Append(ModelIndexPath[^1] + delta);

            if (_childRows is null)
                return;

            var childCount = _childRows.Count;

            for (var i = 0; i < childCount; ++i)
                _childRows[i].UpdateParentModelIndex(ModelIndexPath);
        }

        public void UpdateParentModelIndex(IndexPath parentIndex)
        {
            ModelIndexPath = parentIndex.Append(ModelIndex);

            if (_childRows is null)
                return;

            var childCount = _childRows.Count;

            for (var i = 0; i < childCount; ++i)
                _childRows[i].UpdateParentModelIndex(ModelIndexPath);
        }



        public void UpdateShowExpander(bool value) => ShowExpander = value;

        internal void ClearShowExpander()
        {
            if (_showExpander.HasValue)
            {
                _showExpander = null;
                RaisePropertyChanged(nameof(ShowExpander));
            }
        }

        internal void SortChildren(Comparison<TModel>? comparison)
        {
            _comparison = comparison;

            if (_childRows is null)
                return;

            _childRows.Sort(comparison);

            foreach (var row in _childRows)
            {
                row.SortChildren(comparison);
            }
        }

        private void Expand()
        {
            if (!_expanderColumn.HasChildren(Model))
            {
                _expanderColumn.SetModelIsExpanded(this);
                return;
            }

            _controller.OnBeginExpandCollapse(this);

            var oldExpanded = _isExpanded;
            var childModels = _expanderColumn.GetChildModels(Model);

            if (_childModels != childModels)
            {
                _childModels = childModels;
                _childRows?.Dispose();
                _childRows = new ChildRows(
                    this,
                    TreeDataGridItemsSourceView<TModel>.GetOrCreate(childModels),
                    _comparison);
            }

            if (_childRows?.Count > 0)
            {
                _isExpanded = true;
                SubscribeToModelChanges();
            }
            else
                ShowExpander = false;

            _controller.OnChildCollectionChanged(this, CollectionExtensions.ResetEvent);

            if (_isExpanded != oldExpanded)
                RaisePropertyChanged(nameof(IsExpanded));

            _controller.OnEndExpandCollapse(this);
            _expanderColumn.SetModelIsExpanded(this);
        }

        private void RefreshChildModels()
        {
            var childModels = _expanderColumn.GetChildModels(Model);
            if (ReferenceEquals(_childModels, childModels))
                return;

            var oldExpanded = _isExpanded;
            var replacement = new ChildRows(
                this,
                TreeDataGridItemsSourceView<TModel>.GetOrCreate(childModels),
                _comparison);
            var newExpanded = replacement.Count > 0;
            if (newExpanded != oldExpanded)
                _controller.OnBeginExpandCollapse(this);
            _childModels = childModels;
            _childRows?.Dispose();
            _childRows = replacement;
            _isExpanded = newExpanded;
            if (_isExpanded)
                ClearShowExpander();
            else
            {
                ShowExpander = false;
                replacement.ObserveChangesWhileEmpty();
            }
            _controller.OnChildCollectionChanged(this, CollectionExtensions.ResetEvent);
            if (_isExpanded != oldExpanded)
            {
                RaisePropertyChanged(nameof(IsExpanded));
                _controller.OnEndExpandCollapse(this);
            }

            if (_isExpanded != oldExpanded)
                _expanderColumn.SetModelIsExpanded(this);
            if (!_isExpanded && !_observesExpansionViaModel)
                UnsubscribeFromModelChanges();
        }

        private void SubscribeToModelChanges()
        {
            if (_modelNotifications is null && Model is INotifyPropertyChanged notify)
            {
                _modelNotifications = notify;
                notify.PropertyChanged += OnModelPropertyChanged;
            }
        }

        private void UnsubscribeFromModelChanges()
        {
            if (_modelNotifications is not null)
            {
                _modelNotifications.PropertyChanged -= OnModelPropertyChanged;
                _modelNotifications = null;
            }
        }

        private void Collapse()
        {
            _controller.OnBeginExpandCollapse(this);
            _isExpanded = false;
            _controller.OnChildCollectionChanged(this, CollectionExtensions.ResetEvent);
            RaisePropertyChanged(nameof(IsExpanded));
            _controller.OnEndExpandCollapse(this);
            _expanderColumn.SetModelIsExpanded(this);
            if (!_observesExpansionViaModel)
                UnsubscribeFromModelChanges();
        }

        private class ChildRows : SortableRowsBase<TModel, HierarchicalRow<TModel>>,
            IReadOnlyList<HierarchicalRow<TModel>>
        {
            private readonly HierarchicalRow<TModel> _owner;

            public ChildRows(
                HierarchicalRow<TModel> owner,
                TreeDataGridItemsSourceView<TModel> items,
                Comparison<TModel>? comparison)
                : base(items, comparison)
            {
                _owner = owner;
                CollectionChanged += OnCollectionChanged;
            }

            protected override HierarchicalRow<TModel> CreateRow(int modelIndex, TModel model)
            {
                return new HierarchicalRow<TModel>(
                    _owner._controller,
                    _owner._expanderColumn,
                    _owner.ModelIndexPath.Append(modelIndex),
                    model,
                    _owner._comparison);
            }

            public void ObserveChangesWhileEmpty()
            {
                using var enumerator = GetEnumerator();
            }

            private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                _owner.ClearShowExpander();
                if (_owner.IsExpanded)
                    _owner._controller.OnChildCollectionChanged(_owner, e);
            }
        }
    }
}
