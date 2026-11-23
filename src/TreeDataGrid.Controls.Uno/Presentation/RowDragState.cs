using System;
using System.Collections.Generic;
using TreeDataGridCore;

namespace Uno.Controls.Presentation;

/// <summary>UI-thread drag ownership and reentrant validation without native input dependencies.</summary>
internal class RowDragState
{
    internal RowDragState(ITreeDataGridSource source, IndexPath[] indexes, object[] models)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(indexes);
        ArgumentNullException.ThrowIfNull(models);
        if (indexes.Length == 0 || indexes.Length != models.Length)
            throw new ArgumentException("A drag requires matching, nonempty model and index snapshots.", nameof(indexes));
        Source = source;
        Indexes = Array.AsReadOnly(indexes);
        Models = Array.AsReadOnly(models);
    }

    public string Token { get; } = Guid.NewGuid().ToString("N");
    public ITreeDataGridSource? Source { get; private set; }
    public IReadOnlyList<IndexPath> Indexes { get; private set; }
    public IReadOnlyList<object> Models { get; private set; }

    internal bool IsCurrent(Func<ITreeDataGridSource, IndexPath, object?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        var source = Source;
        var indexes = Indexes;
        var models = Models;
        if (source is null || indexes.Count == 0) return false;
        for (var index = 0; index < indexes.Count; ++index)
        {
            // Child selectors, collection indexers and enumerators can cancel
            // the drag synchronously. Do not read a cleared live Models list or
            // mistake an early-terminated live Indexes loop for successful validation.
            var model = resolve(source, indexes[index]);
            if (!ReferenceEquals(Source, source) || !ReferenceEquals(Indexes, indexes) || !ReferenceEquals(Models, models))
                return false;
            if (!ReferenceEquals(model, models[index])) return false;
        }
        return true;
    }

    public void Release()
    {
        Source = null;
        Indexes = Array.Empty<IndexPath>();
        Models = Array.Empty<object>();
    }
}
