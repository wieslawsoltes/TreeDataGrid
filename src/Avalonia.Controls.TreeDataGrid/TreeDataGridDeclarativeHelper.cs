using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls.Selection;

namespace Avalonia.Controls
{
    internal static class TreeDataGridDeclarativeHelper
    {
        public static ITreeDataGridSource? CreateGeneratedSource(TreeDataGridColumns columns, IEnumerable? itemsSource)
        {
            if (itemsSource is null || columns.Count == 0)
                return null;

            var sample = TryGetSampleItem(itemsSource);
            var modelType = sample?.GetType() ?? TryGetItemType(itemsSource);

            foreach (var column in columns)
                column.InitializeFromSample(sample, modelType);

            var items = GetSourceItems(itemsSource);

            if (columns.Any(x => x is TreeDataGridHierarchicalExpanderColumn))
            {
                var source = new HierarchicalTreeDataGridSource<object>(items);

                foreach (var column in columns)
                    source.Columns.Add(column.CreateUntypedColumn());

                return source;
            }
            else
            {
                var source = new FlatTreeDataGridSource<object>(items);

                foreach (var column in columns)
                    source.Columns.Add(column.CreateUntypedColumn());

                return source;
            }
        }

        public static void ApplySelectionMode(ITreeDataGridSource source, TreeDataGridSelectionMode selectionMode)
        {
            var useCellSelection = (selectionMode & TreeDataGridSelectionMode.Cell) == TreeDataGridSelectionMode.Cell;
            var singleSelect = (selectionMode & TreeDataGridSelectionMode.Multiple) != TreeDataGridSelectionMode.Multiple;

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

        public static object? TryGetSampleItem(IEnumerable items)
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

        public static Type? TryGetItemType(IEnumerable items)
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

        public static IEnumerable<object> GetSourceItems(IEnumerable items)
        {
            if ((items is IList || items is INotifyCollectionChanged) &&
                items is IEnumerable<object> typedItems)
            {
                return typedItems;
            }

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
            if (selection is ITreeDataGridSingleSelectSupport support)
            {
                support.SingleSelect = singleSelect;
                return;
            }

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
