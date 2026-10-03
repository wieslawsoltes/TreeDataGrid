using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using Uno.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;

namespace Uno.Controls;

public enum TreeDataGridRowDropPosition { None, Before, After, Inside }

public class TreeDataGridRowDragStartedEventArgs : RoutedEventArgs
{
    private DataPackageOperation _allowedEffects;
    private bool _cancel;
    public TreeDataGridRowDragStartedEventArgs(IEnumerable<object> models)
    { Models = models.ToArray(); Indexes = Array.Empty<IndexPath>(); }
    internal TreeDataGridRowDragStartedEventArgs(ITreeDataGridSource source, IReadOnlyList<IndexPath> indexes,
        IReadOnlyList<object> models, DragStartingEventArgs inner)
    { Source = source; Indexes = indexes; Models = models; Inner = inner; }
    public ITreeDataGridSource? Source { get; }
    public IReadOnlyList<IndexPath> Indexes { get; }
    public IReadOnlyList<object> Models { get; }
    public DragStartingEventArgs? Inner { get; }
    public DataPackage? Data => Inner?.Data;
    public bool Handled { get; set; }
    public bool Cancel { get => Inner?.Cancel ?? _cancel; set { _cancel = value; if (Inner is not null) Inner.Cancel = value; } }
    public DataPackageOperation AllowedEffects
    {
        get => Inner?.AllowedOperations ?? _allowedEffects;
        set { _allowedEffects = value; if (Inner is not null) Inner.AllowedOperations = value; }
    }
}

public class TreeDataGridRowDragEventArgs : RoutedEventArgs
{
    public TreeDataGridRowDragEventArgs(TreeDataGridRow? row, DragEventArgs inner)
    { Inner = inner; TargetRow = row; TargetRowIndex = row?.RowIndex ?? -1; TargetModel = row?.Model; }
    internal TreeDataGridRowDragEventArgs(DragEventArgs inner, TreeDataGridRow? row, int rowIndex, IndexPath index, object? model, TreeDataGridRowDropPosition position)
        : this(row, inner)
    { TargetRowIndex = rowIndex; TargetIndex = index; TargetModel = model; Position = position; }
    public DragEventArgs Inner { get; }
    public TreeDataGridRow? TargetRow { get; }
    public int TargetRowIndex { get; }
    public IndexPath TargetIndex { get; }
    public object? TargetModel { get; }
    public TreeDataGridRowDropPosition Position { get; set; }
    /// <summary>True means the handler owns the drop; automatic Core movement is skipped.
    /// Set this before awaiting a native deferral in a custom drop handler.</summary>
    public bool Handled { get; set; }
}

public sealed class TreeDataGridRowDragFailedEventArgs(Exception error) : EventArgs
{
    public Exception Error { get; } = error;
}
