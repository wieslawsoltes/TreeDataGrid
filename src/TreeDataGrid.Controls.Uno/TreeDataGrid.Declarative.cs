using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using Uno.Controls.Presentation;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(TreeDataGrid), new PropertyMetadata(null, ItemsSourceChanged));
    private DeclarativeSource? _generatedSource;
    private bool _rebuildingDeclarative;
    private bool _rebuildDeclarativeAgain;

    public TreeDataGridColumns ColumnDefinitions { get; } = new();
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    internal ITreeDataGridSource? ActiveSource => Model ?? _explicitSource ?? _generatedSource?.Source;

    private void InitializeDeclarative() => ColumnDefinitions.CollectionChanged += OnDeclarativeColumnsChanged;
    private void OnDeclarativeColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildDeclarativeSource();
    private static void ItemsSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var grid = (TreeDataGrid)sender;
        if (grid.IsRestoring(e.Property, e.NewValue)) return;
        try { grid.RebuildDeclarativeSource(); }
        catch
        {
            if (ReferenceEquals(grid.ItemsSource, e.NewValue))
            {
                grid.RestoreValue(ItemsSourceProperty, e.OldValue);
            }
            throw;
        }
    }
    private void RebuildDeclarativeSource()
    {
        if (_rebuildingDeclarative) { _rebuildDeclarativeAgain = true; return; }
        _rebuildingDeclarative = true;
        try
        {
            do
            {
                _rebuildDeclarativeAgain = false;
                var definitions = ColumnDefinitions.ToArray();
                var next = ItemsSource is { } items && definitions.Length > 0 ? DeclarativeSource.Create(items, definitions) : null;
                var previous = _generatedSource;
                _generatedSource = next;
                try
                {
                    // A caller-owned Model takes precedence, but the declarative
                    // configuration remains ready when that Model is cleared.
                    if (Model is null && _explicitSource is null) ReplacePresentation();
                    PublishSource();
                }
                catch (System.Exception error)
                {
                    _generatedSource = previous;
                    try
                    {
                        if (next is not null && ReferenceEquals(_presentation?.Model, next.Source)) RestorePresentationAndSource();
                        else PublishSource();
                    }
                    catch (System.Exception restoreError) { throw new System.AggregateException(error, restoreError); }
                    finally
                    {
                        RetireGeneratedSource(next);
                        CollectRetiredGeneratedSources();
                    }
                    throw;
                }
                // View cells and native binding probes must be retired before
                // the generated source's transient accessor resources.
                RetireGeneratedSource(previous);
                CollectRetiredGeneratedSources();
            }
            while (_rebuildDeclarativeAgain);
        }
        finally { _rebuildingDeclarative = false; }
    }
}
