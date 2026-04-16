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
                SubscribeSelectionModel();
                return;
            }

            if (_source != null)
            {
                _source.PropertyChanged -= OnSourcePropertyChanged;
                _source.Sorted -= OnSourceSorted;
            }

            var oldSource = _source;
            _source = next;

            if (_source != null)
            {
                _source.PropertyChanged += OnSourcePropertyChanged;
                _source.Sorted += OnSourceSorted;
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
            if (_rowSelectionModel is not null)
                _rowSelectionModel.SelectionChanged -= OnRowSelectionChanged;

            if (_cellSelectionModel is not null)
                _cellSelectionModel.SelectionChanged -= OnCellSelectionChanged;

            _rowSelectionModel = _source?.Selection as ITreeSelectionModel;
            _cellSelectionModel = _source?.Selection as ITreeDataGridCellSelectionModel;

            if (_source?.Selection is ITreeSelectionModel rowSelection &&
                _source.Selection is not ITreeDataGridCellSelectionModel)
            {
                _rowSelectionModel = rowSelection;
                _rowSelectionModel.SelectionChanged += OnRowSelectionChanged;
            }
            else
            {
                _rowSelectionModel = null;
            }

            if (_cellSelectionModel is not null)
            {
                _cellSelectionModel.SelectionChanged += OnCellSelectionChanged;
            }
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
