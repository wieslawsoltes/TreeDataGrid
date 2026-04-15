using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls.Selection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace Avalonia.Controls
{
    [ContentProperty(Name = nameof(ColumnDefinitions))]
    public partial class TreeDataGrid
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(TreeDataGrid),
                new PropertyMetadata(null, OnItemsSourcePropertyChanged));

        public static readonly DependencyProperty SelectionModeProperty =
            DependencyProperty.Register(
                nameof(SelectionMode),
                typeof(TreeDataGridSelectionMode),
                typeof(TreeDataGrid),
                new PropertyMetadata(TreeDataGridSelectionMode.Row, OnSelectionModePropertyChanged));

        private ITreeDataGridSource? _explicitSource;
        private ITreeDataGridSource? _generatedSource;
        private bool _updatingSelectionMode;

        public TreeDataGridColumns ColumnDefinitions { get; } = new();

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public TreeDataGridSelectionMode SelectionMode
        {
            get => (TreeDataGridSelectionMode)GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        public event EventHandler<TreeDataGridSelectionChangedEventArgs>? SelectionChanged;

        private static void OnItemsSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeDataGrid)d).RebuildGeneratedSource();
        }

        private static void OnSelectionModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeDataGrid)d).ApplySelectionMode();
            ((TreeDataGrid)d).RebindSelection();
        }

        private void InitializeV12Support()
        {
            ColumnDefinitions.CollectionChanged += OnDeclarativeColumnsChanged;
        }

        private void OnDeclarativeColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildGeneratedSource();
        }

        private void OnExplicitSourceChanged(ITreeDataGridSource? source)
        {
            _explicitSource = source;
            UpdateActiveSource();
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

            var items = GetSourceItems(ItemsSource);

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
                RebindSelection();
                return;
            }

            OnSourceChanged(_source, next);
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
            return ReferenceEquals(source, _generatedSource) ||
                ReadLocalValue(SelectionModeProperty) != DependencyProperty.UnsetValue;
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

        private static IEnumerable<object> GetSourceItems(IEnumerable items)
        {
            if (items is IEnumerable<object> typedItems)
                return typedItems.Where(x => x is not null)!;

            return EnumerateItems(items);
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

            var property = selection?.GetType().GetProperty("SingleSelect");

            if (property?.CanWrite == true)
                property.SetValue(selection, singleSelect);
        }
    }
}
