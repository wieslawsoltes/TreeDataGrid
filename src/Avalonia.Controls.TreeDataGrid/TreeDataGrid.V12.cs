using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Collections;
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
            if (ItemsSource is null || ColumnDefinitions.Count == 0)
            {
                _generatedSource = null;
                UpdateActiveSource();
                return;
            }

            var sample = TryGetSampleItem(ItemsSource);
            var modelType = sample?.GetType() ?? TryGetItemType(ItemsSource);

            foreach (var column in ColumnDefinitions)
                column.InitializeFromSample(sample, modelType);

            var items = EnumerateItems(ItemsSource);

            if (ColumnDefinitions.Any(x => x is TreeDataGridHierarchicalExpanderColumn))
            {
                var source = new HierarchicalTreeDataGridSource<object>(items);

                foreach (var column in ColumnDefinitions)
                    source.Columns.Add(column.CreateUntypedColumn());

                _generatedSource = source;
            }
            else
            {
                var source = new FlatTreeDataGridSource<object>(items);

                foreach (var column in ColumnDefinitions)
                    source.Columns.Add(column.CreateUntypedColumn());

                _generatedSource = source;
            }

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
                var useCellSelection = (SelectionMode & TreeDataGridSelectionMode.Cell) == TreeDataGridSelectionMode.Cell;
                var singleSelect = (SelectionMode & TreeDataGridSelectionMode.Multiple) != TreeDataGridSelectionMode.Multiple;

                if (useCellSelection)
                {
                    if (source.Selection is not ITreeDataGridCellSelectionModel)
                        source.Selection = CreateCellSelectionModel(source);

                    SetSingleSelect(source.Selection, singleSelect);
                }
                else
                {
                    if (source.Selection is ITreeDataGridCellSelectionModel || source.Selection is null)
                        source.Selection = CreateRowSelectionModel(source);

                    SetSingleSelect(source.Selection, singleSelect);
                }
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

        private static object? TryGetSampleItem(IEnumerable items)
        {
            if (items is IList list)
            {
                for (var i = 0; i < list.Count; ++i)
                {
                    if (list[i] is not null)
                        return list[i];
                }

                return null;
            }

            foreach (var item in items)
            {
                if (item is not null)
                    return item;
            }

            return null;
        }

        private static Type? TryGetItemType(IEnumerable items)
        {
            var type = items.GetType();

            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return type.GetGenericArguments()[0];
            }

            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType &&
                    iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return iface.GetGenericArguments()[0];
                }
            }

            return null;
        }

        private static IEnumerable<object> EnumerateItems(IEnumerable items)
        {
            foreach (var item in items)
            {
                if (item is not null)
                    yield return item;
            }
        }

        private static ITreeDataGridSelection CreateRowSelectionModel(ITreeDataGridSource source)
        {
            if (source is ITreeDataGridSelectionFactory factory)
                return factory.CreateRowSelectionModel();

            throw new InvalidOperationException(
                $"Source '{source.GetType().FullName}' does not support automatic row selection model creation.");
        }

        private static ITreeDataGridSelection CreateCellSelectionModel(ITreeDataGridSource source)
        {
            if (source is ITreeDataGridSelectionFactory factory)
                return factory.CreateCellSelectionModel();

            throw new InvalidOperationException(
                $"Source '{source.GetType().FullName}' does not support automatic cell selection model creation.");
        }

        private static void SetSingleSelect(object? selection, bool singleSelect)
        {
            if (selection is ITreeSelectionModel treeSelection)
            {
                treeSelection.SingleSelect = singleSelect;
                return;
            }

            if (selection is ITreeDataGridSingleSelectSupport support)
                support.SingleSelect = singleSelect;
        }
    }
}
