using System;
using System.Collections;
using System.Collections.Specialized;
using Uno.Controls.Selection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace Uno.Controls
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
            _generatedSource = TreeDataGridDeclarativeHelper.CreateGeneratedSource(ColumnDefinitions, ItemsSource);
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
                TreeDataGridDeclarativeHelper.ApplySelectionMode(source, SelectionMode);
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

    }
}
