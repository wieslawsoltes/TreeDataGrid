using System;
using System.Collections;
using System.Collections.Specialized;
using Avalonia.Controls.Selection;
using Avalonia.Metadata;

namespace Avalonia.Controls
{
    public partial class TreeDataGrid
    {
        public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
            AvaloniaProperty.Register<TreeDataGrid, IEnumerable?>(nameof(ItemsSource));

        public static readonly StyledProperty<TreeDataGridSelectionMode> SelectionModeProperty =
            AvaloniaProperty.Register<TreeDataGrid, TreeDataGridSelectionMode>(
                nameof(SelectionMode),
                TreeDataGridSelectionMode.Row);

        private ITreeDataGridSource? _explicitSource;
        private ITreeDataGridSource? _generatedSource;
        private ITreeSelectionModel? _rowSelectionModel;
        private ITreeDataGridCellSelectionModel? _cellSelectionModel;
        private bool _updatingSelectionMode;
        private bool _selectionModelEventsSubscribed;

        [Content]
        public TreeDataGridColumns ColumnDefinitions { get; } = new();

        public IEnumerable? ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public TreeDataGridSelectionMode SelectionMode
        {
            get => GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        public event EventHandler<TreeDataGridSelectionChangedEventArgs>? SelectionChanged;

        private void InitializeV12Support()
        {
            ColumnDefinitions.CollectionChanged += OnDeclarativeColumnsChanged;
        }

        private void OnDeclarativeColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildGeneratedSource();
        }

        private void RebuildGeneratedSource()
        {
            _generatedSource = TreeDataGridDeclarativeHelper.CreateGeneratedSource(ColumnDefinitions, ItemsSource);
            UpdateActiveSource();
        }

        private void UpdateActiveSource()
        {
            var next = _explicitSource ?? _generatedSource;

            if (ReferenceEquals(_source, next))
            {
                ApplySelectionMode();
                SelectionInteraction = _source?.Selection as ITreeDataGridSelectionInteraction;
                SubscribeSelectionModel();
                return;
            }

            UnsubscribeSourceEvents();

            var oldSource = _source;
            _source = next;

            if (_source != null)
            {
                ApplySelectionMode(_source);
                Columns = _source.Columns;
                Rows = _source.Rows;
                SelectionInteraction = _source.Selection as ITreeDataGridSelectionInteraction;
            }
            else
            {
                Columns = null;
                Rows = null;
                SelectionInteraction = null;
            }

            SubscribeSourceEvents();
            SubscribeSelectionModel();
            RaisePropertyChanged(SourceProperty, oldSource, _source);
            RowsPresenter?.RecycleAllElements();
            RowsPresenter?.InvalidateMeasure();
        }

        private void ApplySelectionMode()
        {
            ApplySelectionMode(_source);
        }

        private void ApplySelectionMode(ITreeDataGridSource? source)
        {
            if (source is null || _updatingSelectionMode || !ShouldApplySelectionMode(source))
                return;

            _updatingSelectionMode = true;

            try
            {
                TreeDataGridDeclarativeHelper.ApplySelectionMode(source, SelectionMode);
            }
            finally
            {
                _updatingSelectionMode = false;
            }
        }

        private bool ShouldApplySelectionMode(ITreeDataGridSource source)
        {
            return ReferenceEquals(source, _generatedSource) || IsSet(SelectionModeProperty);
        }

        private void SubscribeSelectionModel()
        {
            UnsubscribeSelectionModelEvents();

            _rowSelectionModel = _source?.Selection as ITreeSelectionModel;
            _cellSelectionModel = _source?.Selection as ITreeDataGridCellSelectionModel;

            if (_source?.Selection is ITreeSelectionModel rowSelection &&
                _source.Selection is not ITreeDataGridCellSelectionModel)
            {
                _rowSelectionModel = rowSelection;
            }
            else
            {
                _rowSelectionModel = null;
            }

            SubscribeSelectionModelEvents();
        }

        private void SubscribeSelectionModelEvents()
        {
            if (!_isAttachedToVisualTree || _selectionModelEventsSubscribed)
                return;

            if (_rowSelectionModel is not null)
                _rowSelectionModel.SelectionChanged += OnRowSelectionChanged;

            if (_cellSelectionModel is not null)
                _cellSelectionModel.SelectionChanged += OnCellSelectionChanged;

            _selectionModelEventsSubscribed = _rowSelectionModel is not null || _cellSelectionModel is not null;
        }

        private void UnsubscribeSelectionModelEvents()
        {
            if (!_selectionModelEventsSubscribed)
                return;

            if (_rowSelectionModel is not null)
                _rowSelectionModel.SelectionChanged -= OnRowSelectionChanged;

            if (_cellSelectionModel is not null)
                _cellSelectionModel.SelectionChanged -= OnCellSelectionChanged;

            _selectionModelEventsSubscribed = false;
        }

        private void OnRowSelectionChanged(object? sender, TreeDataGridSelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, e);
        }

        private void OnCellSelectionChanged(object? sender, TreeDataGridSelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, e);
        }

    }
}
