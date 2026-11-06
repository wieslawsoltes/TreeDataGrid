using System;
using System.Collections.Generic;
using TreeDataGridCore;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

/// <summary>View-owned creation policy for a shared Core source.</summary>
public interface ITreeDataGridPresentationOptions
{
    TreeDataGridPresentation Create(ITreeDataGridSource model);
}

/// <summary>Untyped registrations retained for applications using heterogeneous Core models.</summary>
public sealed class TreeDataGridPresentationOptions : ITreeDataGridPresentationOptions
{
    public Dictionary<string, Func<IColumn, CellColumn>> Columns { get; } = new(StringComparer.Ordinal);
    public TreeDataGridPresentation Create(ITreeDataGridSource model) =>
        TreeDataGridPresentation.CreateUntyped(model, this);
}

/// <summary>Column factories whose input model type is checked before any view is created.</summary>
public sealed class TreeDataGridPresentationOptions<TModel> : ITreeDataGridPresentationOptions where TModel : class
{
    public IDictionary<string, Func<IColumn<TModel>, ICellColumn<TModel>>> Columns { get; } =
        new Dictionary<string, Func<IColumn<TModel>, ICellColumn<TModel>>>(StringComparer.Ordinal);

    public TreeDataGridPresentation Create(ITreeDataGridSource model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model is not ITreeDataGridSource<TModel> typed)
            throw new ArgumentException($"Presentation options for {typeof(TModel)} require a Core source with the same model type.", nameof(model));
        return new TreeDataGridPresentation<TModel>(typed, this);
    }
}
