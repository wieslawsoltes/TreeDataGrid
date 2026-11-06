using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using TreeDataGridCore;
using Windows.ApplicationModel.DataTransfer;

namespace Uno.Controls.Models.TreeDataGrid;

/// <summary>Describes the Core source and model-space indexes of a row drag.</summary>
/// <remarks>
/// The source is borrowed, never copied or disposed. Native data packages carry
/// a process-local token, not a serialized source or arbitrary model objects.
/// A caller retaining this snapshot deliberately retains its borrowed source.
/// </remarks>
public class DragInfo
{
    /// <summary>The native data-package property carrying the row-drag token.</summary>
    public static readonly string DataFormat = "TreeDataGrid.Controls.Uno.RowDrag";

    public DragInfo(ITreeDataGridSource source, IEnumerable<IndexPath> indexes)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(indexes);
        Source = source;
        Indexes = indexes;
    }

    /// <summary>The original shared Core source; this object does not own it.</summary>
    public ITreeDataGridSource Source { get; }
    /// <summary>The Core-model alias used by the shared-source API.</summary>
    public ITreeDataGridSource Model => Source;
    /// <summary>The supplied model-space paths, preserving order and enumeration semantics.</summary>
    public IEnumerable<IndexPath> Indexes { get; }

    /// <summary>Resolves a live, current row drag from this process's native data package.</summary>
    /// <remarks>
    /// Unknown, expired and cancelled tokens return false. Validation can invoke
    /// source/child selectors; application exceptions are not silently swallowed.
    /// Call from the UI thread that owns the drag. Constructing a DragInfo does
    /// not register a drag or grant automatic-drop permission.
    /// </remarks>
    public static bool TryGet(DataPackageView data, [NotNullWhen(true)] out DragInfo? info)
    {
        ArgumentNullException.ThrowIfNull(data);
        return global::Uno.Controls.TreeDataGrid.TryGetRowDragInfo(data, out info);
    }
}
