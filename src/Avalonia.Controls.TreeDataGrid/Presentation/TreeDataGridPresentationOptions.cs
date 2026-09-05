using System;
using System.Collections.Generic;
using Core = global::TreeDataGridCore;
namespace Avalonia.Controls.Presentation
{
    public interface ITreeDataGridPresentationOptions
    {
        TreeDataGridPresentation Create(Core.ITreeDataGridSource model);
    }
    /// <summary>Factories owned by the view. The Core model contains only presentation keys.</summary>
    public sealed class TreeDataGridPresentationOptions<TModel> : ITreeDataGridPresentationOptions where TModel : class
    {
        public IDictionary<string, Func<Core.Models.IColumn<TModel>, ICellColumn<TModel>>> Columns { get; } =
            new Dictionary<string, Func<Core.Models.IColumn<TModel>, ICellColumn<TModel>>>(StringComparer.Ordinal);
        public TreeDataGridPresentation Create(Core.ITreeDataGridSource model) => new TreeDataGridPresentation<TModel>((Core.ITreeDataGridSource<TModel>)model, this);
    }
}
