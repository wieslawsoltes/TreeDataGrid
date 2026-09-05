using Avalonia.Controls.Presentation;
// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.VisualTree;

namespace Avalonia.Controls.Automation.Peers;

public class TreeDataGridAutomationPeer : ControlAutomationPeer, ISelectionProvider
{
    private TreeDataGridPresentation? _source;
    private ITreeDataGridSelectionInteraction? _rowSelection;
    private ITreeSelectionModel? _legacyRowSelection;

    public TreeDataGridAutomationPeer(TreeDataGrid owner)
        : base(owner)
    {
        owner.PropertyChanged += OnOwnerPropertyChanged;
        AttachSource(owner.Presentation);
    }

    public new TreeDataGrid Owner => (TreeDataGrid)base.Owner;

    public bool CanSelectMultiple => _source?.CanSelectMultiple == true;

    public bool IsSelectionRequired => false;

    public IReadOnlyList<AutomationPeer> GetSelection()
    {
        if (_source?.SelectedIndexes is null || Owner.Rows is not { } rows)
        {
            return Array.Empty<AutomationPeer>();
        }

        List<AutomationPeer>? result = null;

        foreach (var modelIndex in _source!.SelectedIndexes!)
        {
            var rowIndex = rows.ModelIndexToRowIndex(modelIndex);

            if (rowIndex >= 0 &&
                Owner.TryGetRow(rowIndex) is TreeDataGridRow row &&
                row.IsAttachedToVisualTree())
            {
                result ??= new List<AutomationPeer>();
                result.Add(GetOrCreate(row));
            }
        }

        return result ?? (IReadOnlyList<AutomationPeer>)Array.Empty<AutomationPeer>();
    }

    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.DataGrid;
    }

    protected override object? GetProviderCore(Type providerType)
    {
        if (providerType == typeof(ISelectionProvider) && _source?.SelectedIndexes is null)
        {
            return null;
        }

        return base.GetProviderCore(providerType);
    }

    private void AttachSource(TreeDataGridPresentation? source)
    {
        if (_source is not null)
        {
            _source.PropertyChanged -= OnSourcePropertyChanged;
        }

        _source = source;

        if (_source is not null)
        {
            _source.PropertyChanged += OnSourcePropertyChanged;
        }

        AttachRowSelection();
    }

    private void AttachRowSelection()
    {
        if (_legacyRowSelection is not null)
        {
            _legacyRowSelection.SelectionChanged -= OnRowSelectionChanged;
        }

        if (_rowSelection is not null)
        {
            _rowSelection.SelectionChanged -= OnRowSelectionChanged;
        }

        _legacyRowSelection = _source?.SourceIdentity is ITreeDataGridSource legacySource ?
            legacySource.Selection as ITreeDataGridRowSelectionModel : null;
        _rowSelection = _source?.SelectionInteraction;

        if (_legacyRowSelection is not null)
        {
            _legacyRowSelection.SelectionChanged += OnRowSelectionChanged;
        }
        else if (_rowSelection is not null)
        {
            _rowSelection.SelectionChanged += OnRowSelectionChanged;
        }
    }

    private void RaiseSelectionChanged()
    {
        RaisePropertyChangedEvent(SelectionPatternIdentifiers.SelectionProperty, null, null);
    }

    private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TreeDataGrid.PresentationProperty)
        {
            AttachSource(Owner.Presentation);
            RaiseSelectionChanged();
        }
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ITreeDataGridSource.Selection) || e.PropertyName == nameof(TreeDataGridPresentation.SelectionInteraction))
        {
            AttachRowSelection();
            RaiseSelectionChanged();
        }
    }

    private void OnRowSelectionChanged(object? sender, EventArgs e)
    {
        RaiseSelectionChanged();
    }
}
