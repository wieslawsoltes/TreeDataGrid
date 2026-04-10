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
    private ITreeDataGridSource? _source;
    private ITreeSelectionModel? _rowSelection;

    public TreeDataGridAutomationPeer(TreeDataGrid owner)
        : base(owner)
    {
        owner.PropertyChanged += OnOwnerPropertyChanged;
        AttachSource(owner.Source);
    }

    public new TreeDataGrid Owner => (TreeDataGrid)base.Owner;

    public bool CanSelectMultiple => _rowSelection?.SingleSelect == false;

    public bool IsSelectionRequired => false;

    public IReadOnlyList<AutomationPeer> GetSelection()
    {
        if (_rowSelection is null || Owner.Rows is not { } rows)
        {
            return Array.Empty<AutomationPeer>();
        }

        List<AutomationPeer>? result = null;

        foreach (var modelIndex in _rowSelection.SelectedIndexes)
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
        if (providerType == typeof(ISelectionProvider) && _rowSelection is null)
        {
            return null;
        }

        return base.GetProviderCore(providerType);
    }

    private void AttachSource(ITreeDataGridSource? source)
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

        AttachRowSelection(Owner.RowSelection as ITreeSelectionModel);
    }

    private void AttachRowSelection(ITreeSelectionModel? rowSelection)
    {
        if (_rowSelection is not null)
        {
            _rowSelection.SelectionChanged -= OnRowSelectionChanged;
        }

        _rowSelection = rowSelection;

        if (_rowSelection is not null)
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
        if (e.Property == TreeDataGrid.SourceProperty)
        {
            AttachSource(Owner.Source);
            RaiseSelectionChanged();
        }
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ITreeDataGridSource.Selection))
        {
            AttachRowSelection(Owner.RowSelection as ITreeSelectionModel);
            RaiseSelectionChanged();
        }
    }

    private void OnRowSelectionChanged(object? sender, TreeDataGridSelectionChangedEventArgs e)
    {
        RaiseSelectionChanged();
    }
}
