using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using TreeDataGridCore.Selection;
using Uno.Controls.Presentation;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ITreeDataGridSource), typeof(TreeDataGrid), new PropertyMetadata(null, SourceChanged));
    private ITreeDataGridSource? _explicitSource;
    private int _configurationRevision;
    private bool _clearingModelForSource;
    private bool _publishingSource;
    private ITreeDataGridSource? _publishedSourceValue;
    private DependencyProperty? _restoringProperty;
    private object? _restoringValue;
    private readonly HashSet<DeclarativeSource> _retiredGeneratedSources = new();

    /// <summary>Compatibility entry point. New Core consumers can use Model; neither caller-supplied source is owned.</summary>
    public ITreeDataGridSource? Source
    {
        get => (ITreeDataGridSource?)GetValue(SourceProperty);
        set
        {
            // A CLR assignment of the current generated source still expresses
            // an explicit choice, even if a native DP change would be elided.
            if (ReferenceEquals(GetValue(SourceProperty), value)) AssignSource(value);
            else SetValue(SourceProperty, value);
        }
    }
    public ITreeDataGridRowSelectionModel? RowSelection => Source?.Selection as ITreeDataGridRowSelectionModel;
    public ITreeDataGridCellSelectionModel? ColumnSelection => Source?.Selection as ITreeDataGridCellSelectionModel;

    private static void SourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var grid = (TreeDataGrid)sender;
        if (grid.IsRestoring(e.Property, e.NewValue) ||
            (grid._publishingSource && ReferenceEquals(grid._publishedSourceValue, e.NewValue))) return;
        grid.AssignSource((ITreeDataGridSource?)e.NewValue);
    }
    private void AssignSource(ITreeDataGridSource? value)
    {
        var revision = ++_configurationRevision;
        var previousExplicit = _explicitSource;
        var previousModel = Model;
        _explicitSource = value;
        try
        {
            var clearing = _clearingModelForSource;
            _clearingModelForSource = true;
            try { if (Model is not null) SetValue(ModelProperty, null); }
            finally { _clearingModelForSource = clearing; }
            if (revision != _configurationRevision) return;
            ReplacePresentation();
            if (revision == _configurationRevision) PublishSource();
        }
        catch (Exception error)
        {
            if (revision == _configurationRevision)
            {
                _explicitSource = previousExplicit;
                try
                {
                    RestoreValue(ModelProperty, previousModel);
                    if (revision == _configurationRevision) RestorePresentationAndSource();
                }
                catch (Exception restoreError) { throw new AggregateException(error, restoreError); }
            }
            throw;
        }
        finally { CollectRetiredGeneratedSources(); }
    }
    private void OnPresentationConfigurationChanged(DependencyPropertyChangedEventArgs e)
    {
        if (IsRestoring(e.Property, e.NewValue) ||
            (e.Property == ModelProperty && _clearingModelForSource && e.NewValue is null)) return;
        var revision = ++_configurationRevision;
        var previousExplicit = _explicitSource;
        if (e.Property == ModelProperty && e.NewValue is not null) _explicitSource = null;
        try
        {
            ReplacePresentation();
            if (revision == _configurationRevision) PublishSource();
        }
        catch (Exception error)
        {
            if (revision == _configurationRevision && ReferenceEquals(GetValue(e.Property), e.NewValue))
            {
                _explicitSource = previousExplicit;
                try
                {
                    RestoreValue(e.Property, e.OldValue);
                    if (revision == _configurationRevision) RestorePresentationAndSource();
                }
                catch (Exception restoreError) { throw new AggregateException(error, restoreError); }
            }
            throw;
        }
        finally { CollectRetiredGeneratedSources(); }
    }
    private void RestorePresentationAndSource()
    {
        if (!ReferenceEquals(_presentation?.Model, ActiveSource)) ReplacePresentation();
        else
        {
            // A throwing clearing callback may have emptied the presenter even
            // though the old view survived. Restore its association as well.
            _presenter?.SetPresentation(_loaded ? _presentation : null, _geometry);
            UpdateColumns();
        }
        PublishSource();
    }
    private bool IsRestoring(DependencyProperty property, object? value) =>
        property == _restoringProperty && ReferenceEquals(value, _restoringValue);
    private void RestoreValue(DependencyProperty property, object? value)
    {
        var previousProperty = _restoringProperty;
        var previousValue = _restoringValue;
        _restoringProperty = property;
        _restoringValue = value;
        try { SetValue(property, value); }
        finally { _restoringProperty = previousProperty; _restoringValue = previousValue; }
    }
    private void PublishSource()
    {
        var value = Model is null ? _explicitSource ?? _generatedSource?.Source : null;
        if (ReferenceEquals(GetValue(SourceProperty), value)) return;
        var publishing = _publishingSource;
        var previous = _publishedSourceValue;
        _publishingSource = true;
        _publishedSourceValue = value;
        try { SetValue(SourceProperty, value); }
        finally { _publishingSource = publishing; _publishedSourceValue = previous; }
    }
    private void RetireGeneratedSource(DeclarativeSource? source)
    {
        if (source is not null && !ReferenceEquals(source, _generatedSource)) _retiredGeneratedSources.Add(source);
    }
    private void CollectRetiredGeneratedSources()
    {
        List<Exception>? errors = null;
        foreach (var owner in _retiredGeneratedSources.ToArray())
        {
            if (ReferenceEquals(owner, _generatedSource) || ReferenceEquals(owner.Source, Model) ||
                ReferenceEquals(owner.Source, _explicitSource) || ReferenceEquals(owner.Source, _presentation?.Model)) continue;
            // Remove before disposal: user-supplied binding/resource cleanup can
            // reenter configuration, and must not dispose this owner twice.
            _retiredGeneratedSources.Remove(owner);
            try { owner.Dispose(); }
            catch (Exception error) { (errors ??= new()).Add(error); }
        }
        if (errors is not null) throw new AggregateException(errors);
    }
}
